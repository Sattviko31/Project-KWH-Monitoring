using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using KWHMonitoring.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KWHMonitoring.Services
{
    /// <summary>
    /// Membaca data master titik lokasi dan daya terpasang dari database ERP
    /// WWMERP2019 menggunakan relasi TagihanListrik -> RekListrik -> TitikLokasi.
    ///
    /// - DeviceKey pada database monitoring dipasangkan dengan kolom TitikLokasiID.
    /// - Koneksi: "ConnectionStrings:WWMERPConnection" bila diisi (koneksi ke database ERP
    ///   yang terpisah). Bila dibiarkan kosong, query dijalankan lewat
    ///   "ConnectionStrings:DefaultConnection" memakai nama 3-bagian
    ///   (WwmErp:Database + WwmErp:Table) — untuk kondisi ERP berada di instance yang sama.
    /// - Semua kegagalan (database tidak ada, login ditolak, tabel tidak ada) ditangani aman:
    ///   halaman monitoring tetap normal, hanya info lokasi yang tidak ditampilkan.
    /// - Hasil di-cache in-memory (WwmErp:CacheMinutes) dan ada cooldown kegagalan
    ///   (WwmErp:FailCooldownSeconds) supaya database ERP tidak ditunggu berulang kali.
    /// </summary>
    public class TitikLokasiService : ITitikLokasiService
    {
        private const string DefaultTable = "dbo.TitikLokasi";
        private const string DefaultDatabase = "WWMERP2019";
        private const string DefaultBillingTable = "dbo.TagihanListrik";
        private const string DefaultElectricityAccountTable = "dbo.RekListrik";
        private const string DefaultLocationDetailTable = "dbo.TitikLokasiDetail";
        private const string DefaultClientTable = "dbo.Client";
        private const int MaxParametersPerBatch = 500;

        private const string CachePrefix = "titiklokasi:key:";
        private const string FailCooldownCacheKey = "titiklokasi:failcooldown";

        // Penanda "sudah dicari tapi tidak ada" agar device tanpa data tidak di-query ulang.
        private static readonly object MissSentinel = new object();

        private static readonly Regex SafeNameRegex =
            new Regex(@"^[A-Za-z0-9_\.\[\]]{1,128}$", RegexOptions.Compiled);

        // Kolom teks ERP (khususnya Address) banyak yang mengandung CR/LF dan spasi ganda,
        // diringkas menjadi satu spasi agar tampilan tooltip rapi.
        private static readonly Regex WhitespaceRegex = new Regex(@"\s+", RegexOptions.Compiled);

        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly ILogger<TitikLokasiService> _logger;

        public TitikLokasiService(IConfiguration configuration, IMemoryCache cache, ILogger<TitikLokasiService> logger)
        {
            _configuration = configuration;
            _cache = cache;
            _logger = logger;
        }

        public async Task<Dictionary<string, TitikLokasi>> GetByDeviceKeysAsync(IEnumerable<string> deviceKeys)
        {
            var result = new Dictionary<string, TitikLokasi>(StringComparer.OrdinalIgnoreCase);
            if (deviceKeys == null) return result;

            var requested = deviceKeys
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (requested.Count == 0) return result;

            // Cache ERP dapat dinonaktifkan dengan CacheMinutes = 0.
            // Saat nonaktif, jangan membaca cache lama yang mungkin masih hidup
            // di memory dari konfigurasi/proses sebelumnya.
            var cacheMinutes = GetNonNegativeIntSetting("WwmErp:CacheMinutes", 0);
            var useCache = cacheMinutes > 0;
            var missing = new List<string>();
            foreach (var key in requested)
            {
                if (useCache && _cache.TryGetValue(CachePrefix + key, out var cached))
                {
                    if (cached is TitikLokasi lokasi) result[key] = lokasi;
                }
                else
                {
                    missing.Add(key);
                }
            }

            if (missing.Count == 0) return result;

            // Sumber ERP sedang bermasalah: jangan dicoba lagi agar halaman monitoring tetap cepat.
            if (_cache.TryGetValue(FailCooldownCacheKey, out _)) return result;

            // Cache ERP sengaja dapat dinonaktifkan dengan CacheMinutes = 0.
            // Untuk data ID pelanggan, Catatan, dan daya terpasang, dashboard harus
            // membaca hasil terbaru dari ERP pada setiap pemuatan bila cache = 0.
            var cacheOptions = cacheMinutes > 0
                ? new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(cacheMinutes)
                }
                : null;

            try
            {
                var rows = await QueryWithTimeoutAsync(missing);

                foreach (var key in missing)
                {
                    if (rows.TryGetValue(key, out var row))
                    {
                        result[key] = row;
                        if (cacheOptions != null)
                        {
                            _cache.Set(CachePrefix + key, row, cacheOptions);
                        }
                    }
                    else
                    {
                        if (cacheOptions != null)
                        {
                            _cache.Set(CachePrefix + key, MissSentinel, cacheOptions);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var cooldown = GetIntSetting("WwmErp:FailCooldownSeconds", 60);
                _cache.Set(FailCooldownCacheKey, true, TimeSpan.FromSeconds(cooldown));
                _logger.LogWarning(ex,
                    "Data TitikLokasi (WWMERP) tidak dapat dibaca. Tooltip panel tampil tanpa info lokasi.");
            }

            return result;
        }

        /// <summary>
        /// Menjalankan QueryAsync dengan batas waktu total (WwmErp:OverallTimeoutSeconds).
        ///
        /// Connect Timeout saja tidak cukup: kalau host ERP drop paket (firewall diam / host mati),
        /// koneksi TCP baru bisa gagal setelah 20+ detik sehingga request halaman monitoring
        /// ikut tertahan ("stuck") walau Connect Timeout sudah kecil. Karena itu query ERP
        /// dibatasi di level aplikasi; bila lewat batas, info lokasi dilewati untuk request ini
        /// dan database ERP tidak dicoba lagi selama WwmErp:FailCooldownSeconds.
        /// </summary>
        private async Task<Dictionary<string, TitikLokasi>> QueryWithTimeoutAsync(IList<string> deviceKeys)
        {
            var timeoutSeconds = GetIntSetting("WwmErp:OverallTimeoutSeconds", 2);

            var queryTask = QueryAsync(deviceKeys);

            // 0 atau negatif = tanpa batas (perilaku lama).
            if (timeoutSeconds <= 0) return await queryTask;

            var finished = await Task.WhenAny(queryTask, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));
            if (finished != queryTask)
            {
                // Query yang ditinggal tetap berjalan di background; exception-nya diobservasi
                // agar tidak menjadi unobserved task exception.
                _ = queryTask.ContinueWith(t => { var ignored = t.Exception; },
                    TaskContinuationOptions.OnlyOnFaulted);

                throw new TimeoutException(
                    "Query TitikLokasi (WWMERP) melebihi " + timeoutSeconds +
                    " detik. Info lokasi dilewati untuk request ini.");
            }

            return await queryTask;
        }

        /// <summary>
        /// Query ke database ERP. Dipisah dari GetByDeviceKeysAsync agar seluruh
        /// exception koneksi/query bisa ditangani di satu tempat.
        /// </summary>
        private async Task<Dictionary<string, TitikLokasi>> QueryAsync(IList<string> deviceKeys)
        {
            var found = new Dictionary<string, TitikLokasi>(StringComparer.OrdinalIgnoreCase);

            var target = ResolveTarget();
            if (string.IsNullOrWhiteSpace(target.ConnectionString))
            {
                _logger.LogWarning("Connection string database ERP belum dikonfigurasi " +
                                   "(WWMERPConnection / DefaultConnection). Info TitikLokasi dilewati.");
                return found;
            }

            var connectTimeout = GetIntSetting("WwmErp:ConnectTimeoutSeconds", 5);
            var commandTimeout = GetIntSetting("WwmErp:CommandTimeoutSeconds", 8);

            var builder = new SqlConnectionStringBuilder(target.ConnectionString)
            {
                ConnectTimeout = connectTimeout,
                // Retry koneksi internal SqlClient dimatikan supaya tidak menambah waktu tunggu
                // (lihat juga batas waktu total di QueryWithTimeoutAsync).
                ConnectRetryCount = 0,
                ApplicationName = "KWHMonitoring"
            };

            using (var connection = new SqlConnection(builder.ConnectionString))
            {
                await connection.OpenAsync();

                // Parameter SQL Server maksimum 2100 -> kirim per batch.
                for (var offset = 0; offset < deviceKeys.Count; offset += MaxParametersPerBatch)
                {
                    var batch = deviceKeys.Skip(offset).Take(MaxParametersPerBatch).ToList();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandTimeout = commandTimeout;

                        var parameterNames = new List<string>(batch.Count);
                        for (var i = 0; i < batch.Count; i++)
                        {
                            var name = "@k" + i;
                            parameterNames.Add(name);
                            command.Parameters.AddWithValue(name, batch[i]);
                        }

                        // TitikLokasiID dapat berupa kolom numerik maupun teks, jadi dibandingkan
                        // sebagai teks: CONVERT(nvarchar(50), TitikLokasiID) = DeviceKey.
                        // Satu lokasi dapat memiliki banyak tagihan/rekening. Jangan
                        // menggunakan SELECT DISTINCT lalu mengambil baris pertama,
                        // karena urutan hasil SQL tidak dijamin. Pilih secara eksplisit
                        // tagihan terbaru per lokasi berdasarkan Periode, lalu gunakan
                        // TagihanListrikID sebagai tie-breaker deterministik.
                        command.CommandText =
                            "WITH LatestErp AS (" +
                            "SELECT " +
                            "r.[RekListrikID], r.[NoPelanggan], r.[IDPelanggan], r.[NamaRekening], " +
                            "l.[KodeLokasi], l.[Address], l.[City], l.[Province], ld.[ArahLokasi], r.[Daya], " +
                            "t.[TagihanListrikID], t.[ClientID], c.[CompanyName] AS [Client], " +
                            "t.[MeterAwal], t.[MeterAkhir], t.[Periode], t.[JumlahTagihan], " +
                            "t.[LoginUser], t.[Catatan], l.[TitikLokasiID], " +
                            "CONCAT(r.[IDPelanggan], ' | ', r.[NamaRekening]) AS [pelanggan], " +
                            "UPPER(l.[Area]) AS [Area], " +
                            "ROW_NUMBER() OVER (" +
                            "PARTITION BY l.[TitikLokasiID] " +
                            "ORDER BY t.[Periode] DESC, t.[TagihanListrikID] DESC" +
                            ") AS [RowNumber] " +
                            "FROM " + target.BillingTableReference + " t " +
                            "INNER JOIN " + target.ElectricityAccountTableReference + " r " +
                            "ON t.[RekListrikID] = r.[RekListrikID] " +
                            "INNER JOIN " + target.TableReference + " l " +
                            "ON r.[TitikLokasiID] = l.[TitikLokasiID] " +
                            "INNER JOIN " + target.LocationDetailTableReference + " ld " +
                            "ON l.[TitikLokasiID] = ld.[TitikLokasiID] " +
                            "LEFT JOIN " + target.ClientTableReference + " c " +
                            "ON t.[ClientID] = c.[ClientID] " +
                            "WHERE CONVERT(nvarchar(50), l.[TitikLokasiID]) IN (" +
                            string.Join(", ", parameterNames) + ")" +
                            ") " +
                            "SELECT * FROM LatestErp WHERE [RowNumber] = 1";

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var id = Convert.ToString(reader["TitikLokasiID"]);
                                if (string.IsNullOrWhiteSpace(id)) continue;

                                var lokasi = new TitikLokasi
                                {
                                    TitikLokasiID = Normalize(id),
                                    KodeLokasi = Normalize(Convert.ToString(reader["KodeLokasi"])),
                                    Address = Normalize(Convert.ToString(reader["Address"])),
                                    City = Normalize(Convert.ToString(reader["City"])),
                                    Province = Normalize(Convert.ToString(reader["Province"])),
                                    IDPelanggan = Normalize(Convert.ToString(reader["IDPelanggan"])),
                                    Catatan = Normalize(Convert.ToString(reader["Catatan"])),
                                    DayaVA = ReadDecimal(reader["Daya"])
                                };

                                if (!found.ContainsKey(lokasi.TitikLokasiID)) found[lokasi.TitikLokasiID] = lokasi;
                            }
                        }
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// Tentukan koneksi + nama tabel yang dipakai:
        /// 1) WWMERPConnection diisi  -> koneksi khusus ke database ERP, tabel "dbo.TitikLokasi".
        /// 2) WWMERPConnection kosong -> pakai DefaultConnection dengan nama 3-bagian
        ///    "WWMERP2019.dbo.TitikLokasi" (ERP pada instance yang sama).
        /// Nama database/tabel dari konfigurasi divalidasi agar tidak bisa dipakai untuk injeksi.
        /// </summary>
        private TargetInfo ResolveTarget()
        {
            var configuredTable = GetSetting("WwmErp:Table");
            var table = SafeName(configuredTable) ?? DefaultTable;
            var database = SafeName(GetSetting("WwmErp:Database")) ?? DefaultDatabase;
            var billingTable = SafeName(GetSetting("WwmErp:BillingTable")) ?? DefaultBillingTable;
            var electricityAccountTable = SafeName(GetSetting("WwmErp:ElectricityAccountTable")) ?? DefaultElectricityAccountTable;
            var locationDetailTable = SafeName(GetSetting("WwmErp:LocationDetailTable")) ?? DefaultLocationDetailTable;
            var clientTable = SafeName(GetSetting("WwmErp:ClientTable")) ?? DefaultClientTable;

            var erpConnection = _configuration.GetConnectionString("WWMERPConnection");
            if (!string.IsNullOrWhiteSpace(erpConnection))
            {
                return new TargetInfo
                {
                    ConnectionString = erpConnection,
                    TableReference = table,
                    BillingTableReference = billingTable,
                    ElectricityAccountTableReference = electricityAccountTable,
                    LocationDetailTableReference = locationDetailTable,
                    ClientTableReference = clientTable
                };
            }

            return new TargetInfo
            {
                ConnectionString = _configuration.GetConnectionString("DefaultConnection"),
                TableReference = database + "." + table,
                BillingTableReference = database + "." + billingTable,
                ElectricityAccountTableReference = database + "." + electricityAccountTable,
                LocationDetailTableReference = database + "." + locationDetailTable,
                ClientTableReference = database + "." + clientTable
            };
        }

        /// <summary>
        /// Ringkas spasi/newline berlebih pada teks ERP agar tampilan tooltip rapi
        /// (kolom Address ERP sering berakhiran CR/LF, mis. "JPO Basuki Rahmat \r\n").
        /// </summary>
        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return WhitespaceRegex.Replace(value, " ").Trim();
        }

        private static decimal ReadDecimal(object value)
        {
            if (value == null || value == DBNull.Value) return 0m;
            return decimal.TryParse(Convert.ToString(value), out var result) && result > 0m ? result : 0m;
        }

        private static string SafeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var trimmed = name.Trim();
            return SafeNameRegex.IsMatch(trimmed) ? trimmed : null;
        }

        private int GetIntSetting(string key, int fallback)
        {
            var raw = _configuration[key];
            return int.TryParse(raw, out var value) && value > 0 ? value : fallback;
        }

        private int GetNonNegativeIntSetting(string key, int fallback)
        {
            var raw = _configuration[key];
            return int.TryParse(raw, out var value) && value >= 0 ? value : fallback;
        }

        private string GetSetting(string key)
        {
            var raw = _configuration[key];
            return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
        }

        private class TargetInfo
        {
            public string ConnectionString { get; set; }
            public string TableReference { get; set; }
            public string BillingTableReference { get; set; }
            public string ElectricityAccountTableReference { get; set; }
            public string LocationDetailTableReference { get; set; }
            public string ClientTableReference { get; set; }
        }

    }
}
