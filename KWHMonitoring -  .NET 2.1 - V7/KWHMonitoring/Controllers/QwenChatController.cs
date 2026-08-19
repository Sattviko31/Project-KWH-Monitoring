using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using KWHMonitoring.Models;
using KWHMonitoring.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KWHMonitoring.Controllers
{
    [ApiController]
    [Route("api/qwenchat")]
    public class QwenChatController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<QwenChatController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly AesEncryptionService _encryption;
        private readonly IMemoryCache _cache;

        public QwenChatController(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ILogger<QwenChatController> logger,
            ApplicationDbContext context,
            AesEncryptionService encryption,
            IMemoryCache cache)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _context = context;
            _encryption = encryption;
            _cache = cache;
        }

        private class ChatbotConfig
        {
            public string ApiKey { get; set; }
            public string Model { get; set; }
            public string ApiUrl { get; set; }
        }

        private async Task<ChatbotConfig> GetChatbotConfigAsync()
        {
            const string cacheKey = "ChatbotConfig_Cached";

            if (_cache.TryGetValue(cacheKey, out ChatbotConfig cached))
                return cached;

            string apiKey = "";
            string modelName = "qwen-plus-2025-04-28";
            string apiUrl = "https://dashscope-intl.aliyuncs.com/compatible-mode/v1/chat/completions";

            try
            {
                var settings = await _context.AppSettingsRecords
                    .Where(x => x.SettingKey.StartsWith("Chatbot."))
                    .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);

                if (settings.TryGetValue("Chatbot.ApiKey", out var encryptedKey) && !string.IsNullOrEmpty(encryptedKey))
                {
                    var decrypted = _encryption.Decrypt(encryptedKey);
                    if (!string.IsNullOrEmpty(decrypted))
                        apiKey = decrypted;
                }

                if (settings.TryGetValue("Chatbot.Model", out var model))
                    modelName = model;

                if (settings.TryGetValue("Chatbot.ApiUrl", out var url))
                    apiUrl = url;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load chatbot config from DB, falling back to appsettings.json");
            }

            // Fallback to appsettings.json if DB has no config
            if (string.IsNullOrEmpty(apiKey))
                apiKey = _configuration["Qwen:ApiKey"] ?? "";

            if (modelName == "qwen-plus-2025-04-28" && !string.IsNullOrEmpty(_configuration["Qwen:Model"]))
                modelName = _configuration["Qwen:Model"];

            var config = new ChatbotConfig { ApiKey = apiKey, Model = modelName, ApiUrl = apiUrl };

            _cache.Set(cacheKey, config, TimeSpan.FromMinutes(5));

            return config;
        }

        public class ChatMessageDto
        {
            public string Role { get; set; }
            public string Content { get; set; }
        }

        public class ChatRequestDto
        {
            public string Message { get; set; }
            public List<ChatMessageDto> History { get; set; }
            // Fitur Baru: Untuk menerima data yang sedang tampil di layar website
            // Sekarang menerima JObject/JArray untuk struktur data yang rapi
            public JToken RealTimeData { get; set; }
        }

        private class PanelUsageData
        {
            public string DeviceKey { get; set; }
            public string GroupName { get; set; }
            public decimal TodayKWh { get; set; }
            public decimal TodayCost { get; set; }
            public decimal MonthKWh { get; set; }
            public decimal MonthCost { get; set; }
            public decimal YearKWh { get; set; }
            public decimal YearCost { get; set; }
            public decimal AllTimeKWh { get; set; }
            public decimal AllTimeCost { get; set; }
            public decimal CurrentPower { get; set; }
            public string Status { get; set; }
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] ChatRequestDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { success = false, message = "Pesan tidak boleh kosong." });
            }

            _logger.LogInformation($"=== MENERIMA PESAN CHAT: {request.Message} ===");

            // Log data realtime yang diterima - VALIDASI EKSPLOSIIF
            if (request.RealTimeData != null)
            {
                try
                {
                    var panelsCount = request.RealTimeData["panels"]?.Count() ?? 0;
                    var hasUsageStats = request.RealTimeData["usageStatistics"] != null;
                    var hasSystemStats = request.RealTimeData["systemStatistics"] != null;
                    var hasAnomalies = request.RealTimeData["anomalies"] != null;
                    var chartCount = request.RealTimeData["chartHistory"]?.Count() ?? 0;
                    var hasTariff = request.RealTimeData["tariff"] != null;
                    
                    // Tampilkan detail JSON untuk debugging
                    var rawData = JsonConvert.SerializeObject(request.RealTimeData, Formatting.Indented);
                    _logger.LogInformation($"=== DATA REALTIME YANG DITERIMA ===\n{rawData}\n===================================");

                    if (panelsCount > 0 && hasUsageStats && hasSystemStats && hasTariff)
                    {
                        _logger.LogInformation($"✅ Data realtime LENGKAP diterima: panels={panelsCount}, usageStats={hasUsageStats}, systemStats={hasSystemStats}, anomalies={hasAnomalies}, charts={chartCount}, tariff={hasTariff}");
                        
                        // Cek apakah ada data usage statistics yang valid
                        try
                        {
                            var usageStats = request.RealTimeData["usageStatistics"];
                            var perPanel = usageStats["perPanel"];
                            var rankings = perPanel["rankings"];
                            var rankingCount = rankings?.Count() ?? 0;
                            
                            if (rankingCount > 0)
                            {
                                _logger.LogInformation($"📊 Usage Statistics Rankings: {rankingCount} panel ditemukan");
                                
                                // Log top 3 panel paling boros
                                var top3 = rankings.Take(3);
                                int idx = 1;
                                foreach (var ranking in top3)
                                {
                                    try
                                    {
                                        var groupName = ranking["groupName"]?.ToString() ?? "Unknown";
                                        var todayKwh = ranking["today"]?["energyKWh"]?.ToString() ?? "0";
                                        var todayCost = ranking["today"]?["estimatedCost"]?.ToString() ?? "0";
                                        _logger.LogInformation($"   #{idx++}. {groupName}: {todayKwh} kWh (Rp {todayCost})");
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning($"Gagal membaca ranking panel #{idx}: {ex.Message}");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Gagal membaca usage statistics: {ex.Message}");
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"⚠️ Data realtime TIDAK LENGKAP! Status: panels={panelsCount}, usageStats={hasUsageStats}, systemStats={hasSystemStats}, anomalies={hasAnomalies}, charts={chartCount}, tariff={hasTariff}");
                        
                        // Log spesifik field yang hilang
                        if (panelsCount == 0)
                            _logger.LogWarning("❌ TIDAK ADA PANEL DATA!");
                        if (!hasUsageStats)
                            _logger.LogWarning("❌ USAGE STATISTICS TIDAK ADA!");
                        if (!hasSystemStats)
                            _logger.LogWarning("❌ SYSTEM STATISTICS TIDAK ADA!");
                        if (!hasTariff)
                            _logger.LogWarning("❌ TARIFF TIDAK ADA!");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Gagal memvalidasi data realtime yang diterima");
                }
            }
            else
            {
                _logger.LogError("❌ ERROR: Tidak ada data realtime yang diterima dari frontend! RealTimeData = NULL");
            }

            var chatbotConfig = await GetChatbotConfigAsync();
            string apiKey = chatbotConfig.ApiKey;
            string modelName = chatbotConfig.Model;
            string url = chatbotConfig.ApiUrl;

            if (string.IsNullOrEmpty(apiKey))
            {
                return StatusCode(500, new { success = false, message = "AI Chatbot API Key belum dikonfigurasi. Atur di Settings > AI Chatbot." });
            }

            try
            {
                var client = _httpClientFactory.CreateClient("QwenClient");
                var messages = new List<object>();

                // ==========================================
                // INSTRUKSI SISTEM & INJEKSI DATA REAL-TIME
                // ==========================================
                string systemPrompt = "Nama kamu Voltra, Kamu adalah asisten virtual pintar dan suka banget humor yang KHUSUS melayani sistem KWH Monitoring. " +
                                      "Fokus utamamu HANYA menjawab pertanyaan seputar data listrik, daya (Watt), Power Factor, tegangan, arus, konsumsi energi (WH/KWH), dan panduan penggunaan aplikasi. " +
                                      "ATURAN MUTLAK: Jika pengguna bertanya tentang topik di luar sistem monitoring listrik (seperti resep masakan, cuaca, berita umum, coding di luar konteks, dll), kamu WAJIB menolak dengan humor dan beritahu mereka bahwa kamu hanya diprogram untuk KWH Monitoring.";

                // Jika ada data realtime dari frontend, masukkan ke memori AI dalam format terstruktur
                if (request.RealTimeData != null && !request.RealTimeData.ToString().Equals("null", StringComparison.OrdinalIgnoreCase))
                {
                    string formattedData;
                    try
                    {
                        // Cek apakah data ini punya struktur lengkap (snapshot dari /api/qwenchat/current-dashboard-data)
                        var panelsToken = request.RealTimeData["panels"];
                        var tariffToken = request.RealTimeData["tariff"];
                        var statsToken = request.RealTimeData["systemStatistics"];
                        var usageToken = request.RealTimeData["usageStatistics"];
                        var anomaliesToken = request.RealTimeData["anomalies"];
                        bool isFullSnapshot = (panelsToken != null || tariffToken != null || usageToken != null);

                        if (isFullSnapshot)
                        {
                            // Format khusus untuk snapshot utuh — bagi per seksi agar AI mudah membaca
                            var sb = new StringBuilder();
                            sb.AppendLine("{");

                            // Header metadata
                            var ts = request.RealTimeData["timestamp"];
                            if (ts != null)
                                sb.AppendLine("  \"metadata\": { \"timestamp\": \"" + ts.ToString() + "\" },");

                            // Tariff (PENTING UNTUK TAGIHAN)
                            if (tariffToken != null)
                            {
                                sb.AppendLine("  \"tariff\": " + tariffToken.ToString());
                            }

                            // System Statistics (real-time monitoring)
                            if (statsToken != null)
                            {
                                sb.AppendLine("  \"statistikSistemRealtime\": " + statsToken.ToString());
                            }

                            // Usage Statistics (untuk perhitungan tagihan — PALING AKURAT dengan perPanel.rankings)
                            if (usageToken != null)
                            {
                                var perPanel = usageToken["perPanel"];
                                if (perPanel != null && perPanel["rankings"] != null)
                                {
                                    sb.AppendLine("  \"usageStatistics\": {");
                                    sb.AppendLine("    \"global\": " + (usageToken["global"]?.ToString() ?? "{}") + ",");
                                    sb.AppendLine("    \"perPanel\": {");
                                    sb.AppendLine("      \"period\": \"" + (perPanel["period"]?.ToString() ?? "") + "\",");
                                    sb.AppendLine("      \"totalSystemKWh\": " + (perPanel["totalSystemKWh"]?.ToString() ?? "0") + ",");
                                    sb.AppendLine("      \"rankings\": [");
                                    
                                    var rankings = perPanel["rankings"] as JArray;
                                    if (rankings != null)
                                    {
                                        int rankCount = Math.Min(rankings.Count, 15); // Tampilkan top 15
                                        for (int i = 0; i < rankCount; i++)
                                        {
                                            var r = rankings[i];
                                            var today = r["today"];
                                            var thisMonth = r["thisMonth"];
                                            var thisYear = r["thisYear"];
                                            
                                            string line = "        { " +
                                                $"\"rank\": {i + 1}, " +
                                                $"\"deviceKey\": \"{r["deviceKey"]}\", " +
                                                $"\"groupName\": \"{r["groupName"]}\", " +
                                                $"\"todayKWh\": {(today?["energyKWh"]?.ToString() ?? "0")}, " +
                                                $"\"todayCost\": {(today?["estimatedCost"]?.ToString() ?? "0")}, " +
                                                $"\"monthKWh\": {(thisMonth?["energyKWh"]?.ToString() ?? "0")}, " +
                                                $"\"monthCost\": {(thisMonth?["estimatedCost"]?.ToString() ?? "0")}, " +
                                                $"\"yearKWh\": {(thisYear?["energyKWh"]?.ToString() ?? "0")}, " +
                                                $"\"yearCost\": {(thisYear?["estimatedCost"]?.ToString() ?? "0")} " +
                                                "}";
                                            if (i < rankCount - 1) line += ",";
                                            sb.AppendLine(line);
                                        }
                                    }
                                    
                                    sb.AppendLine("      ]");
                                    sb.AppendLine("    }");
                                    sb.AppendLine("  }");
                                }
                                else
                                {
                                    sb.AppendLine("  \"usageStatistics\": " + usageToken.ToString() + ",");
                                }
                            }

                            // Anomalies (deteksi masalah)
                            if (anomaliesToken != null)
                            {
                                sb.AppendLine("  \"anomaliDeteksi\": " + anomaliesToken.ToString());
                            }

                            // Panels (data per device - tegangan, arus, cosPhi, dll)
                            if (panelsToken != null)
                            {
                                sb.AppendLine("  \"panel\": [");
                                var panelArray = panelsToken as JArray;
                                int totalPanels = panelArray != null ? panelArray.Count : 0;
                                int panelCount = Math.Min(totalPanels, 20); // Batasi 20 panel pertama
                                for (int i = 0; i < panelCount; i++)
                                {
                                    var p = panelArray[i];
                                    string line = "    { ";

                                    var dk = p["deviceKey"];
                                    if (dk != null) line += "\"deviceKey\": \"" + dk.ToString() + "\"";

                                    var gn = p["groupName"];
                                    if (gn != null) line += ", \"groupName\": \"" + gn.ToString() + "\"";

                                    var dw = p["dayaWatt"];
                                    if (dw != null) line += ", \"dayaWatt\": " + dw.ToString();

                                    var cp = p["cosPhi"];
                                    if (cp != null) line += ", \"cosPhi\": " + cp.ToString();

                                    var vr = p["r"];
                                    if (vr != null) line += ", \"teganganR\": " + vr.ToString();

                                    var ar = p["ampR"];
                                    if (ar != null) line += ", \"arusR\": " + ar.ToString();

                                    var st = p["status"];
                                    if (st != null) line += ", \"status\": \"" + st.ToString() + "\"";

                                    line += " }";
                                    if (i < panelCount - 1) line += ",";
                                    sb.AppendLine(line);
                                }
                                sb.AppendLine("  ]");
                            }

                            // Chart History (time-series untuk trend analysis)
                            var chartToken = request.RealTimeData["chartHistory"];
                            if (chartToken != null)
                            {
                                var chartArray = chartToken as JArray;
                                if (chartArray != null && chartArray.Count > 0)
                                {
                                    sb.AppendLine("  \"chartHistory\": [");
                                    int chartCount = Math.Min(chartArray.Count, 10);
                                    for (int i = 0; i < chartCount; i++)
                                    {
                                        var c = chartArray[i];
                                        var dk = c["deviceKey"]?.ToString() ?? "";
                                        var gn = c["groupName"]?.ToString() ?? "";
                                        var pts = c["pointCount"]?.ToString() ?? "0";
                                        var powerArr = c["power"]?.ToString() ?? "[]";
                                        var labelsArr = c["labels"]?.ToString() ?? "[]";
                                        string line = "    { \"deviceKey\": \"" + dk + "\", \"groupName\": \"" + gn + "\", \"dataPoints\": " + pts + ", \"labels\": " + labelsArr + ", \"powerWatt\": " + powerArr + " }";
                                        if (i < chartCount - 1) line += ",";
                                        sb.AppendLine(line);
                                    }
                                    sb.AppendLine("  ]");
                                }
                            }

                            sb.AppendLine("}");
                            formattedData = sb.ToString();
                        }
                        else
                        {
                            // Fallback: format biasa untuk string atau data lain
                            formattedData = JsonConvert.SerializeObject(request.RealTimeData, Formatting.Indented);
                        }
                    }
                    catch
                    {
                        // Fallback terakhir: string mentah
                        formattedData = request.RealTimeData.ToString();
                    }

                    systemPrompt += "\n\n" +
                                    "═══════════════════════════════════════════════════════════════════════════════\n" +
                                    "📊 DATA REALTIME DASHBOARD KWH MONITORING\n" +
                                    "═══════════════════════════════════════════════════════════════════════════════\n" +
                                    "Kamu adalah Voltra, asisten AI KWH Monitoring. Data di bawah ini adalah snapshot LANGSUNG dari database sistem.\n" +
                                    "Kamu WAJIB menggunakan data ini untuk menjawab. JANGAN menebak, JANGAN menggunakan placeholder, JANGAN bilang 'kurang data'.\n\n" +
                                    "⚠️ ATURAN MUTLAK:\n" +
                                    "1. Jika data di bawah ini menunjukkan angka > 0, kamu HARUS menyebutkan angka tersebut.\n" +
                                    "2. Jika data = 0, katakan 'Data menunjukkan 0 kWh' atau 'Tidak ada konsumsi'.\n" +
                                    "3. Jangan pernah menjawab '[Nama_Panel]', '[X]', '[Z]', '[A]', '[B]', '[C]', '[D]'.\n" +
                                    "4. Selalu sebutkan unit: kWh untuk energi, Watt untuk daya, Rupiah untuk biaya, V untuk tegangan, A untuk arus.\n" +
                                    "5. Jika tidak tahu, katakan 'Berdasarkan data terakhir yang tersedia...' lalu sebutkan angka terakhir.\n\n" +
                                    "📋 SUMBER DATA BERDASARKAN PERTANYAAN:\n" +
                                    "- TAGIHAN / KONSUMSI: Gunakan usageStatistics.perPanel.rankings\n" +
                                    "  • Hari ini: today.energyKWh + today.estimatedCost\n" +
                                    "  • Bulan ini: thisMonth.energyKWh + thisMonth.estimatedCost\n" +
                                    "  • Tahun ini: thisYear.energyKWh + thisYear.estimatedCost\n" +
                                    "  • Total: usageStatistics.global.today/thisMonth/thisYear.totalKWh\n" +
                                    "- PANEL PALING BOROS: Urutan pertama di usageStatistics.perPanel.rankings\n" +
                                    "- DAYA TOTAL: systemStatistics.totalDaya (dalam Watt)\n" +
                                    "- PANEL AKTIF: systemStatistics.activePanels\n" +
                                    "- TEgangAN/ARUS/PF PER PANEL: array panels[]\n" +
                                    "- ANOMALI: anomalies.totalCount, anomalies.unacknowledged\n" +
                                    "- TREND: chartHistory[].power[] dan chartHistory[].labels[]\n" +
                                    "- TARIF: tariff.tariffPerKWh\n\n" +
                                    "✅ FORMAT JAWABAN WAJIB (pilih satu):\n" +
                                    "Jawab singkat, padat, dan pakai angka real. Tambahkan emoji yang relevan.\n\n" +
                                    "CONTOH 'berapa tagihan bulan ini?':\n" +
                                    "'Total konsumsi bulan ini adalah [angka] kWh × Rp [tarif]/kWh = Rp [biaya]. Panel terbesar: [Nama Panel] dengan [angka] kWh.'\n\n" +
                                    "CONTOH 'mana panel paling boros?':\n" +
                                    "'Panel paling boros adalah [Nama Panel] dengan [angka] kWh hari ini (Rp [biaya]). Untuk bulan ini [angka] kWh (Rp [biaya]).'\n\n" +
                                    "CONTOH 'berapa daya total sekarang?':\n" +
                                    "'Total daya saat ini adalah [angka] Watt dari [jumlah] panel aktif.'\n\n" +
                                    "═══════════════════════════════════════════════════════════════════════════════\n" +
                                    "FORMAT DATA (JSON):\n" +
                                    "```json\n" +
                                    formattedData +
                                    "\n```";
                }

                messages.Add(new { role = "system", content = systemPrompt });

                // ==========================================
                // RIWAYAT CHAT & PESAN USER
                // ==========================================
                if (request.History != null && request.History.Count > 0)
                {
                    foreach (var item in request.History)
                    {
                        messages.Add(new { role = item.Role, content = item.Content });
                    }
                }

                messages.Add(new { role = "user", content = request.Message });

                // Eksekusi API
                var payload = new { model = modelName, messages = messages };
                string jsonString = JsonConvert.SerializeObject(payload);
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(jsonString, Encoding.UTF8, "application/json")
                };
                httpRequest.Headers.Add("Authorization", "Bearer " + apiKey);

                var response = await client.SendAsync(httpRequest);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, new {
                        success = false,
                        message = "Qwen API Error", detail = responseString
                    });
                }

                var responseJson = JObject.Parse(responseString);
                var choices = responseJson["choices"] as JArray;
                if (choices == null || choices.Count == 0 || choices[0]["message"] == null)
                {
                    return StatusCode(502, new { success = false, message = "Response dari Qwen API tidak valid.", detail = responseString });
                }
                string replyText = choices[0]["message"]["content"]?.ToString() ?? "";

                return Ok(new { success = true, reply = replyText });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Internal Server Error", detail = ex.Message });
            }
        }

        [HttpGet("current-dashboard-data")]
        public async Task<IActionResult> GetCurrentDashboardData()
        {
            try
            {
                _logger.LogInformation("=== MULAI PENGAMBILAN DATA REALTIME DARI DATABASE ===");

                // ==========================================
                // 1. DATA PANEL TERBARU (Real-time monitoring) - Dari tabel KWH_Monitoring
                // ==========================================
                _logger.LogInformation("Mengambil data panel dari KWH_Monitoring...");
                
                List<KWHData> allPanelData = new List<KWHData>();
                List<KWHData> validPanels = new List<KWHData>();
                
                try
                {
                    allPanelData = await _context.KWH_Monitoring.ToListAsync();
                    _logger.LogInformation($"Total data dalam KWH_Monitoring: {allPanelData.Count}");
                    
                    if (allPanelData.Any())
                    {
                        validPanels = allPanelData
                            .GroupBy(x => x.DeviceKey)
                            .Select(g => g.OrderByDescending(x => x.Waktu_Server).First())
                            .ToList();
                    }
                    
                    _logger.LogInformation($"[1/7] KWH_Monitoring: {validPanels.Count} panel aktif ditemukan");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Gagal mengambil data dari KWH_Monitoring");
                }

                var panelsJson = validPanels.Select(data => new
                {
                    deviceKey = data.DeviceKey ?? "",
                    deviceId = data.DeviceId ?? "",
                    groupName = data.GroupName ?? "",
                    isThreePhase = data.IsThreePhase,
                    r = data.Volt_R ?? 0m,
                    s = data.Volt_S ?? 0m,
                    t = data.Volt_T ?? 0m,
                    ampR = data.Amp_R ?? 0m,
                    ampS = data.Amp_S ?? 0m,
                    ampT = data.Amp_T ?? 0m,
                    cosPhi = data.Cos_Phi ?? 0m,
                    dayaWatt = data.Daya_Watt ?? 0m,
                    totalW1M_Wh = data.TotalW1M_Wh ?? 0m,
                    energiAktif_Wh = data.Energi_Aktif_Wh ?? 0m,
                    totalEnergy_Wh = data.Total_Energy_Wh ?? 0m,
                    frekuensi = data.Frekuensi_Hz ?? 0m,
                    avgVoltage = data.AvgVoltage,
                    avgAmpere = data.AvgAmpere,
                    status = data.Status ?? "",
                    lastUpdate = data.Waktu_Server,
                    waktuDevice = data.Waktu_Device?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"
                }).ToList();

                // ==========================================
                // 2. TARIFF PER KWH (dari AppSettingsRecords)
                // ==========================================
                _logger.LogInformation("Mengambil tariff dari AppSettingsRecords...");

                List<AppSettingsRecord> allAppSettings = new List<AppSettingsRecord>();
                try
                {
                    allAppSettings = await _context.AppSettingsRecords.ToListAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Gagal mengambil AppSettingsRecords");
                }

                decimal tariffPerKWh = 1500;
                try
                {
                    var tariffRecord = allAppSettings
                        .FirstOrDefault(x => x.SettingKey == "Tariff.PerKWh" || x.SettingKey == "TariffPerKWh");

                    if (tariffRecord != null && 
                        !string.IsNullOrEmpty(tariffRecord.SettingValue) &&
                        decimal.TryParse(tariffRecord.SettingValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedTariff))
                    {
                        tariffPerKWh = parsedTariff;
                    }
                    
                    _logger.LogInformation($"[2/7] AppSettingsRecords: Tarif Per kWh = Rp {tariffPerKWh:N0}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Gagal mengambil tariff dari AppSettingsRecords");
                }

                var tariffJson = new { tariffPerKWh };

                // ==========================================
                // 3. STATISTIK SISTEM (agregat real-time) - Dari KWH_Monitoring
                // ==========================================
                _logger.LogInformation("Menghitung system statistics...");
                
                var systemStats = new
                {
                    totalDaya = validPanels.Sum(x => x.Daya_Watt) ?? 0m,
                    totalEnergy_Wh = validPanels.Sum(x => x.Total_Energy_Wh) ?? 0m,
                    totalW1M_Wh = validPanels.Sum(x => x.TotalW1M_Wh) ?? 0m,
                    totalEnergiAktif_Wh = validPanels.Sum(x => x.Energi_Aktif_Wh) ?? 0m,
                    activePanels = validPanels.Count,
                    avgPowerFactor = validPanels.Count > 0 ? validPanels.Average(x => x.Cos_Phi) ?? 0m : 0,
                    avgVoltage = validPanels.Count > 0 ? validPanels.Average(x => x.AvgVoltage) : 0,
                    avgFrequency = validPanels.Count > 0 ? validPanels.Average(x => x.Frekuensi_Hz) ?? 0m : 0,
                    timestamp = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")
                };
                _logger.LogInformation($"[3/7] System Statistics: Total Daya={systemStats.totalDaya:N0}W, Active Panels={systemStats.activePanels}");

                // ==========================================
                // 4. USAGE STATISTICS PER PANEL (DARI TABEL AGREGAT)
                // ==========================================
                _logger.LogInformation("Mengambil data usage statistics...");
                
                var utcNow = DateTime.UtcNow;
                var jakartaOffset = TimeSpan.FromHours(7);
                var serverNow = new DateTimeOffset(utcNow, TimeSpan.Zero).ToOffset(jakartaOffset).DateTime;
                var serverToday = serverNow.Date;
                var monthStart = new DateTime(serverToday.Year, serverToday.Month, 1);
                var yearStart = new DateTime(serverToday.Year, 1, 1);

                _logger.LogInformation($"Periode waktu: Today={serverToday:yyyy-MM-dd}, Month={monthStart:yyyy-MM-dd}, Year={yearStart.Year}");

                // Inisialisasi dictionaries
                
                // HourlyEnergy - Hari Ini
                var hourlyTodayDict = new Dictionary<string, decimal>();
                try
                {
                    var todayHourly = await _context.HourlyEnergy
                        .Where(x => x.Hour >= serverToday)
                        .ToListAsync();

                    hourlyTodayDict = todayHourly
                        .GroupBy(x => x.DeviceKey)
                        .ToDictionary(g => g.Key, g => g.Sum(x => x.EnergyKWh));

                    _logger.LogInformation($"[4a/7] HourlyEnergy: {hourlyTodayDict.Count} device dengan data hari ini");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Gagal mengambil HourlyEnergy data");
                }

                // DailyEnergy - Bulan Ini
                var dailyThisMonthDict = new Dictionary<string, decimal>();
                try
                {
                    var monthDaily = await _context.DailyEnergy
                        .Where(x => x.Date >= monthStart)
                        .ToListAsync();

                    dailyThisMonthDict = monthDaily
                        .GroupBy(x => x.DeviceKey)
                        .ToDictionary(g => g.Key, g => g.Sum(x => x.EnergyKWh));

                    _logger.LogInformation($"[4b/7] DailyEnergy: {dailyThisMonthDict.Count} device dengan data bulan ini");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Gagal mengambil DailyEnergy data");
                }

                // MonthlyEnergy - Tahun Ini
                var monthlyThisYearDict = new Dictionary<string, decimal>();
                try
                {
                    var yearMonthly = await _context.MonthlyEnergy
                        .Where(x => x.Year == serverNow.Year)
                        .ToListAsync();

                    monthlyThisYearDict = yearMonthly
                        .GroupBy(x => x.DeviceKey)
                        .ToDictionary(g => g.Key, g => g.Sum(x => x.EnergyKWh));

                    _logger.LogInformation($"[4c/7] MonthlyEnergy: {monthlyThisYearDict.Count} device dengan data tahun ini");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Gagal mengambil MonthlyEnergy data");
                }

                // YearlyEnergy - All-Time Total
                var yearlyAllTimeDict = new Dictionary<string, decimal>();
                try
                {
                    var allYearlyData = await _context.YearlyEnergy.ToListAsync();

                    yearlyAllTimeDict = allYearlyData
                        .GroupBy(x => x.DeviceKey)
                        .ToDictionary(g => g.Key, g => g.Sum(x => x.EnergyKWh));

                    _logger.LogInformation($"[4d/7] YearlyEnergy: {yearlyAllTimeDict.Count} device dengan data all-time");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Gagal mengambil YearlyEnergy data");
                }

                // Gabungkan semua data per panel
                var panelUsageFull = new List<PanelUsageData>();
                foreach (var panel in validPanels)
                {
                    try
                    {
                        decimal todayKWh = hourlyTodayDict.ContainsKey(panel.DeviceKey) ? hourlyTodayDict[panel.DeviceKey] : 0;
                        decimal monthKWh = dailyThisMonthDict.ContainsKey(panel.DeviceKey) ? dailyThisMonthDict[panel.DeviceKey] : 0;
                        decimal yearKWh = monthlyThisYearDict.ContainsKey(panel.DeviceKey) ? monthlyThisYearDict[panel.DeviceKey] : 0;
                        decimal allTimeKWh = yearlyAllTimeDict.ContainsKey(panel.DeviceKey) ? yearlyAllTimeDict[panel.DeviceKey] : 0;

                        panelUsageFull.Add(new PanelUsageData
                        {
                            DeviceKey = panel.DeviceKey ?? "",
                            GroupName = panel.GroupName ?? "",
                            TodayKWh = Math.Round(todayKWh, 3),
                            TodayCost = Math.Round(todayKWh * tariffPerKWh, 2),
                            MonthKWh = Math.Round(monthKWh, 3),
                            MonthCost = Math.Round(monthKWh * tariffPerKWh, 2),
                            YearKWh = Math.Round(yearKWh, 3),
                            YearCost = Math.Round(yearKWh * tariffPerKWh, 2),
                            AllTimeKWh = Math.Round(allTimeKWh, 3),
                            AllTimeCost = Math.Round(allTimeKWh * tariffPerKWh, 2),
                            CurrentPower = panel.Daya_Watt ?? 0m,
                            Status = string.IsNullOrEmpty(panel.Status) ? "UNKNOWN" : panel.Status
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Gagal memproses data usage untuk panel {panel.DeviceKey}");
                    }
                }

                // Sort descending by today consumption
                var sortedPanelUsage = panelUsageFull
                    .OrderByDescending(x => x.TodayKWh)
                    .ToList();

                var totalTodayKWh = sortedPanelUsage.Sum(x => x.TodayKWh);
                var totalMonthKWh = sortedPanelUsage.Sum(x => x.MonthKWh);
                var totalYearKWh = sortedPanelUsage.Sum(x => x.YearKWh);

                var usageStatistics = new
                {
                    global = new
                    {
                        today = new { totalKWh = Math.Round(totalTodayKWh, 3) },
                        thisMonth = new { totalKWh = Math.Round(totalMonthKWh, 3) },
                        thisYear = new { totalKWh = Math.Round(totalYearKWh, 3) }
                    },
                    perPanel = new
                    {
                        period = "DATA KONSUMSI PER PANEL (dari tabel agregat sistem)",
                        totalSystemKWh = Math.Round(totalTodayKWh, 3),
                        estimatedTotalCost = Math.Round(totalTodayKWh * tariffPerKWh, 2),
                        rankings = sortedPanelUsage.Select(x => new
                        {
                            deviceKey = x.DeviceKey,
                            groupName = x.GroupName,
                            today = new { energyKWh = x.TodayKWh, estimatedCost = x.TodayCost },
                            thisMonth = new { energyKWh = x.MonthKWh, estimatedCost = x.MonthCost },
                            thisYear = new { energyKWh = x.YearKWh, estimatedCost = x.YearCost },
                            allTime = new { energyKWh = x.AllTimeKWh, estimatedCost = x.AllTimeCost },
                            currentPower = x.CurrentPower,
                            status = x.Status
                        }).ToList(),
                        note = "Data konsumsi per panel diambil LANGSUNG dari tabel HourlyEnergy/DailyEnergy/MonthlyEnergy — sudah dikalkulasi dan divalidasi oleh sistem. INI SUMBER PALING AKURAT untuk tagihan."
                    }
                };

                // ==========================================
                // 5. ANOMALY LOGS TERBARU (dari tabel AnomalyLogs)
                // ==========================================
                _logger.LogInformation("Mengambil data anomaly logs...");
                
                var recentAnomalies = new List<object>();
                int totalAnomalies = 0, unackAnomalies = 0, last24hAnomalies = 0;
                
                try
                {
                    var allAnomalyLogs = await _context.AnomalyLogs.ToListAsync();
                    
                    // Recent anomalies (last 20)
                    recentAnomalies = allAnomalyLogs
                        .OrderByDescending(x => x.DetectedTime)
                        .Take(20)
                        .Select(a => new
                        {
                            id = a.Id,
                            deviceKey = a.DeviceKey ?? "",
                            anomalyType = a.AnomalyType ?? "",
                            powerValue = a.PowerValue,
                            deviation = a.Deviation,
                            detectedTime = a.DetectedTime,
                            acknowledged = a.Acknowledged ?? false
                        })
                        .Cast<object>()
                        .ToList();

                    totalAnomalies = allAnomalyLogs.Count;
                    unackAnomalies = allAnomalyLogs.Count(x => x.Acknowledged != true);
                    last24hAnomalies = allAnomalyLogs.Count(x => x.DetectedTime >= DateTime.Now.AddDays(-1));
                    
                    _logger.LogInformation($"[5/7] AnomalyLogs: Total={totalAnomalies}, Unacknowledged={unackAnomalies}, Last24h={last24hAnomalies}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Gagal mengambil AnomalyLogs data");
                }
                
                var anomaliesSummary = new
                {
                    totalCount = totalAnomalies,
                    unacknowledged = unackAnomalies,
                    last24Hours = last24hAnomalies,
                    recentLogs = recentAnomalies
                };

                // ==========================================
                // 6. CHART DATA (Time-Series History dari KWH_Monitoring)
                // ==========================================
                _logger.LogInformation("Mengambil chart history data...");
                
                var chartData = new List<object>();
                try
                {
                    foreach (var panel in validPanels)
                    {
                        try
                        {
                            var panelHistory = allPanelData
                                .Where(x => x.DeviceKey == panel.DeviceKey)
                                .OrderByDescending(x => x.Waktu_Server)
                                .Take(20)
                                .OrderBy(x => x.Waktu_Server)
                                .ToList();

                            var panelChart = new
                            {
                                deviceKey = panel.DeviceKey ?? "",
                                groupName = panel.GroupName ?? "",
                                labels = panelHistory.Select(x => x.Waktu_Server.ToString("HH:mm:ss")).ToList(),
                                power = panelHistory.Select(x => Convert.ToDouble(x.Daya_Watt ?? 0m)).ToList(),
                                voltageR = panelHistory.Select(x => Convert.ToDouble(x.Volt_R ?? 0m)).ToList(),
                                voltageS = panelHistory.Where(x => x.Volt_S.HasValue).Select(x => Convert.ToDouble(x.Volt_S.Value)).ToList(),
                                voltageT = panelHistory.Where(x => x.Volt_T.HasValue).Select(x => Convert.ToDouble(x.Volt_T.Value)).ToList(),
                                currentR = panelHistory.Select(x => Convert.ToDouble(x.Amp_R ?? 0m)).ToList(),
                                currentS = panelHistory.Where(x => x.Amp_S.HasValue).Select(x => Convert.ToDouble(x.Amp_S.Value)).ToList(),
                                currentT = panelHistory.Where(x => x.Amp_T.HasValue).Select(x => Convert.ToDouble(x.Amp_T.Value)).ToList(),
                                isThreePhase = panelHistory.Any(x => x.Volt_S.HasValue && x.Volt_T.HasValue && x.Amp_S.HasValue && x.Amp_T.HasValue),
                                pointCount = panelHistory.Count
                            };
                            chartData.Add(panelChart);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Gagal mengambil chart data untuk panel {panel.DeviceKey}: {ex.Message}");
                        }
                    }
                    
                    _logger.LogInformation($"[6/7] Chart History: {chartData.Count} panel dengan time-series data");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Gagal mengambil Chart History data");
                }

                // ==========================================
                // 7. DEVICE CATEGORIES (dari AppSettingsRecords)
                // ==========================================
                _logger.LogInformation("Mengambil device categories...");
                
                var categorySettings = new Dictionary<string, string>();
                try
                {
                    var categoryRecords = allAppSettings
                        .Where(x => x.SettingKey.StartsWith("DeviceCategory."))
                        .ToList();
                    
                    categorySettings = categoryRecords
                        .ToDictionary(
                            x => x.SettingKey.Replace("DeviceCategory.", ""), 
                            x => x.SettingValue ?? ""
                        );
                        
                    _logger.LogInformation($"[7/7] Device Categories: {categorySettings.Count} kategori device ditemukan");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Gagal mengambil Device Categories");
                }

                // ==========================================
                // RESPONSE UTUH - SEMUA DATA DARI 7 TABEL DATABASE
                // ==========================================
                var result = new
                {
                    timestamp = DateTime.Now.ToString("o"),
                    source = "Direct Database Query - ALL TABLES (Most Accurate)",
                    note = "Data diambil langsung dari SEMUA tabel database: KWH_Monitoring, HourlyEnergy, DailyEnergy, MonthlyEnergy, YearlyEnergy, AnomalyLogs, AppSettingsRecords",
                    databaseTables = new
                    {
                        kwhMonitoring = validPanels.Count,
                        hourlyEnergy = hourlyTodayDict.Count,
                        dailyEnergy = dailyThisMonthDict.Count,
                        monthlyEnergy = monthlyThisYearDict.Count,
                        yearlyEnergy = yearlyAllTimeDict.Count,
                        anomalyLogs = totalAnomalies,
                        appSettingsRecords = categorySettings.Count + 1 // +1 for tariff
                    },
                    tariff = tariffJson,
                    systemStatistics = systemStats,
                    usageStatistics = usageStatistics,
                    anomalies = anomaliesSummary,
                    panels = panelsJson,
                    chartHistory = chartData,
                    deviceCategories = categorySettings
                };

                _logger.LogInformation($"=== SELESAI: Data realtime berhasil dikirim - {validPanels.Count} panel, {chartData.Count} charts, {totalAnomalies} anomalies ===");
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "=== FATAL ERROR in GetCurrentDashboardData: {Message} ===", ex.Message);
                return Ok(new
                {
                    timestamp = DateTime.Now.ToString("o"),
                    source = "Error Handler - Fallback Response",
                    error = "Gagal mengambil data sistem: " + ex.Message,
                    databaseTables = new { kwhMonitoring = 0, hourlyEnergy = 0, dailyEnergy = 0, monthlyEnergy = 0, yearlyEnergy = 0, anomalyLogs = 0, appSettingsRecords = 0 },
                    tariff = new { tariffPerKWh = 1500 },
                    systemStatistics = new { totalDaya = 0, activePanels = 0, timestamp = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") },
                    usageStatistics = new { 
                        global = new { today = new { totalKWh = 0 }, thisMonth = new { totalKWh = 0 }, thisYear = new { totalKWh = 0 } },
                        perPanel = new { rankings = new List<object>() }
                    },
                    anomalies = new { totalCount = 0, unacknowledged = 0, last24Hours = 0, recentLogs = new List<object>() },
                    panels = new List<object>(),
                    chartHistory = new List<object>(),
                    deviceCategories = new Dictionary<string, string>()
                });
            }
        }

        [HttpGet("test")]
        public async Task<IActionResult> TestConnection()
        {
            var chatbotConfig = await GetChatbotConfigAsync();
            string apiKey = chatbotConfig.ApiKey;
            string modelName = chatbotConfig.Model;
            string url = chatbotConfig.ApiUrl;

            if (string.IsNullOrEmpty(apiKey) || apiKey.StartsWith("YOUR_API_KEY") || apiKey == "CHANGE_ME")
            {
                return BadRequest(new { status = "ERROR", message = "AI Chatbot API Key belum dikonfigurasi. Atur di Settings > AI Chatbot." });
            }

            try
            {
                var client = _httpClientFactory.CreateClient("QwenClient");

                var payload = new
                {
                    model = modelName,
                    messages = new[]
                    {
                        new { role = "user", content = "Halo, ini tes koneksi. Jawab singkat: 'OK'." }
                    }
                };

                string jsonString = JsonConvert.SerializeObject(payload);
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
                httpRequest.Content = new StringContent(jsonString, Encoding.UTF8, "application/json");
                httpRequest.Headers.Add("Authorization", "Bearer " + apiKey);

                var response = await client.SendAsync(httpRequest);
                var responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = JObject.Parse(responseString);
                    var choices = responseJson["choices"] as JArray;
                    string replyText = "";
                    if (choices != null && choices.Count > 0 && choices[0]["message"] != null)
                    {
                        replyText = choices[0]["message"]["content"]?.ToString() ?? "";
                    }

                    return Ok(new {
                        status = "SUCCESS",
                        message = "Berhasil terhubung ke Qwen Model: " + modelName,
                        qwenResponse = replyText.Trim()
                    });
                }
                else
                {
                    return StatusCode((int)response.StatusCode, new {
                        status = "FAILED",
                        httpCode = (int)response.StatusCode,
                        message = "Gagal terhubung ke Qwen API.",
                        rawError = responseString
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { status = "EXCEPTION", message = ex.Message });
            }
        }
    }
}