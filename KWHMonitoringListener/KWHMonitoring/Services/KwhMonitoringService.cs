using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using KWHMonitoring.Configuration;

namespace KWHMonitoring.Services;

public class KwhMonitoringService : BackgroundService
{
    private readonly ILogger<KwhMonitoringService> _logger;
    private readonly AppConfig _config;
    private readonly string _connectionString;

    private IMqttClient? _mqttClient;
    private long _messageCount, _errorCount, _successCount;
    private long _relayMessageCount;
    private volatile bool _sqlHealthy = true;

    private readonly ConcurrentQueue<MqttMessageBuffer> _messageQueue = new();
    private readonly ConcurrentDictionary<string, string> _deviceKeyCache = new();
    private readonly Dictionary<string, string> _columnMapping = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _lastKwhSaveByDevice = new();
    private readonly ConcurrentDictionary<string, string?> _lastRciValueByDevice = new();

    // ============================================================
    // KOLOM KWHData - SESUAI STRUKTUR DATABASE FINAL (11 kolom data)
    // ============================================================
    private static readonly HashSet<string> KWHDataColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "PHASE_R", "PHASE_S", "PHASE_T",
        "AMPERE_R", "AMPERE_S", "AMPERE_T",
        "COSPHI", "W", "AKTIF_POWER", "TOTALW", "F"
    };

    // Kolom untuk RelayControl
    private static readonly HashSet<string> RelayControlColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "RC",
        "RCI"
    };

    // Kolom sistem (tidak disimpan sebagai data)
    private static readonly HashSet<string> SystemColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "_terminalTime", "_groupName"
    };

    public KwhMonitoringService(ILogger<KwhMonitoringService> logger, IOptions<AppConfig> config)
    {
        _logger = logger;
        _config = config.Value;
        _connectionString = _config.Database.GetConnectionString();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("╔══════════════════════════════════════════════════════════╗");
        _logger.LogInformation("║   HAIWELL ELECTRICAL - MQTT TO SQL SERVER SERVICE        ║");
        _logger.LogInformation("║   Version 12.0 - AutoReconnect (KWHData + RelayControl)  ║");
        _logger.LogInformation("╚══════════════════════════════════════════════════════════╝");

        try
        {
            // Tunggu SQL Server tersedia sebelum melanjutkan, agar service tetap
            // hidup meski SQL/MQTT belum siap saat PC dinyalakan ulang.
            _logger.LogInformation("[*] Menunggu SQL Server tersedia...");
            await WaitForSqlReadyAsync(stoppingToken);

            _logger.LogInformation("[*] Memulai Smart Migration Database...");
            await RunStartupDatabaseTaskAsync(stoppingToken);

            if (_config.Sampling.IntervalSeconds > 0)
                _logger.LogInformation("[*] Sampling data aktif: 1 data per {Interval} detik per perangkat", _config.Sampling.IntervalSeconds);
            else
                _logger.LogInformation("[*] Sampling data: non-aktif (semua data disimpan)");

            _ = Task.Run(() => ProcessQueueAsync(stoppingToken), stoppingToken);
            _ = Task.Run(() => MonitorQueueAsync(stoppingToken), stoppingToken);

            _logger.LogInformation("[*] Menghubungkan ke MQTT Broker...");
            _ = Task.Run(() => MaintainMqttConnectionAsync(stoppingToken), stoppingToken);

            _logger.LogInformation("[✓] Service berjalan normal di background.");
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[*] Service dihentikan.");
        }
        catch (SqlException ex) when (ex.Number == 258 || ex.Number == -2 || ex.Number == 10060 || ex.Number == 10061 || ex.Number == 18456 || ex.Number == 4060)
        {
            _logger.LogCritical(ex, "[✗] Gagal koneksi ke SQL Server");
            _logger.LogError("TROUBLESHOOTING - SQL Server Connection Failed:");
            _logger.LogError("1. Cek SQL Server dapat diakses: ping {Server}", _config.Database.Server);
            _logger.LogError("2. Cek port 1433 terbuka: telnet {Server} 1433", _config.Database.Server);
            _logger.LogError("3. Cek SQL Server Configuration Manager: TCP/IP → Enabled");
            _logger.LogError("4. Cek Windows Firewall → Allow port 1433");
            _logger.LogError("5. Cek SQL Server Authentication Mode → Mixed Mode");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "[✗] Fatal error pada service");
        }
        finally
        {
            try
            {
                if (_mqttClient?.IsConnected == true) _mqttClient.DisconnectAsync().GetAwaiter().GetResult();
            }
            catch { }
            LogStats("final");
        }
    }

    #region Smart Migration
    private async Task RunMigrationAsync(CancellationToken ct)
    {
        var startTime = DateTime.Now;
        using var conn = new SqlConnection(_config.Database.GetMasterConnectionString());
        await conn.OpenAsync(ct);

        // Validasi identifier SQL sebelum digunakan di dynamic SQL
        if (!IsValidSqlIdentifier(_config.Database.DatabaseName))
            throw new ArgumentException("DatabaseName tidak valid.");
        if (!IsValidSqlIdentifier(_config.Database.Username))
            throw new ArgumentException("Database Username tidak valid.");

        using var checkDbCmd = new SqlCommand("SELECT DB_ID(@dbName)", conn);
        checkDbCmd.Parameters.AddWithValue("@dbName", _config.Database.DatabaseName);
        var dbId = await checkDbCmd.ExecuteScalarAsync(ct);
        bool dbExists = dbId != null && dbId != DBNull.Value;

        if (!dbExists)
        {
            _logger.LogInformation($"[+] Membuat database '{_config.Database.DatabaseName}'...");
            using var createDbCmd = new SqlCommand($"CREATE DATABASE [{_config.Database.DatabaseName}]", conn);
            createDbCmd.CommandTimeout = 120;
            await createDbCmd.ExecuteNonQueryAsync(ct);
            _logger.LogInformation($"[✓] Database berhasil dibuat.");
        }
        else
        {
            _logger.LogInformation($"[✓] Database '{_config.Database.DatabaseName}' sudah ada.");
        }

        // User & Role (aman: identifier divalidasi, password di-escape)
        var safePassword = EscapeSqlStringLiteral(_config.Database.Password);
        var userScript = $@"
            USE [{_config.Database.DatabaseName}];

            DECLARE @IsDbo BIT = 0;
            IF EXISTS (
                SELECT 1 FROM sys.database_principals dp
                INNER JOIN sys.server_principals sp ON dp.sid = sp.sid
                WHERE sp.name = '{_config.Database.Username}' AND dp.name = 'dbo'
            )
                SET @IsDbo = 1;

            IF @IsDbo = 0
            BEGIN
                IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = '{_config.Database.Username}')
                    CREATE LOGIN [{_config.Database.Username}] WITH PASSWORD = '{safePassword}',
                        DEFAULT_DATABASE = [{_config.Database.DatabaseName}], CHECK_EXPIRATION = OFF, CHECK_POLICY = OFF;

                IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = '{_config.Database.Username}')
                    CREATE USER [{_config.Database.Username}] FOR LOGIN [{_config.Database.Username}];

                IF IS_ROLEMEMBER('db_ddladmin', '{_config.Database.Username}') IS NULL OR IS_ROLEMEMBER('db_ddladmin', '{_config.Database.Username}') = 0
                    ALTER ROLE [db_ddladmin] ADD MEMBER [{_config.Database.Username}];
                IF IS_ROLEMEMBER('db_datareader', '{_config.Database.Username}') IS NULL OR IS_ROLEMEMBER('db_datareader', '{_config.Database.Username}') = 0
                    ALTER ROLE [db_datareader] ADD MEMBER [{_config.Database.Username}];
                IF IS_ROLEMEMBER('db_datawriter', '{_config.Database.Username}') IS NULL OR IS_ROLEMEMBER('db_datawriter', '{_config.Database.Username}') = 0
                    ALTER ROLE [db_datawriter] ADD MEMBER [{_config.Database.Username}];
            END";

        using (var cmd = new SqlCommand(userScript, conn))
        {
            cmd.CommandTimeout = 60;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Smart Migration
        _logger.LogInformation("[*] Memeriksa objek database yang sudah ada...");

        var existingObjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = new SqlCommand(@"
            SELECT name FROM sys.tables WHERE type = 'U'
            UNION SELECT name FROM sys.views WHERE type = 'V'
            UNION SELECT name FROM sys.procedures WHERE type IN ('P', 'PC')
            UNION SELECT name FROM sys.indexes WHERE type IN (1, 2) AND name IS NOT NULL", conn))
        using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
                existingObjects.Add(reader.GetString(0));
        }
        _logger.LogInformation($"[✓] Ditemukan {existingObjects.Count} objek yang sudah ada.");

        _logger.LogInformation("[*] Membuat objek yang belum ada...");

        var cleanScript = MigrationScript.Replace("[HaiwellElectrical]", $"[{_config.Database.DatabaseName}]");

        if (dbExists)
        {
            cleanScript = Regex.Replace(cleanScript, @"ALTER\s+DATABASE\s+\[.*?\].*?GO", "",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            cleanScript = Regex.Replace(cleanScript, @"CREATE\s+DATABASE\s+\[.*?\][\s\S]*?GO", "",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }

        cleanScript = Regex.Replace(cleanScript, @"USE\s+\[master\].*?GO", "",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        cleanScript = Regex.Replace(cleanScript, @"CREATE\s+USER\s+\[.*?\].*?GO", "",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        cleanScript = Regex.Replace(cleanScript, @"ALTER\s+ROLE\s+\[.*?\].*?GO", "",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        var batches = Regex.Split(cleanScript, @"^\s*GO\s*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        int success = 0, skipped = 0;

        foreach (var batch in batches)
        {
            var b = batch.Trim();
            if (string.IsNullOrEmpty(b)) continue;

            var match = Regex.Match(b, @"CREATE\s+(?:TABLE|VIEW|PROC(?:EDURE)?)\s+\[?(?:dbo\.)?\[?(\w+)\]?",
                RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var objectName = match.Groups[1].Value;
                if (existingObjects.Contains(objectName))
                {
                    skipped++;
                    continue;
                }
            }

            var indexMatch = Regex.Match(b, @"CREATE\s+(?:UNIQUE\s+)?(?:NONCLUSTERED\s+)?INDEX\s+\[?(\w+)\]?",
                RegexOptions.IgnoreCase);
            if (indexMatch.Success)
            {
                var indexName = indexMatch.Groups[1].Value;
                if (existingObjects.Contains(indexName))
                {
                    skipped++;
                    continue;
                }
            }

            try
            {
                using var cmd = new SqlCommand(b, conn);
                cmd.CommandTimeout = 60;
                await cmd.ExecuteNonQueryAsync(ct);
                success++;
            }
            catch (SqlException ex) when (ex.Number is 2714 or 1913 or 2705 or 15023 or 2627 or 2601 or 15231 or 15151 or 1781 or 15063)
            {
                skipped++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[!] Warning: {ex.Message}");
            }
        }

        // ============================================================
        // MIGRASI KOLOM INKREMENTAL (tabel sudah ada, kolom belum ada)
        // ============================================================
        try
        {
            var addColumnSql = $@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID('dbo.RelayControl') AND name = 'RCI'
                )
                BEGIN
                    ALTER TABLE dbo.RelayControl ADD [RCI] [int] NULL;
                END";
            using var cmd = new SqlCommand(addColumnSql, conn);
            cmd.CommandTimeout = 60;
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[!] Warning saat menambahkan kolom RCI: {ex.Message}");
        }

        var elapsed = (DateTime.Now - startTime).TotalSeconds;
        _logger.LogInformation($"[✓] Smart Migration selesai dalam {elapsed:F1} detik. ({success} dibuat, {skipped} dilewati)");
    }
    #endregion

    private async Task WaitForSqlReadyAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var conn = new SqlConnection(_config.Database.GetMasterConnectionString());
                await conn.OpenAsync(ct);
                _logger.LogInformation("[✓] SQL Server tersedia");
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[!] SQL Server belum tersedia: {Message}. Retry in 5s...", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }

    private async Task RunStartupDatabaseTaskAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunMigrationAsync(ct);
                _logger.LogInformation("[*] Memuat konfigurasi dari database...");
                await LoadConfigurationsAsync(ct);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[!] Database startup gagal: {Message}. Retry in 5s...", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }

    #region Configuration & MQTT
    private async Task LoadConfigurationsAsync(CancellationToken ct)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Load ColumnMapping dari database
        using (var cmd = new SqlCommand("SELECT OldColumnName, NewColumnName FROM ColumnMapping WHERE IsActive = 1", conn))
        using (var reader = await cmd.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) _columnMapping[reader.GetString(0).ToUpper()] = reader.GetString(1);

        // Default column mapping: MQTT field → DB column
        if (_columnMapping.Count == 0)
        {
            _columnMapping["VR"] = "PHASE_R";
            _columnMapping["VS"] = "PHASE_S";
            _columnMapping["VT"] = "PHASE_T";
            _columnMapping["AKTIF_W"] = "AKTIF_POWER";
            _columnMapping["RC"] = "RC";
        }
        _logger.LogInformation($"[✓] Loaded {_columnMapping.Count} mappings");
        _logger.LogInformation($"[✓] KWHData columns: PHASE_R/S/T, AMPERE_R/S/T, CosPhi, W, Aktif_Power, TotalW, F");
        _logger.LogInformation($"[✓] RelayControl columns: RC, RCI");
    }

    private async Task MaintainMqttConnectionAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_mqttClient == null)
                {
                    var factory = new MqttFactory();
                    _mqttClient = factory.CreateMqttClient();

                    _mqttClient.ConnectedAsync += async _ =>
                    {
                        _logger.LogInformation("[✓] MQTT connected");
                        try
                        {
                            await _mqttClient.SubscribeAsync(
                                new MqttTopicFilterBuilder()
                                    .WithTopic("#")
                                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                                    .Build(),
                                ct);
                            _logger.LogInformation("[✓] MQTT subscribed to #");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("[!] MQTT subscribe failed: {Message}", ex.Message);
                        }
                    };

                    _mqttClient.DisconnectedAsync += _ =>
                    {
                        _logger.LogWarning("[✗] MQTT disconnected");
                        return Task.CompletedTask;
                    };

                    _mqttClient.ApplicationMessageReceivedAsync += e =>
                    {
                        _messageQueue.Enqueue(new MqttMessageBuffer
                        {
                            Topic = e.ApplicationMessage.Topic,
                            Payload = e.ApplicationMessage.ConvertPayloadToString() ?? "",
                            ReceivedAt = DateTime.Now
                        });
                        return Task.CompletedTask;
                    };
                }

                if (!_mqttClient.IsConnected)
                {
                    var options = new MqttClientOptionsBuilder()
                        .WithTcpServer(_config.Mqtt.BrokerIp, _config.Mqtt.Port)
                        .WithClientId($"KWHApp_{Guid.NewGuid():N}".Substring(0, 20))
                        .WithCleanSession();

                    options.WithTlsOptions(o =>
                    {
                        o.UseTls(_config.Mqtt.UseTls);

                        if (_config.Mqtt.UseClientCertificate)
                        {
                            if (!string.IsNullOrEmpty(_config.Mqtt.ClientCertificatePath) && File.Exists(_config.Mqtt.ClientCertificatePath))
                            {
                                var certificate = new X509Certificate2(_config.Mqtt.ClientCertificatePath, _config.Mqtt.ClientCertificatePassword, X509KeyStorageFlags.Exportable);
                                o.WithClientCertificates(new List<X509Certificate2> { certificate });
                                _logger.LogInformation("[✓] MQTT client certificate loaded");
                            }
                            else
                            {
                                _logger.LogWarning("[!] MQTT client certificate path tidak ditemukan atau kosong");
                            }
                        }
                    });

                    if (!_config.Mqtt.UseClientCertificate && !string.IsNullOrEmpty(_config.Mqtt.Username))
                    {
                        options.WithCredentials(_config.Mqtt.Username, _config.Mqtt.Password);
                    }

                    await _mqttClient.ConnectAsync(options.Build(), ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[!] MQTT connection error: {Message}. Retry in 5s...", ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }
    #endregion

    #region Message Processing
    private async Task ProcessQueueAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (_messageQueue.TryDequeue(out var buffer)) await ProcessSingleMessageAsync(buffer, ct);
            else await Task.Delay(50, ct);
        }
    }

    private async Task MonitorQueueAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(30000, ct);
            LogStats("periodic");
        }
    }

    private void LogStats(string source)
    {
        var total = Interlocked.Read(ref _messageCount);
        var success = Interlocked.Read(ref _successCount);
        var errors = Interlocked.Read(ref _errorCount);
        var relay = Interlocked.Read(ref _relayMessageCount);
        var mqttStatus = _mqttClient?.IsConnected == true ? "Connected" : "Disconnected";
        var dbStatus = _sqlHealthy ? "OK" : "FAIL";
        var health = mqttStatus == "Connected" && _sqlHealthy && _messageQueue.Count < 1000 && (total == 0 || errors <= success)
            ? "Healthy"
            : "Degraded";

        _logger.LogInformation(
            "[STATS] Total={Total} | Success={Success} | Errors={Errors} | Relay={Relay} | Queue={Queue} | MQTT={Mqtt} | DB={Db} | Health={Health}",
            total, success, errors, relay, _messageQueue.Count, mqttStatus, dbStatus, health);
    }

    private async Task ProcessSingleMessageAsync(MqttMessageBuffer buffer, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrEmpty(buffer.Payload)) return;

            var doc = JsonDocument.Parse(buffer.Payload);
            var allProperties = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in doc.RootElement.EnumerateObject())
                allProperties[prop.Name] = prop.Value.ToString();

            // Apply column mapping
            var mappedProperties = ApplyColumnMapping(allProperties);

            // Pisahkan data berdasarkan tujuan tabel
            var kwhData = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var relayData = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in mappedProperties)
            {
                if (SystemColumns.Contains(kvp.Key)) continue;

                if (KWHDataColumns.Contains(kvp.Key))
                    kwhData[kvp.Key] = kvp.Value;
                else if (RelayControlColumns.Contains(kvp.Key))
                    relayData[kvp.Key] = kvp.Value;
            }

            // Extract device info
            var terminalTime = mappedProperties.TryGetValue("_terminalTime", out var tt) ? tt : null;
            var groupName = mappedProperties.TryGetValue("_groupName", out var gn) ? gn : null;
            var deviceId = ExtractDeviceId(buffer.Topic, groupName);
            var deviceKey = await GetOrCreateDeviceKeyAsync(deviceId, groupName, ct);

            bool hasKwhData = kwhData.Count > 0;
            bool hasRelayData = relayData.Count > 0;
            bool saved = false;

            if (hasKwhData && ShouldSaveKwhData(deviceKey))
                saved = await SaveKWHDataAsync(kwhData, terminalTime, groupName, deviceId, deviceKey, ct);

            if (hasRelayData)
            {
                var filteredRelayData = ApplyRelaySampling(deviceKey, relayData);
                if (filteredRelayData.Count > 0)
                {
                    saved = await SaveRelayControlAsync(filteredRelayData, terminalTime, groupName, deviceId, deviceKey, ct) || saved;
                    Interlocked.Increment(ref _relayMessageCount);
                }
            }

            if (!hasKwhData && !hasRelayData)
            {
                _logger.LogWarning("[!] Tidak ada data yang dikenali dari topic: {Topic}", buffer.Topic);
                await LogFailedMessage(buffer, "No recognized data fields", ct);
                return;
            }

            Interlocked.Increment(ref _successCount);
            Interlocked.Increment(ref _messageCount);

            if (saved)
                LogStats("save");
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _errorCount);
            Interlocked.Increment(ref _messageCount);
            _logger.LogError(ex, $"[✗] Error processing: {buffer.Topic}");
            await LogFailedMessage(buffer, ex.Message, ct);
        }
    }

    /// <summary>
    /// Menentukan apakah data KWH untuk deviceKey boleh disimpan berdasarkan
    /// interval sampling yang dikonfigurasi. Jika interval = 0, semua data disimpan.
    /// </summary>
    private bool ShouldSaveKwhData(string deviceKey)
    {
        var intervalSeconds = _config.Sampling.IntervalSeconds;
        if (intervalSeconds <= 0) return true;

        var now = DateTime.Now;
        var interval = TimeSpan.FromSeconds(intervalSeconds);

        if (_lastKwhSaveByDevice.TryGetValue(deviceKey, out var lastSaved))
        {
            if (now - lastSaved < interval) return false;
        }

        _lastKwhSaveByDevice[deviceKey] = now;
        return true;
    }

    /// <summary>
    /// Menerapkan aturan sampling pada data relay:
    /// - RC selalu disimpan.
    /// - RCI hanya disimpan jika nilainya berubah dari nilai terakhir per perangkat.
    /// </summary>
    private Dictionary<string, string?> ApplyRelaySampling(string deviceKey, Dictionary<string, string?> relayData)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (relayData.TryGetValue("RC", out var rc))
        {
            result["RC"] = rc;
        }

        if (relayData.TryGetValue("RCI", out var rci))
        {
            var lastRci = _lastRciValueByDevice.TryGetValue(deviceKey, out var last) ? last : null;
            if (!string.Equals(rci, lastRci, StringComparison.OrdinalIgnoreCase))
            {
                result["RCI"] = rci;
                _lastRciValueByDevice[deviceKey] = rci;
            }
        }

        return result;
    }

    private Dictionary<string, string?> ApplyColumnMapping(Dictionary<string, string?> properties)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in properties)
        {
            var keyUpper = kvp.Key.ToUpper();

            if (SystemColumns.Contains(kvp.Key))
            {
                result[kvp.Key] = kvp.Value;
                continue;
            }

            if (_columnMapping.TryGetValue(keyUpper, out var mappedName))
                result[mappedName] = kvp.Value;
            else
                result[kvp.Key] = kvp.Value;
        }

        return result;
    }

    private static string ExtractDeviceId(string topic, string? groupName)
    {
        if (!string.IsNullOrEmpty(groupName) && groupName.ToUpper().Contains("KWHMETER"))
            return $"kwhapp{groupName.ToUpper().Replace("KWHMETER", "")}";

        var parts = topic.Split('/');
        return parts.Length >= 3 ? parts[2] : "unknown";
    }

    private async Task<string> GetOrCreateDeviceKeyAsync(string deviceId, string? groupName, CancellationToken ct)
    {
        if (_deviceKeyCache.TryGetValue(deviceId, out var cached)) return cached;
        var deviceKey = deviceId.ToUpper();

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var checkCmd = new SqlCommand("SELECT DeviceKey FROM DeviceRegistry WHERE DeviceId = @DeviceId", conn);
        checkCmd.Parameters.AddWithValue("@DeviceId", deviceId);
        var existing = await checkCmd.ExecuteScalarAsync(ct);

        if (existing != null)
        {
            deviceKey = existing.ToString()!;
            using var upd = new SqlCommand("UPDATE DeviceRegistry SET LastSeen = GETDATE(), GroupName = ISNULL(@GroupName, GroupName), UpdatedAt = GETDATE() WHERE DeviceId = @DeviceId", conn);
            upd.Parameters.AddWithValue("@DeviceId", deviceId);
            upd.Parameters.AddWithValue("@GroupName", (object?)groupName ?? DBNull.Value);
            await upd.ExecuteNonQueryAsync(ct);
        }
        else
        {
            using var ins = new SqlCommand("INSERT INTO DeviceRegistry (DeviceKey, DeviceId, GroupName, FirstSeen, LastSeen) VALUES (@DeviceKey, @DeviceId, @GroupName, GETDATE(), GETDATE())", conn);
            ins.Parameters.AddWithValue("@DeviceKey", deviceKey);
            ins.Parameters.AddWithValue("@DeviceId", deviceId);
            ins.Parameters.AddWithValue("@GroupName", (object?)groupName ?? DBNull.Value);
            await ins.ExecuteNonQueryAsync(ct);
            _logger.LogInformation($"[+] Registered: {deviceId} -> {deviceKey}");
        }
        _deviceKeyCache.TryAdd(deviceId, deviceKey);
        return deviceKey;
    }
    #endregion

    #region Database Save Operations
    private async Task<bool> SaveKWHDataAsync(Dictionary<string, string?> data, string? terminalTime,
        string? groupName, string deviceId, string deviceKey, CancellationToken ct)
    {
        for (int retry = 0; retry < 3; retry++)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync(ct);

                var columns = new List<string> { "DeviceKey", "TerminalTime", "GroupName", "DeviceId" };
                var values = new List<string> { "@DeviceKey", "@TerminalTime", "@GroupName", "@DeviceId" };

                using var cmd = new SqlCommand("", conn);
                cmd.Parameters.AddWithValue("@DeviceKey", deviceKey);
                cmd.Parameters.AddWithValue("@TerminalTime", ParseDateTime(terminalTime));
                cmd.Parameters.AddWithValue("@GroupName", (object?)groupName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DeviceId", deviceId);

                foreach (var col in KWHDataColumns)
                {
                    if (data.TryGetValue(col, out var value))
                    {
                        columns.Add($"[{col}]");
                        values.Add($"@{col}");
                        cmd.Parameters.AddWithValue($"@{col}", ParseValueWithScale(col, value));
                    }
                }

                cmd.CommandText = $"INSERT INTO KWHData ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)})";
                await cmd.ExecuteNonQueryAsync(ct);

                using var upd = new SqlCommand("UPDATE DeviceRegistry SET MessageCount = MessageCount + 1, LastSeen = GETDATE(), UpdatedAt = GETDATE() WHERE DeviceKey = @DeviceKey", conn);
                upd.Parameters.AddWithValue("@DeviceKey", deviceKey);
                await upd.ExecuteNonQueryAsync(ct);

                _sqlHealthy = true;
                return true;
            }
            catch when (retry < 2) { await Task.Delay(500 * (retry + 1), ct); }
        }
        _sqlHealthy = false;
        return false;
    }

    private async Task<bool> SaveRelayControlAsync(Dictionary<string, string?> data, string? terminalTime,
        string? groupName, string deviceId, string deviceKey, CancellationToken ct)
    {
        for (int retry = 0; retry < 3; retry++)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync(ct);

                // Ambil data relay terakhir perangkat ini untuk menjaga nilai RC/RCI terakhir
                var mergedData = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                using (var selectCmd = new SqlCommand("SELECT TOP 1 RC, RCI FROM RelayControl WHERE DeviceKey = @DeviceKey ORDER BY ReceivedTime DESC", conn))
                {
                    selectCmd.Parameters.AddWithValue("@DeviceKey", deviceKey);
                    using var reader = await selectCmd.ExecuteReaderAsync(ct);
                    if (await reader.ReadAsync(ct))
                    {
                        if (!reader.IsDBNull(0)) mergedData["RC"] = reader.GetInt32(0).ToString();
                        if (!reader.IsDBNull(1)) mergedData["RCI"] = reader.GetInt32(1).ToString();
                    }
                }

                // Hapus data lama perangkat ini
                using (var deleteCmd = new SqlCommand("DELETE FROM RelayControl WHERE DeviceKey = @DeviceKey", conn))
                {
                    deleteCmd.Parameters.AddWithValue("@DeviceKey", deviceKey);
                    await deleteCmd.ExecuteNonQueryAsync(ct);
                }

                // Gabungkan data terakhir dengan data baru (data baru menimpa)
                foreach (var kvp in data)
                    mergedData[kvp.Key] = kvp.Value;

                var columns = new List<string> { "DeviceKey", "TerminalTime", "GroupName", "DeviceId" };
                var values = new List<string> { "@DeviceKey", "@TerminalTime", "@GroupName", "@DeviceId" };

                using var cmd = new SqlCommand("", conn);
                cmd.Parameters.AddWithValue("@DeviceKey", deviceKey);
                cmd.Parameters.AddWithValue("@TerminalTime", ParseDateTime(terminalTime));
                cmd.Parameters.AddWithValue("@GroupName", (object?)groupName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DeviceId", deviceId);

                foreach (var col in RelayControlColumns)
                {
                    if (mergedData.TryGetValue(col, out var value) && !string.IsNullOrEmpty(value))
                    {
                        columns.Add($"[{col}]");
                        values.Add($"@{col}");
                        cmd.Parameters.AddWithValue($"@{col}", ParseRelayValue(value));
                    }
                }

                cmd.CommandText = $"INSERT INTO RelayControl ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)})";
                await cmd.ExecuteNonQueryAsync(ct);

                _sqlHealthy = true;
                return true;
            }
            catch when (retry < 2) { await Task.Delay(500 * (retry + 1), ct); }
        }
        _sqlHealthy = false;
        return false;
    }

    private static decimal ParseValueWithScale(string col, string? val)
    {
        if (string.IsNullOrEmpty(val) || !decimal.TryParse(val, out var raw)) return 0;
        var c = col.ToUpper();

        // VOLTAGE - PHASE_R, PHASE_S, PHASE_T (Dynamic Scaling)
        if (c is "PHASE_R" or "PHASE_S" or "PHASE_T")
        {
            decimal result;
            if (raw >= 10000m) result = raw / 100m;
            else if (raw >= 1000m) result = raw / 10m;
            else if (raw > 500m) result = raw / 10m;
            else result = raw;
            return Math.Round(result, 2);
        }

        // CURRENT
        if (c is "AMPERE_R" or "AMPERE_S" or "AMPERE_T")
            return Math.Round(raw / 10m, 2);

        // POWER FACTOR
        if (c == "COSPHI")
            return Math.Round(raw / 10m, 3);

        // ACTIVE POWER
        if (c == "W")
            return Math.Round(raw / 10m, 1);

        // ENERGY
        if (c is "AKTIF_POWER" or "TOTALW")
            return Math.Round(raw / 100m, 2);

        // FREQUENCY
        if (c == "F")
            return Math.Round(raw / 10m, 2);

        return raw;
    }

    private static object ParseRelayValue(string? val)
    {
        if (string.IsNullOrEmpty(val)) return DBNull.Value;

        if (int.TryParse(val, out var intVal))
            return intVal;

        if (decimal.TryParse(val, out var decVal))
            return (int)decVal;

        return val;
    }

    private static DateTime ParseDateTime(string? str)
    {
        if (string.IsNullOrEmpty(str)) return DateTime.Now;
        if (DateTime.TryParseExact(str, "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.None, out var r1)) return r1;
        if (DateTime.TryParse(str, out var r2)) return r2;
        return DateTime.Now;
    }

    private async Task LogFailedMessage(MqttMessageBuffer buffer, string reason, CancellationToken ct)
    {
        try
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            using var cmd = new SqlCommand("INSERT INTO FailedMessages (Topic, Payload, Reason, ReceivedAt) VALUES (@Topic, @Payload, @Reason, @ReceivedAt)", conn);
            cmd.Parameters.AddWithValue("@Topic", buffer.Topic);
            cmd.Parameters.AddWithValue("@Payload", buffer.Payload);
            cmd.Parameters.AddWithValue("@Reason", reason);
            cmd.Parameters.AddWithValue("@ReceivedAt", buffer.ReceivedAt);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch { }
    }
    #endregion

    // ============================================================
    // SECURITY HELPERS
    // ============================================================
    private static bool IsValidSqlIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        // SQL identifier: huruf/underscore pertama, kemudian huruf/angka/underscore/dollar
        return Regex.IsMatch(value, @"^[a-zA-Z_][a-zA-Z0-9_\$]{0,127}$");
    }

    private static string EscapeSqlStringLiteral(string? value)
    {
        // Escape single quote untuk SQL string literal
        return value?.Replace("'", "''") ?? "";
    }

    #region Embedded Migration Script - SESUAI STRUKTUR FINAL
    private const string MigrationScript = @"
USE [master]
GO
CREATE DATABASE [HaiwellElectrical]
GO
ALTER DATABASE [HaiwellElectrical] SET COMPATIBILITY_LEVEL = 170
GO
IF (1 = FULLTEXTSERVICEPROPERTY('IsFullTextInstalled'))
begin
EXEC [HaiwellElectrical].[dbo].[sp_fulltext_database] @action = 'enable'
end
GO
ALTER DATABASE [HaiwellElectrical] SET ANSI_NULL_DEFAULT OFF 
GO
ALTER DATABASE [HaiwellElectrical] SET ANSI_NULLS OFF 
GO
ALTER DATABASE [HaiwellElectrical] SET ANSI_PADDING OFF 
GO
ALTER DATABASE [HaiwellElectrical] SET ANSI_WARNINGS OFF 
GO
ALTER DATABASE [HaiwellElectrical] SET ARITHABORT OFF 
GO
ALTER DATABASE [HaiwellElectrical] SET AUTO_CLOSE ON 
GO
ALTER DATABASE [HaiwellElectrical] SET AUTO_SHRINK OFF 
GO
ALTER DATABASE [HaiwellElectrical] SET AUTO_UPDATE_STATISTICS ON 
GO
ALTER DATABASE [HaiwellElectrical] SET CURSOR_CLOSE_ON_COMMIT OFF 
GO
ALTER DATABASE [HaiwellElectrical] SET CURSOR_DEFAULT  GLOBAL 
GO
ALTER DATABASE [HaiwellElectrical] SET CONCAT_NULL_YIELDS_NULL OFF 
GO
ALTER DATABASE [HaiwellElectrical] SET NUMERIC_ROUNDABORT OFF 
GO
ALTER DATABASE [HaiwellElectrical] SET QUOTED_IDENTIFIER OFF 
GO
ALTER DATABASE [HaiwellElectrical] SET RECURSIVE_TRIGGERS OFF 
GO
ALTER DATABASE [HaiwellElectrical] SET  DISABLE_BROKER 
GO
ALTER DATABASE [HaiwellElectrical] SET AUTO_UPDATE_STATISTICS_ASYNC OFF 
GO
ALTER DATABASE [HaiwellElectrical] SET DATE_CORRELATION_OPTIMIZATION OFF 
GO
ALTER DATABASE [HaiwellElectrical] SET TRUSTWORTHY OFF 
GO
ALTER DATABASE [HaiwellElectrical] SET ALLOW_SNAPSHOT_ISOLATION OFF 
GO
ALTER DATABASE [HaiwellElectrical] SET PARAMETERIZATION SIMPLE 
GO
ALTER DATABASE [HaiwellElectrical] SET READ_COMMITTED_SNAPSHOT OFF 
GO
ALTER DATABASE [HaiwellElectrical] SET HONOR_BROKER_PRIORITY OFF 
GO
ALTER DATABASE [HaiwellElectrical] SET RECOVERY SIMPLE 
GO
ALTER DATABASE [HaiwellElectrical] SET  MULTI_USER 
GO
ALTER DATABASE [HaiwellElectrical] SET PAGE_VERIFY CHECKSUM 
GO
ALTER DATABASE [HaiwellElectrical] SET DB_CHAINING OFF 
GO
ALTER DATABASE [HaiwellElectrical] SET FILESTREAM( NON_TRANSACTED_ACCESS = OFF ) 
GO
ALTER DATABASE [HaiwellElectrical] SET TARGET_RECOVERY_TIME = 60 SECONDS 
GO
ALTER DATABASE [HaiwellElectrical] SET DELAYED_DURABILITY = DISABLED 
GO
ALTER DATABASE [HaiwellElectrical] SET OPTIMIZED_LOCKING = OFF 
GO
ALTER DATABASE [HaiwellElectrical] SET ACCELERATED_DATABASE_RECOVERY = OFF 
GO
ALTER DATABASE [HaiwellElectrical] SET QUERY_STORE = ON 
GO
ALTER DATABASE [HaiwellElectrical] SET QUERY_STORE (OPERATION_MODE = READ_WRITE, CLEANUP_POLICY = (STALE_QUERY_THRESHOLD_DAYS = 30), DATA_FLUSH_INTERVAL_SECONDS = 900, INTERVAL_LENGTH_MINUTES = 60, MAX_STORAGE_SIZE_MB = 1000, QUERY_CAPTURE_MODE = AUTO, SIZE_BASED_CLEANUP_MODE = AUTO, MAX_PLANS_PER_QUERY = 200, WAIT_STATS_CAPTURE_MODE = ON)
GO
USE [HaiwellElectrical]
GO
CREATE USER [kwhapp] WITHOUT LOGIN WITH DEFAULT_SCHEMA=[dbo]
GO
ALTER ROLE [db_ddladmin] ADD MEMBER [kwhapp]
GO
ALTER ROLE [db_datareader] ADD MEMBER [kwhapp]
GO
ALTER ROLE [db_datawriter] ADD MEMBER [kwhapp]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DeviceRegistry](
[Id] [int] IDENTITY(1,1) NOT NULL,
[DeviceKey] [varchar](20) NOT NULL,
[DeviceId] [varchar](50) NOT NULL,
[GroupName] [varchar](100) NULL,
[Location] [varchar](200) NULL,
[FirstSeen] [datetime2](7) NOT NULL,
[LastSeen] [datetime2](7) NOT NULL,
[IsActive] [bit] NOT NULL,
[MessageCount] [bigint] NOT NULL,
[CreatedAt] [datetime2](7) NOT NULL,
[UpdatedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED ([Id] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED ([DeviceKey] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED ([DeviceId] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[KWHData](
[Id] [bigint] IDENTITY(1,1) NOT NULL,
[DeviceKey] [varchar](20) NOT NULL,
[TerminalTime] [datetime2](7) NULL,
[ReceivedTime] [datetime2](7) NOT NULL,
[GroupName] [nvarchar](100) NULL,
[DeviceId] [nvarchar](50) NULL,
[PHASE_R] [decimal](18, 2) NULL,
[PHASE_S] [decimal](18, 2) NULL,
[PHASE_T] [decimal](18, 2) NULL,
[AMPERE_R] [decimal](18, 3) NULL,
[AMPERE_S] [decimal](18, 3) NULL,
[AMPERE_T] [decimal](18, 3) NULL,
[CosPhi] [decimal](18, 3) NULL,
[W] [decimal](18, 1) NULL,
[Aktif_Power] [decimal](18, 2) NULL,
[TotalW] [decimal](18, 2) NULL,
[F] [decimal](18, 2) NULL,
PRIMARY KEY CLUSTERED ([Id] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[RelayControl](
[Id] [bigint] IDENTITY(1,1) NOT NULL,
[DeviceKey] [varchar](20) NOT NULL,
[TerminalTime] [datetime2](7) NULL,
[ReceivedTime] [datetime2](7) NOT NULL,
[GroupName] [nvarchar](100) NULL,
[DeviceId] [nvarchar](50) NULL,
[RC] [int] NULL,
[RCI] [int] NULL,
PRIMARY KEY CLUSTERED ([Id] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE VIEW [dbo].[vLatestKWHData]
AS
SELECT
k.Id, k.DeviceKey, d.DeviceId, d.GroupName,
k.TerminalTime, k.ReceivedTime,
k.PHASE_R, k.PHASE_S, k.PHASE_T,
k.AMPERE_R, k.AMPERE_S, k.AMPERE_T,
k.CosPhi, k.W, k.Aktif_Power, k.TotalW, k.F
FROM KWHData k
INNER JOIN DeviceRegistry d ON k.DeviceKey = d.DeviceKey
INNER JOIN (
SELECT DeviceKey, MAX(ReceivedTime) AS MaxTime
FROM KWHData GROUP BY DeviceKey
) latest ON k.DeviceKey = latest.DeviceKey AND k.ReceivedTime = latest.MaxTime;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE VIEW [dbo].[vDeviceSummary]
AS
SELECT
d.DeviceKey, d.DeviceId, d.GroupName,
d.FirstSeen, d.LastSeen, d.IsActive, d.MessageCount,
COUNT(k.Id) AS TotalRecords,
MAX(k.ReceivedTime) AS LastDataReceived
FROM DeviceRegistry d
LEFT JOIN KWHData k ON d.DeviceKey = k.DeviceKey
GROUP BY d.DeviceKey, d.DeviceId, d.GroupName,
d.FirstSeen, d.LastSeen, d.IsActive, d.MessageCount;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE VIEW [dbo].[vDailyEnergy]
AS
SELECT
k.DeviceKey, d.GroupName,
CAST(k.TerminalTime AS DATE) AS ReportDate,
MIN(k.TotalW) AS EnergyStart_kWh,
MAX(k.TotalW) AS EnergyEnd_kWh,
(MAX(k.TotalW) - MIN(k.TotalW)) AS DailyConsumption_kWh,
COUNT(*) AS ReadingCount
FROM KWHData k
INNER JOIN DeviceRegistry d ON k.DeviceKey = d.DeviceKey
GROUP BY k.DeviceKey, d.GroupName, CAST(k.TerminalTime AS DATE);
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE VIEW [dbo].[vLatestRelayControl]
AS
SELECT
r.Id, r.DeviceKey, d.DeviceId, d.GroupName,
r.TerminalTime, r.ReceivedTime, r.RC
FROM RelayControl r
INNER JOIN DeviceRegistry d ON r.DeviceKey = d.DeviceKey
INNER JOIN (
SELECT DeviceKey, MAX(ReceivedTime) AS MaxTime
FROM RelayControl GROUP BY DeviceKey
) latest ON r.DeviceKey = latest.DeviceKey AND r.ReceivedTime = latest.MaxTime;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[__EFMigrationsHistory](
[MigrationId] [nvarchar](150) NOT NULL,
[ProductVersion] [nvarchar](32) NOT NULL,
CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY CLUSTERED ([MigrationId] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AnomalyLogs](
[Id] [bigint] IDENTITY(1,1) NOT NULL,
[DeviceKey] [nvarchar](50) NOT NULL,
[DeviceId] [nvarchar](50) NULL,
[AnomalyType] [nvarchar](20) NOT NULL,
[PowerValue] [decimal](18, 2) NOT NULL,
[ThresholdValue] [decimal](18, 2) NOT NULL,
[Deviation] [decimal](5, 2) NOT NULL,
[DetectedTime] [datetime2](7) NOT NULL,
[EMAValue] [decimal](18, 2) NULL,
[ThresholdMode] [nvarchar](20) NULL,
[Acknowledged] [bit] NULL,
[AcknowledgedTime] [datetime2](7) NULL,
[Notes] [nvarchar](500) NULL,
PRIMARY KEY CLUSTERED ([Id] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AppLog](
[Id] [bigint] IDENTITY(1,1) NOT NULL,
[LogLevel] [varchar](20) NOT NULL,
[Message] [nvarchar](max) NOT NULL,
[Topic] [varchar](200) NULL,
[DeviceKey] [varchar](20) NULL,
[CreatedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED ([Id] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AppSettings](
[Id] [int] IDENTITY(1,1) NOT NULL,
[SettingKey] [nvarchar](100) NOT NULL,
[SettingValue] [nvarchar](500) NOT NULL,
[UpdatedAt] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED ([Id] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED ([SettingKey] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ColumnMapping](
[Id] [int] IDENTITY(1,1) NOT NULL,
[OldColumnName] [varchar](50) NOT NULL,
[NewColumnName] [varchar](50) NOT NULL,
[IsActive] [bit] NOT NULL,
[CreatedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED ([Id] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ColumnScaleConfig](
[ColumnName] [varchar](50) NOT NULL,
[ScaleFactor] [decimal](18, 5) NOT NULL,
[RegisterAddress] [varchar](10) NULL,
[DataType] [varchar](20) NOT NULL,
[Unit] [varchar](50) NULL,
[Category] [varchar](50) NULL,
[Description] [varchar](500) NULL,
[IsDynamic] [bit] NOT NULL,
[LastUpdated] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED ([ColumnName] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DailyEnergy](
[Id] [bigint] IDENTITY(1,1) NOT NULL,
[DeviceKey] [nvarchar](100) NOT NULL,
[Date] [date] NOT NULL,
[EnergyKWh] [decimal](18, 4) NOT NULL,
[CalculatedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED ([Id] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[FailedMessages](
[Id] [bigint] IDENTITY(1,1) NOT NULL,
[Topic] [varchar](500) NOT NULL,
[Payload] [nvarchar](max) NOT NULL,
[Reason] [nvarchar](500) NULL,
[RetryCount] [int] NOT NULL,
[IsResolved] [bit] NOT NULL,
[ReceivedAt] [datetime2](7) NOT NULL,
[ResolvedAt] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED ([Id] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[HourlyEnergy](
[Id] [bigint] IDENTITY(1,1) NOT NULL,
[DeviceKey] [nvarchar](100) NOT NULL,
[Hour] [datetime2](7) NOT NULL,
[EnergyKWh] [decimal](18, 4) NOT NULL,
[CalculatedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED ([Id] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[KWHData_History](
[HistoryId] [bigint] IDENTITY(1,1) NOT NULL,
[OriginalId] [bigint] NOT NULL,
[DeviceKey] [varchar](20) NOT NULL,
[TerminalTime] [datetime2](7) NULL,
[ReceivedTime] [datetime2](7) NOT NULL,
[GroupName] [nvarchar](100) NULL,
[DeviceId] [nvarchar](50) NULL,
[PHASE_R] [decimal](18, 2) NULL,
[PHASE_S] [decimal](18, 2) NULL,
[PHASE_T] [decimal](18, 2) NULL,
[AMPERE_R] [decimal](18, 3) NULL,
[AMPERE_S] [decimal](18, 3) NULL,
[AMPERE_T] [decimal](18, 3) NULL,
[CosPhi] [decimal](18, 3) NULL,
[W] [decimal](18, 1) NULL,
[Aktif_Power] [decimal](18, 2) NULL,
[TotalW] [decimal](18, 2) NULL,
[F] [decimal](18, 2) NULL,
[ArchivedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED ([HistoryId] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MonthlyEnergy](
[Id] [bigint] IDENTITY(1,1) NOT NULL,
[DeviceKey] [nvarchar](100) NOT NULL,
[Year] [int] NOT NULL,
[Month] [int] NOT NULL,
[EnergyKWh] [decimal](18, 4) NOT NULL,
[CalculatedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED ([Id] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[YearlyEnergy](
[Id] [bigint] IDENTITY(1,1) NOT NULL,
[DeviceKey] [nvarchar](100) NOT NULL,
[Year] [int] NOT NULL,
[EnergyKWh] [decimal](18, 4) NOT NULL,
[CalculatedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED ([Id] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_AnomalyLogs_DetectedTime] ON [dbo].[AnomalyLogs]([DetectedTime] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
CREATE NONCLUSTERED INDEX [IX_AnomalyLogs_DeviceKey] ON [dbo].[AnomalyLogs]([DeviceKey] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_AppLog_CreatedAt] ON [dbo].[AppLog]([CreatedAt] DESC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
CREATE NONCLUSTERED INDEX [IX_AppLog_Level] ON [dbo].[AppLog]([LogLevel] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_ColumnMapping_OldName] ON [dbo].[ColumnMapping]([OldColumnName] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
CREATE NONCLUSTERED INDEX [IX_DeviceRegistry_DeviceId] ON [dbo].[DeviceRegistry]([DeviceId] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
CREATE NONCLUSTERED INDEX [IX_DeviceRegistry_DeviceKey] ON [dbo].[DeviceRegistry]([DeviceKey] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_FailedMessages_IsResolved] ON [dbo].[FailedMessages]([IsResolved] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_FailedMessages_ReceivedAt] ON [dbo].[FailedMessages]([ReceivedAt] DESC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
CREATE NONCLUSTERED INDEX [IX_KWHData_DeviceKey] ON [dbo].[KWHData]([DeviceKey] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
CREATE NONCLUSTERED INDEX [IX_KWHData_DeviceKey_ReceivedTime] ON [dbo].[KWHData]([DeviceKey] ASC,[ReceivedTime] DESC)
INCLUDE([DeviceId],[GroupName],[TerminalTime],[PHASE_R],[PHASE_S],[PHASE_T],[AMPERE_R],[AMPERE_S],[AMPERE_T],[CosPhi],[W],[Aktif_Power],[TotalW],[F]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
CREATE NONCLUSTERED INDEX [IX_KWHData_DeviceKey_TerminalTime] ON [dbo].[KWHData]([DeviceKey] ASC,[TerminalTime] DESC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_KWHData_ReceivedTime] ON [dbo].[KWHData]([ReceivedTime] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
CREATE NONCLUSTERED INDEX [IX_KWHData_ReceivedTime_DeviceKey] ON [dbo].[KWHData]([ReceivedTime] DESC,[DeviceKey] ASC)
INCLUDE([DeviceId],[GroupName],[TerminalTime],[PHASE_R],[PHASE_S],[PHASE_T],[AMPERE_R],[AMPERE_S],[AMPERE_T],[CosPhi],[W],[Aktif_Power],[TotalW],[F]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_KWHData_TerminalTime] ON [dbo].[KWHData]([TerminalTime] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
CREATE NONCLUSTERED INDEX [IX_RelayControl_DeviceKey] ON [dbo].[RelayControl]([DeviceKey] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_RelayControl_ReceivedTime] ON [dbo].[RelayControl]([ReceivedTime] DESC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
CREATE NONCLUSTERED INDEX [IX_RelayControl_DeviceKey_ReceivedTime] ON [dbo].[RelayControl]([DeviceKey] ASC,[ReceivedTime] DESC)
INCLUDE([DeviceId],[GroupName],[TerminalTime],[RC]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_KWHData_History_ArchivedAt] ON [dbo].[KWHData_History]([ArchivedAt] DESC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
CREATE NONCLUSTERED INDEX [IX_KWHData_History_DeviceKey] ON [dbo].[KWHData_History]([DeviceKey] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[AnomalyLogs] ADD  DEFAULT (getdate()) FOR [DetectedTime]
GO
ALTER TABLE [dbo].[AnomalyLogs] ADD  DEFAULT ('manual') FOR [ThresholdMode]
GO
ALTER TABLE [dbo].[AnomalyLogs] ADD  DEFAULT ((0)) FOR [Acknowledged]
GO
ALTER TABLE [dbo].[AppLog] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[AppSettings] ADD  DEFAULT (getdate()) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[ColumnMapping] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[ColumnMapping] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[ColumnScaleConfig] ADD  DEFAULT ('DECIMAL(18,3)') FOR [DataType]
GO
ALTER TABLE [dbo].[ColumnScaleConfig] ADD  DEFAULT ((0)) FOR [IsDynamic]
GO
ALTER TABLE [dbo].[ColumnScaleConfig] ADD  DEFAULT (getdate()) FOR [LastUpdated]
GO
ALTER TABLE [dbo].[DailyEnergy] ADD  DEFAULT (getdate()) FOR [CalculatedAt]
GO
ALTER TABLE [dbo].[DeviceRegistry] ADD  DEFAULT (getdate()) FOR [FirstSeen]
GO
ALTER TABLE [dbo].[DeviceRegistry] ADD  DEFAULT (getdate()) FOR [LastSeen]
GO
ALTER TABLE [dbo].[DeviceRegistry] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[DeviceRegistry] ADD  DEFAULT ((0)) FOR [MessageCount]
GO
ALTER TABLE [dbo].[DeviceRegistry] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[DeviceRegistry] ADD  DEFAULT (getdate()) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[FailedMessages] ADD  DEFAULT ((0)) FOR [RetryCount]
GO
ALTER TABLE [dbo].[FailedMessages] ADD  DEFAULT ((0)) FOR [IsResolved]
GO
ALTER TABLE [dbo].[FailedMessages] ADD  DEFAULT (getdate()) FOR [ReceivedAt]
GO
ALTER TABLE [dbo].[HourlyEnergy] ADD  DEFAULT (getdate()) FOR [CalculatedAt]
GO
ALTER TABLE [dbo].[KWHData] ADD  DEFAULT (getdate()) FOR [ReceivedTime]
GO
ALTER TABLE [dbo].[RelayControl] ADD  DEFAULT (getdate()) FOR [ReceivedTime]
GO
ALTER TABLE [dbo].[KWHData_History] ADD  DEFAULT (getdate()) FOR [ArchivedAt]
GO
ALTER TABLE [dbo].[MonthlyEnergy] ADD  DEFAULT (getdate()) FOR [CalculatedAt]
GO
ALTER TABLE [dbo].[YearlyEnergy] ADD  DEFAULT (getdate()) FOR [CalculatedAt]
GO
ALTER TABLE [dbo].[KWHData]  WITH CHECK ADD  CONSTRAINT [FK_KWHData_DeviceRegistry] FOREIGN KEY([DeviceKey])
REFERENCES [dbo].[DeviceRegistry] ([DeviceKey])
GO
ALTER TABLE [dbo].[KWHData] CHECK CONSTRAINT [FK_KWHData_DeviceRegistry]
GO
ALTER TABLE [dbo].[RelayControl]  WITH CHECK ADD  CONSTRAINT [FK_RelayControl_DeviceRegistry] FOREIGN KEY([DeviceKey])
REFERENCES [dbo].[DeviceRegistry] ([DeviceKey])
GO
ALTER TABLE [dbo].[RelayControl] CHECK CONSTRAINT [FK_RelayControl_DeviceRegistry]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE PROCEDURE [dbo].[sp_CleanupOldData]
@DaysToKeep INT = 90
AS
BEGIN
SET NOCOUNT ON;
DELETE FROM KWHData WHERE ReceivedTime < DATEADD(DAY, -@DaysToKeep, GETDATE());
DELETE FROM RelayControl WHERE ReceivedTime < DATEADD(DAY, -@DaysToKeep, GETDATE());
DELETE FROM AppLog WHERE CreatedAt < DATEADD(DAY, -@DaysToKeep, GETDATE());
DELETE FROM FailedMessages WHERE ReceivedAt < DATEADD(DAY, -30, GETDATE()) AND IsResolved = 1;
END;
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE PROCEDURE [dbo].[sp_RegisterDevice]
@DeviceId VARCHAR(50),
@GroupName VARCHAR(100) = NULL,
@DeviceKey VARCHAR(20) OUTPUT
AS
BEGIN
SET NOCOUNT ON;
IF NOT EXISTS (SELECT 1 FROM DeviceRegistry WHERE DeviceId = @DeviceId)
BEGIN
DECLARE @NextNumber INT;
SELECT @NextNumber = ISNULL(MAX(CAST(SUBSTRING(DeviceKey, 5, 3) AS INT)), 0) + 1
FROM DeviceRegistry WHERE DeviceKey LIKE 'KWH-%';
SET @DeviceKey = 'KWH-' + RIGHT('000' + CAST(@NextNumber AS VARCHAR), 3);
INSERT INTO DeviceRegistry (DeviceKey, DeviceId, GroupName, FirstSeen, LastSeen)
VALUES (@DeviceKey, @DeviceId, @GroupName, GETDATE(), GETDATE());
END
ELSE
BEGIN
SELECT @DeviceKey = DeviceKey FROM DeviceRegistry WHERE DeviceId = @DeviceId;
UPDATE DeviceRegistry
SET LastSeen = GETDATE(), GroupName = ISNULL(@GroupName, GroupName), UpdatedAt = GETDATE()
WHERE DeviceId = @DeviceId;
END
END;
GO
USE [master]
GO
ALTER DATABASE [HaiwellElectrical] SET  READ_WRITE 
GO
";
    #endregion
}

public class DynamicMqttData
{
    public Dictionary<string, string?> Properties { get; set; } = new();
    public string? GetProperty(string name) => Properties.TryGetValue(name, out string? value) ? value : null;
}

public class MqttMessageBuffer
{
    public string Topic { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTime ReceivedAt { get; set; } = DateTime.Now;
}