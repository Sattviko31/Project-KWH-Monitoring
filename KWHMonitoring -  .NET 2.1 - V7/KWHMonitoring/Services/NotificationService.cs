using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using KWHMonitoring.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KWHMonitoring.Services
{
    public class NotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NotificationService> _logger;
        private NotificationSettings _settings;
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        public NotificationService(ApplicationDbContext context, ILogger<NotificationService> logger)
        {
            _context = context;
            _logger = logger;
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                var settingsRecord = _context.AppSettingsRecords
                    .Where(x => x.SettingKey.StartsWith("Notification"))
                    .ToDictionary(x => x.SettingKey, x => x.SettingValue);

                var phoneNumbers = new List<string>();
                var phonesRaw = GetVal(settingsRecord, "Notification.WablasPhoneNumbers", "");
                if (!string.IsNullOrWhiteSpace(phonesRaw))
                {
                    phoneNumbers = phonesRaw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .Where(p => !string.IsNullOrEmpty(p))
                        .ToList();
                }

                _settings = new NotificationSettings
                {
                    SmtpServer = GetVal(settingsRecord, "Notification.SmtpServer", "smtp.gmail.com"),
                    SmtpPort = int.TryParse(GetVal(settingsRecord, "Notification.SmtpPort", "587"), out var port) ? port : 587,
                    SenderEmail = GetVal(settingsRecord, "Notification.SenderEmail", null),
                    SenderPassword = GetVal(settingsRecord, "Notification.SenderPassword", null),
                    RecipientEmail = GetVal(settingsRecord, "Notification.RecipientEmail", null),
                    EnableEmailNotification = bool.TryParse(GetVal(settingsRecord, "Notification.EnableEmail", "false"), out var emailOn) && emailOn,

                    WablasServerUrl = GetVal(settingsRecord, "Notification.WablasServerUrl", null),
                    WablasToken = GetVal(settingsRecord, "Notification.WablasToken", null),
                    WablasSecretKey = GetVal(settingsRecord, "Notification.WablasSecretKey", null),
                    WablasPhoneNumbers = phoneNumbers,
                    EnableWhatsAppNotification = bool.TryParse(GetVal(settingsRecord, "Notification.EnableWhatsApp", "false"), out var waOn) && waOn,

                    SendInstantAlert = bool.TryParse(GetVal(settingsRecord, "Notification.SendInstantAlert", "true"), out var instantOn) ? instantOn : true,
                    SendHourlyReport = bool.TryParse(GetVal(settingsRecord, "Notification.SendHourlyReport", "true"), out var hourlyOn) ? hourlyOn : true,
                    SendDailyReport = bool.TryParse(GetVal(settingsRecord, "Notification.SendDailyReport", "false"), out var dailyOn) && dailyOn,
                    SendMonthlyReport = bool.TryParse(GetVal(settingsRecord, "Notification.SendMonthlyReport", "false"), out var monthlyOn) && monthlyOn,
                    HourlyReportInterval = 0,
                    DailyReportTime = GetVal(settingsRecord, "Notification.DailyReportTime", "08:00"),
                    MonthlyReportDay = int.TryParse(GetVal(settingsRecord, "Notification.MonthlyReportDay", "1"), out var mday) ? mday : 1,
                    MonthlyReportTime = GetVal(settingsRecord, "Notification.MonthlyReportTime", "08:00")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading notification settings");
                _settings = new NotificationSettings();
            }
        }

        private static string GetVal(Dictionary<string, string> dict, string key, string defaultValue)
        {
            string value;
            return dict.TryGetValue(key, out value) ? value : defaultValue;
        }

        private async Task<(int maxCapacity, int mediumThreshold, int normalThreshold)> LoadThresholdsAsync()
        {
            int maxCap = 30000, mediumThresh = 70, normalThresh = 30;

            try
            {
                var keys = new[] { "Load.MaxCapacity", "Load.MediumThreshold", "Load.NormalThreshold" };
                var records = await _context.AppSettingsRecords
                    .Where(x => keys.Contains(x.SettingKey))
                    .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);

                if (records.ContainsKey("Load.MaxCapacity") && int.TryParse(records["Load.MaxCapacity"], out var mc) && mc > 0)
                    maxCap = mc;
                if (records.ContainsKey("Load.MediumThreshold") && int.TryParse(records["Load.MediumThreshold"], out var mt))
                    mediumThresh = mt;
                if (records.ContainsKey("Load.NormalThreshold") && int.TryParse(records["Load.NormalThreshold"], out var nt))
                    normalThresh = nt;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load thresholds from DB, using defaults");
            }

            return (maxCap, mediumThresh, normalThresh);
        }

        public void ReloadSettings()
        {
            LoadSettings();
        }

        public NotificationSettings GetSettings()
        {
            return _settings;
        }

        private async Task<decimal> GetTariffPerKWhAsync()
        {
            try
            {
                var tariffRecord = await _context.AppSettingsRecords
                    .FirstOrDefaultAsync(x => x.SettingKey == "Tariff.PerKWh" || x.SettingKey == "TariffPerKWh");

                if (tariffRecord != null && decimal.TryParse(tariffRecord.SettingValue, out var result))
                    return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load tariff from DB, using default 1500");
            }

            return 1500m;
        }

        // ============================================
        // ANOMALY ALERT
        // ============================================
        public async Task SendAnomalyAlertAsync(string deviceKey, string anomalyType, decimal powerValue, decimal thresholdValue, decimal deviation)
        {
            if (!_settings.EnableEmailNotification && !_settings.EnableWhatsAppNotification)
                return;

            if (!_settings.SendInstantAlert)
                return;

            // Use the rich format for instant alerts
            await SendRealtimeInstantAlertAsync(deviceKey, anomalyType, powerValue, thresholdValue, deviation, isTest: false);
        }

        // ============================================
        // REALTIME INSTANT ALERT (styled like other reports)
        // ============================================
        public async Task SendRealtimeInstantAlertAsync(string deviceKey, string anomalyType, decimal powerValue, decimal thresholdValue, decimal deviation, bool isTest = true)
        {
            if (!_settings.EnableEmailNotification && !_settings.EnableWhatsAppNotification)
                return;

            // Skip if instant alerts are disabled (unless it's a test call)
            if (!isTest && !_settings.SendInstantAlert)
                return;

            var now = DateTime.Now;

            // Get latest panel data for the device
            var panelData = await _context.KWH_Monitoring
                .Where(x => x.DeviceKey == deviceKey)
                .OrderByDescending(x => x.Waktu_Server)
                .FirstOrDefaultAsync();

            // Get recent anomaly count (last 24 hours)
            var last24h = now.AddHours(-24);
            var recentAnomalies = await _context.AnomalyLogs
                .Where(x => x.DetectedTime >= last24h)
                .ToListAsync();
            var recentOverloadCount = recentAnomalies.Count(x => x.AnomalyType == "OVERLOAD");
            var recentDropCount = recentAnomalies.Count(x => x.AnomalyType == "DROP");

            // Anomaly type styling
            var isOverload = anomalyType == "OVERLOAD";
            var alertColor = isOverload ? "#dc3545" : "#ffc107";
            var alertGradient = isOverload
                ? "linear-gradient(135deg, #dc3545 0%, #c82333 100%)"
                : "linear-gradient(135deg, #ffc107 0%, #e0a800 100%)";
            var alertIcon = isOverload ? "🔴" : "🟡";
            var alertLabel = isOverload ? "OVERLOAD" : "DEVICE DROP";
            var severityPct = Math.Abs(deviation);
            var severityLevel = severityPct > 50 ? "CRITICAL" : severityPct > 20 ? "HIGH" : severityPct > 10 ? "MEDIUM" : "LOW";
            var severityColor = severityPct > 50 ? "#dc3545" : severityPct > 20 ? "#fd7e14" : severityPct > 10 ? "#ffc107" : "#198754";

            // Panel info
            var groupName = panelData?.GroupName ?? deviceKey;
            var panelVoltage = panelData?.AvgVoltage ?? 0;
            var panelCurrent = panelData?.Amp_R ?? 0;
            var panelPF = panelData?.Cos_Phi ?? 0;
            var panelFreq = panelData?.Frekuensi_Hz ?? 0;
            var panelEnergy = panelData?.Total_Energy_Wh ?? 0;

            var subject = $"⚠️ {alertLabel} Alert - {groupName}";
            var message = $@"
                <div style='font-family: Arial, sans-serif; max-width: 800px; margin: 0 auto;'>
                    <!-- Header -->
                    <div style='background: {alertGradient}; color: white; padding: 25px; text-align: center;'>
                        <h1 style='margin: 0; font-size: 24px;'>⚠️ KWH MONITORING</h1>
                        <p style='margin: 5px 0 0 0; font-size: 16px; opacity: 0.9;'>Instant Alert - {alertLabel}</p>
                    </div>

                    <!-- Alert Info -->
                    <div style='padding: 20px; background-color: #f8f9fa; border-bottom: 3px solid {alertColor};'>
                        <p style='margin: 0; font-size: 14px;'><strong>📅 Time:</strong> {now:dd/MM/yyyy HH:mm:ss}</p>
                        <p style='margin: 5px 0 0 0; font-size: 14px;'><strong>🖥️ Device:</strong> {groupName}</p>
                        <p style='margin: 5px 0 0 0; font-size: 14px;'><strong>📊 Severity:</strong> <span style='background-color: {severityColor}; color: white; padding: 2px 10px; border-radius: 12px; font-size: 12px;'>{severityLevel}</span></p>
                    </div>

                    <!-- Alert Detail Cards -->
                    <div style='padding: 20px;'>
                        <h3 style='color: #333; border-bottom: 2px solid {alertColor}; padding-bottom: 10px;'>⚠️ Alert Details</h3>
                        <table style='width: 100%; border-collapse: collapse;'>
                            <tr>
                                <td style='padding: 10px;'>
                                    <div style='background: {alertGradient}; color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Current Power</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{powerValue:N0} W</div>
                                    </div>
                                </td>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Threshold</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{thresholdValue:N0} W</div>
                                    </div>
                                </td>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, {severityColor} 0%, {alertColor} 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Deviation</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{deviation:N1}%</div>
                                    </div>
                                </td>
                            </tr>
                        </table>

                        <!-- Device Status -->
                        <h3 style='color: #333; border-bottom: 2px solid {alertColor}; padding-bottom: 10px; margin-top: 25px;'>🖥️ Device Status</h3>
                        <table style='width: 100%; border-collapse: collapse; font-size: 13px;'>
                            <thead>
                                <tr style='background-color: {alertColor}; color: white;'>
                                    <th style='padding: 10px; text-align: left;'>Device</th>
                                    <th style='padding: 10px; text-align: center;'>Power</th>
                                    <th style='padding: 10px; text-align: center;'>Voltage</th>
                                    <th style='padding: 10px; text-align: center;'>Current</th>
                                    <th style='padding: 10px; text-align: center;'>PF</th>
                                    <th style='padding: 10px; text-align: center;'>Freq</th>
                                    <th style='padding: 10px; text-align: center;'>Energy</th>
                                </tr>
                            </thead>
                            <tbody>
                                <tr style='border-bottom: 1px solid #eee;'>
                                    <td style='padding: 8px; font-weight: bold;'>{groupName}</td>
                                    <td style='padding: 8px; text-align: center;'><span style='background-color: {alertColor}; color: white; padding: 2px 8px; border-radius: 4px; font-size: 11px;'>{powerValue:N0} W</span></td>
                                    <td style='padding: 8px; text-align: center;'>{panelVoltage:N0} V</td>
                                    <td style='padding: 8px; text-align: center;'>{panelCurrent:N0} A</td>
                                    <td style='padding: 8px; text-align: center;'>{panelPF:N2}</td>
                                    <td style='padding: 8px; text-align: center;'>{Math.Round(panelFreq)} Hz</td>
                                    <td style='padding: 8px; text-align: center;'>{panelEnergy:N0} Wh</td>
                                </tr>
                            </tbody>
                        </table>

                        <!-- Recent Anomaly Summary (24h) -->
                        <div style='margin-top: 20px; padding: 15px; background-color: #fff3cd; border-left: 4px solid #ffc107; border-radius: 5px;'>
                            <h4 style='margin: 0 0 10px 0; color: #856404;'>⚠️ Anomaly Summary (Last 24 Hours)</h4>
                            <table style='width: 100%;'>
                                <tr>
                                    <td style='padding: 5px;'><strong>Total Anomalies:</strong></td>
                                    <td style='padding: 5px; text-align: right;'><span style='background-color: #dc3545; color: white; padding: 3px 10px; border-radius: 12px;'>{recentAnomalies.Count}</span></td>
                                    <td style='padding: 5px;'><strong>Overload:</strong></td>
                                    <td style='padding: 5px; text-align: right;'><span style='background-color: #dc3545; color: white; padding: 3px 10px; border-radius: 12px;'>{recentOverloadCount}</span></td>
                                </tr>
                                <tr>
                                    <td style='padding: 5px;'><strong>Device Drop:</strong></td>
                                    <td style='padding: 5px; text-align: right;'><span style='background-color: #ffc107; color: #333; padding: 3px 10px; border-radius: 12px;'>{recentDropCount}</span></td>
                                    <td style='padding: 5px;'><strong>Affected Devices:</strong></td>
                                    <td style='padding: 5px; text-align: right;'><span style='background-color: #17a2b8; color: white; padding: 3px 10px; border-radius: 12px;'>{recentAnomalies.Select(x => x.DeviceKey).Distinct().Count()}</span></td>
                                </tr>
                            </table>
                        </div>
                    </div>

                    <!-- Footer -->
                    <div style='background-color: #343a40; color: white; padding: 15px; text-align: center; font-size: 12px;'>
                        <p style='margin: 0;'>⚠️ KWH Monitoring System - Instant Alert</p>
                        <p style='margin: 5px 0 0 0; opacity: 0.7;'>Generated at {now:dd/MM/yyyy HH:mm:ss}</p>
                    </div>
                </div>";

            if (_settings.EnableEmailNotification)
            {
                await SendEmailAsync(subject, message);
            }

            if (_settings.EnableWhatsAppNotification)
            {
                var whatsappMessage = string.Format(
                    "{0} *{1}INSTANT ALERT*\n\n" +
                    "🖥️ Device: *{2}*\n" +
                    "📊 Type: *{3}*\n" +
                    "⚡ Power: *{4:N0} W*\n" +
                    "📏 Threshold: *{5:N0} W*\n" +
                    "📈 Deviation: *{6:N1}%*\n" +
                    "🏷️ Severity: *{7}*\n" +
                    "🕐 Time: *{8:dd/MM/yyyy HH:mm:ss}*\n\n" +
                    "━━━━━━━━━━━━━━━━━━\n" +
                    "🖥️ *DEVICE STATUS*\n" +
                    "━━━━━━━━━━━━━━━━━━\n\n" +
                    "🔌 Voltage: *{9:N0} V*\n" +
                    "⚡ Current: *{10:N0} A*\n" +
                    "📏 PF: *{11:N2}*\n" +
                    "🔄 Freq: *{12:N0} Hz*\n" +
                    "🔋 Energy: *{13:N0} Wh*\n\n" +
                    "━━━━━━━━━━━━━━━━━━\n" +
                    "⚠️ *ANOMALY (24h)*\n" +
                    "━━━━━━━━━━━━━━━━━━\n\n" +
                    "📊 Total: *{14}*\n" +
                    "🔴 Overload: *{15}*\n" +
                    "🟡 Drop: *{16}*\n\n" +
                    "_KWH Monitoring System_",
                    alertIcon, isTest ? "[TEST] " : "", groupName, alertLabel,
                    powerValue, thresholdValue, deviation, severityLevel,
                    now, panelVoltage, panelCurrent, panelPF, panelFreq, panelEnergy,
                    recentAnomalies.Count, recentOverloadCount, recentDropCount);
                await SendWablasAsync(whatsappMessage);
            }

            _logger.LogInformation(string.Format("Realtime instant alert sent for {0}: {1}", deviceKey, anomalyType));
        }

        // ============================================
        // DOWNTIME POWER ALERT (styled like other reports)
        // Notifikasi khusus: listrik seharusnya mati tapi masih menyala
        // ============================================
        public async Task SendDowntimePowerAlertAsync(string deviceKey, decimal powerValue, int startHour, int endHour)
        {
            if (!_settings.EnableEmailNotification && !_settings.EnableWhatsAppNotification)
                return;

            if (!_settings.SendInstantAlert)
                return;

            var now = DateTime.Now;

            // Get latest panel data for the device
            var panelData = await _context.KWH_Monitoring
                .Where(x => x.DeviceKey == deviceKey)
                .OrderByDescending(x => x.Waktu_Server)
                .FirstOrDefaultAsync();

            // Get recent anomaly count (last 24 hours)
            var last24h = now.AddHours(-24);
            var recentAnomalies = await _context.AnomalyLogs
                .Where(x => x.DetectedTime >= last24h)
                .ToListAsync();
            var recentOverloadCount = recentAnomalies.Count(x => x.AnomalyType == "OVERLOAD");
            var recentDropCount = recentAnomalies.Count(x => x.AnomalyType == "DROP");

            // Panel info
            var groupName = panelData?.GroupName ?? deviceKey;
            var panelVoltage = panelData?.AvgVoltage ?? 0;
            var panelCurrent = panelData?.Amp_R ?? 0;
            var panelPF = panelData?.Cos_Phi ?? 0;
            var panelFreq = panelData?.Frekuensi_Hz ?? 0;
            var panelEnergy = panelData?.Total_Energy_Wh ?? 0;

            var downtimePeriod = string.Format("{0:D2}:00 - {1:D2}:00", startHour, endHour);

            var subject = $"🔴 POWER ALERT - Listrik menyala saat jam mati! - {groupName}";
            var message = $@"
                <div style='font-family: Arial, sans-serif; max-width: 800px; margin: 0 auto;'>
                    <!-- Header -->
                    <div style='background: linear-gradient(135deg, #dc3545 0%, #6f1e1e 100%); color: white; padding: 25px; text-align: center;'>
                        <h1 style='margin: 0; font-size: 24px;'>🔴 KWH MONITORING</h1>
                        <p style='margin: 5px 0 0 0; font-size: 16px; opacity: 0.9;'>Power Alert - Listrik Menyala Saat Jam Mati</p>
                    </div>

                    <!-- Alert Info -->
                    <div style='padding: 20px; background-color: #f8d7da; border-bottom: 3px solid #dc3545;'>
                        <p style='margin: 0; font-size: 14px;'><strong>📅 Time:</strong> {now:dd/MM/yyyy HH:mm:ss}</p>
                        <p style='margin: 5px 0 0 0; font-size: 14px;'><strong>🖥️ Device:</strong> {groupName}</p>
                        <p style='margin: 5px 0 0 0; font-size: 14px;'><strong>📊 Severity:</strong> <span style='background-color: #dc3545; color: white; padding: 2px 10px; border-radius: 12px; font-size: 12px;'>CRITICAL</span></p>
                    </div>

                    <!-- Alert Detail Cards -->
                    <div style='padding: 20px;'>
                        <h3 style='color: #333; border-bottom: 2px solid #dc3545; padding-bottom: 10px;'>⚠️ Alert Details</h3>
                        <table style='width: 100%; border-collapse: collapse;'>
                            <tr>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #dc3545 0%, #6f1e1e 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Power Terdeteksi</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{powerValue:N0} W</div>
                                    </div>
                                </td>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #343a40 0%, #6c757d 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Periode Jam Mati</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{downtimePeriod}</div>
                                    </div>
                                </td>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #dc3545 0%, #fd7e14 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Status</div>
                                        <div style='font-size: 18px; font-weight: bold;'>SEHARUSNYA MATI</div>
                                    </div>
                                </td>
                            </tr>
                        </table>

                        <!-- Warning Box -->
                        <div style='margin-top: 20px; padding: 15px; background-color: #f8d7da; border-left: 4px solid #dc3545; border-radius: 5px;'>
                            <h4 style='margin: 0 0 10px 0; color: #721c24;'>⚡ Perhatian!</h4>
                            <p style='margin: 0; color: #721c24;'>Listrik seharusnya mati pada periode <strong>{downtimePeriod}</strong>, tetapi terdeteksi power <strong>{powerValue:N0} W</strong>. Silakan periksa perangkat segera.</p>
                        </div>

                        <!-- Device Status -->
                        <h3 style='color: #333; border-bottom: 2px solid #dc3545; padding-bottom: 10px; margin-top: 25px;'>🖥️ Device Status</h3>
                        <table style='width: 100%; border-collapse: collapse; font-size: 13px;'>
                            <thead>
                                <tr style='background-color: #dc3545; color: white;'>
                                    <th style='padding: 10px; text-align: left;'>Device</th>
                                    <th style='padding: 10px; text-align: center;'>Power</th>
                                    <th style='padding: 10px; text-align: center;'>Voltage</th>
                                    <th style='padding: 10px; text-align: center;'>Current</th>
                                    <th style='padding: 10px; text-align: center;'>PF</th>
                                    <th style='padding: 10px; text-align: center;'>Freq</th>
                                    <th style='padding: 10px; text-align: center;'>Energy</th>
                                </tr>
                            </thead>
                            <tbody>
                                <tr style='border-bottom: 1px solid #eee;'>
                                    <td style='padding: 8px; font-weight: bold;'>{groupName}</td>
                                    <td style='padding: 8px; text-align: center;'><span style='background-color: #dc3545; color: white; padding: 2px 8px; border-radius: 4px; font-size: 11px;'>{powerValue:N0} W</span></td>
                                    <td style='padding: 8px; text-align: center;'>{panelVoltage:N0} V</td>
                                    <td style='padding: 8px; text-align: center;'>{panelCurrent:N0} A</td>
                                    <td style='padding: 8px; text-align: center;'>{panelPF:N2}</td>
                                    <td style='padding: 8px; text-align: center;'>{Math.Round(panelFreq)} Hz</td>
                                    <td style='padding: 8px; text-align: center;'>{panelEnergy:N0} Wh</td>
                                </tr>
                            </tbody>
                        </table>

                        <!-- Recent Anomaly Summary (24h) -->
                        <div style='margin-top: 20px; padding: 15px; background-color: #fff3cd; border-left: 4px solid #ffc107; border-radius: 5px;'>
                            <h4 style='margin: 0 0 10px 0; color: #856404;'>⚠️ Anomaly Summary (Last 24 Hours)</h4>
                            <table style='width: 100%;'>
                                <tr>
                                    <td style='padding: 5px;'><strong>Total Anomalies:</strong></td>
                                    <td style='padding: 5px; text-align: right;'><span style='background-color: #dc3545; color: white; padding: 3px 10px; border-radius: 12px;'>{recentAnomalies.Count}</span></td>
                                    <td style='padding: 5px;'><strong>Overload:</strong></td>
                                    <td style='padding: 5px; text-align: right;'><span style='background-color: #dc3545; color: white; padding: 3px 10px; border-radius: 12px;'>{recentOverloadCount}</span></td>
                                </tr>
                                <tr>
                                    <td style='padding: 5px;'><strong>Device Drop:</strong></td>
                                    <td style='padding: 5px; text-align: right;'><span style='background-color: #ffc107; color: #333; padding: 3px 10px; border-radius: 12px;'>{recentDropCount}</span></td>
                                    <td style='padding: 5px;'><strong>Affected Devices:</strong></td>
                                    <td style='padding: 5px; text-align: right;'><span style='background-color: #17a2b8; color: white; padding: 3px 10px; border-radius: 12px;'>{recentAnomalies.Select(x => x.DeviceKey).Distinct().Count()}</span></td>
                                </tr>
                            </table>
                        </div>
                    </div>

                    <!-- Footer -->
                    <div style='background-color: #343a40; color: white; padding: 15px; text-align: center; font-size: 12px;'>
                        <p style='margin: 0;'>🔴 KWH Monitoring System - Downtime Power Alert</p>
                        <p style='margin: 5px 0 0 0; opacity: 0.7;'>Generated at {now:dd/MM/yyyy HH:mm:ss}</p>
                    </div>
                </div>";

            if (_settings.EnableEmailNotification)
            {
                await SendEmailAsync(subject, message);
            }

            if (_settings.EnableWhatsAppNotification)
            {
                var whatsappMessage = string.Format(
                    "🔴 *POWER ALERT*\n\n" +
                    "🖥️ Device: *{0}*\n" +
                    "⚡ Power: *{1:N0} W*\n" +
                    "🕐 Jam Mati: *{2}*\n" +
                    "🏷️ Status: *SEHARUSNYA MATI TAPI MASIH MENYALA*\n" +
                    "🕐 Time: *{3:dd/MM/yyyy HH:mm:ss}*\n\n" +
                    "━━━━━━━━━━━━━━━━━━\n" +
                    "🖥️ *DEVICE STATUS*\n" +
                    "━━━━━━━━━━━━━━━━━━\n\n" +
                    "🔌 Voltage: *{4:N0} V*\n" +
                    "⚡ Current: *{5:N0} A*\n" +
                    "📏 PF: *{6:N2}*\n" +
                    "🔄 Freq: *{7:N0} Hz*\n" +
                    "🔋 Energy: *{8:N0} Wh*\n\n" +
                    "━━━━━━━━━━━━━━━━━━\n" +
                    "⚠️ *ANOMALY (24h)*\n" +
                    "━━━━━━━━━━━━━━━━━━\n\n" +
                    "📊 Total: *{9}*\n" +
                    "🔴 Overload: *{10}*\n" +
                    "🟡 Drop: *{11}*\n\n" +
                    "_KWH Monitoring System_",
                    groupName, powerValue, downtimePeriod,
                    now, panelVoltage, panelCurrent, panelPF, panelFreq, panelEnergy,
                    recentAnomalies.Count, recentOverloadCount, recentDropCount);
                await SendWablasAsync(whatsappMessage);
            }

            _logger.LogInformation(string.Format("Downtime power alert sent for {0}: {1:N0}W during {2}", deviceKey, powerValue, downtimePeriod));
        }

        // ============================================
        // HOURLY REPORT
        // [UNUSED] - Versi non-realtime. Semua pemanggilan menggunakan
        // SendRealtimeHourlyReportAsync() yang lebih lengkap.
        // ============================================
        public async Task SendHourlyReportAsync()
        {
            if (!_settings.EnableEmailNotification && !_settings.EnableWhatsAppNotification)
                return;

            if (!_settings.SendHourlyReport)
                return;

            var oneHourAgo = DateTime.Now.AddHours(-1);

            var anomalies = await _context.AnomalyLogs
                .Where(x => x.DetectedTime >= oneHourAgo)
                .OrderByDescending(x => x.DetectedTime)
                .ToListAsync();

            var overloadCount = anomalies.Count(x => x.AnomalyType == "OVERLOAD");
            var dropCount = anomalies.Count(x => x.AnomalyType == "DROP");
            var deviceCount = anomalies.Select(x => x.DeviceKey).Distinct().Count();

            var subject = string.Format("Hourly Report - {0:dd/MM/yyyy HH:mm}", DateTime.Now);
            var message = string.Format(@"
                <h2>Hourly Monitoring Report</h2>
                <p><strong>Period:</strong> {0:dd/MM/yyyy HH:mm} - {1:dd/MM/yyyy HH:mm}</p>
                <hr>
                <h3>Summary</h3>
                <ul>
                    <li><strong>Total Anomalies:</strong> {2}</li>
                    <li><strong>Overload Events:</strong> {3}</li>
                    <li><strong>Device Drop Events:</strong> {4}</li>
                    <li><strong>Affected Devices:</strong> {5}</li>
                </ul>
                <hr>
                <p><em>This is an automated report from KWH Monitoring System</em></p>
            ", oneHourAgo, DateTime.Now, anomalies.Count, overloadCount, dropCount, deviceCount);

            if (_settings.EnableEmailNotification)
            {
                await SendEmailAsync(subject, message);
            }

            if (_settings.EnableWhatsAppNotification)
            {
                var whatsappMessage = string.Format(
                    "📋 *HOURLY REPORT*\n\n" +
                    "🕐 Period: {0:HH:mm} - {1:HH:mm}\n\n" +
                    "📊 Total Anomalies: {2}\n" +
                    "🔴 Overload: {3}\n" +
                    "🟡 Device Drop: {4}\n" +
                    "🖥️ Affected Devices: {5}\n\n" +
                    "_KWH Monitoring System_",
                    oneHourAgo, DateTime.Now, anomalies.Count, overloadCount, dropCount, deviceCount);
                await SendWablasAsync(whatsappMessage);
            }

            _logger.LogInformation("Hourly report sent successfully");
        }

        // ============================================
        // REALTIME HOURLY REPORT (untuk test)
        // Mengirim laporan 1 jam terakhir dari saat ini
        // ============================================
        public async Task SendRealtimeHourlyReportAsync(bool isTest = true)
        {
            if (!_settings.EnableEmailNotification && !_settings.EnableWhatsAppNotification)
                return;

            // Skip if hourly report is disabled (unless it's a test call)
            if (!isTest && !_settings.SendHourlyReport)
                return;

            var now = DateTime.Now;
            var oneHourAgo = now.AddHours(-1);

            // Ambil data panel terbaru
            var latestPanels = await _context.KWH_Monitoring
                .GroupBy(x => x.DeviceKey)
                .Select(g => g.OrderByDescending(x => x.Waktu_Server).FirstOrDefault())
                .ToListAsync();

            var validPanels = latestPanels.Where(x => x != null).ToList();

            // Ambil anomaly log 1 jam terakhir
            var anomalies = await _context.AnomalyLogs
                .Where(x => x.DetectedTime >= oneHourAgo)
                .OrderByDescending(x => x.DetectedTime)
                .ToListAsync();

            var overloadCount = anomalies.Count(x => x.AnomalyType == "OVERLOAD");
            var dropCount = anomalies.Count(x => x.AnomalyType == "DROP");
            var deviceCount = anomalies.Select(x => x.DeviceKey).Distinct().Count();

            var totalDaya = validPanels.Sum(x => x.Daya_Watt);
            var totalEnergy = validPanels.Sum(x => x.Total_Energy_Wh);
            var avgPowerFactor = validPanels.Any() ? validPanels.Average(x => x.Cos_Phi) : 0;
            var avgVoltage = validPanels.Any() ? validPanels.Average(x => x.AvgVoltage) : 0;
            var avgFrequency = validPanels.Any() ? validPanels.Average(x => x.Frekuensi_Hz) : 0;

            // Calculate cost from aggregated energy data (same as Usage Statistics)
            var tariffPerKWh = await GetTariffPerKWhAsync();
            var startOfToday = now.Date;

            // Hourly report: current hour kWh + today kWh
            var currentHourStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);
            var hourEnergy = await _context.HourlyEnergy
                .Where(x => x.Hour >= currentHourStart && x.Hour < currentHourStart.AddHours(1))
                .SumAsync(x => x.EnergyKWh);
            var hourKWh = Math.Round(hourEnergy, 2);

            var todayHourlyEnergy = await _context.HourlyEnergy
                .Where(x => x.Hour >= startOfToday && x.Hour < startOfToday.AddDays(1))
                .SumAsync(x => x.EnergyKWh);
            var todayKWh = Math.Round(todayHourlyEnergy, 2);

            var estimatedCostToday = Math.Round(todayKWh * tariffPerKWh, 2);

            // Load thresholds once (not per-panel)
            var (maxCap, mediumThresh, normalThresh) = await LoadThresholdsAsync();

            // Build panel detail rows
            var panelRows = new StringBuilder();
            foreach (var panel in validPanels.Take(20)) // Max 20 panels
            {
                var loadPercent = Math.Min((panel.Daya_Watt / maxCap) * 100, 100);
                var statusColor = loadPercent > mediumThresh ? "#dc3545" : loadPercent > normalThresh ? "#ffc107" : "#198754";
                var statusText = loadPercent > mediumThresh ? "HIGH" : loadPercent > normalThresh ? "MEDIUM" : "NORMAL";
                panelRows.Append($"<tr style='border-bottom: 1px solid #eee;'>");
                panelRows.Append($"<td style='padding: 8px; font-weight: bold;'>{panel.GroupName}</td>");
                panelRows.Append($"<td style='padding: 8px; text-align: center;'>{panel.Daya_Watt:N0} W</td>");
                panelRows.Append($"<td style='padding: 8px; text-align: center;'>{panel.Volt_R:N0} V</td>");
                panelRows.Append($"<td style='padding: 8px; text-align: center;'>{panel.Amp_R:N0} A</td>");
                panelRows.Append($"<td style='padding: 8px; text-align: center;'>{panel.Cos_Phi:N2}</td>");
                panelRows.Append($"<td style='padding: 8px; text-align: center;'><span style='background-color: {statusColor}; color: white; padding: 2px 8px; border-radius: 4px; font-size: 11px;'>{statusText}</span></td>");
                panelRows.Append($"</tr>");
            }

            var subject = $"⚡ Hourly Report - {now:dd/MM/yyyy HH:mm}";
            var message = $@"
                <div style='font-family: Arial, sans-serif; max-width: 800px; margin: 0 auto;'>
                    <!-- Header -->
                    <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 25px; text-align: center;'>
                        <h1 style='margin: 0; font-size: 24px;'>⚡ KWH MONITORING</h1>
                        <p style='margin: 5px 0 0 0; font-size: 16px; opacity: 0.9;'>Hourly Report - Realtime Dashboard</p>
                    </div>

                    <!-- Report Info -->
                    <div style='padding: 20px; background-color: #f8f9fa; border-bottom: 3px solid #667eea;'>
                        <p style='margin: 0; font-size: 14px;'><strong>📅 Period:</strong> {oneHourAgo:dd/MM/yyyy HH:mm} - {now:dd/MM/yyyy HH:mm}</p>
                        <p style='margin: 5px 0 0 0; font-size: 14px;'><strong>🕐 Generated:</strong> {now:dd/MM/yyyy HH:mm:ss}</p>
                    </div>

                    <!-- Summary Cards -->
                    <div style='padding: 20px;'>
                        <h3 style='color: #333; border-bottom: 2px solid #667eea; padding-bottom: 10px;'>📊 Summary Dashboard</h3>
                        <table style='width: 100%; border-collapse: collapse;'>
                            <tr>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Active Panels</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{validPanels.Count}</div>
                                    </div>
                                </td>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Total Power</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{totalDaya:N0} W</div>
                                    </div>
                                </td>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Total Energy</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{totalEnergy:N0} Wh</div>
                                    </div>
                                </td>
                            </tr>
                            <tr>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #43e97b 0%, #38f9d7 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Avg Power Factor</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{Math.Round(avgPowerFactor, 2)}</div>
                                    </div>
                                </td>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #fa709a 0%, #fee140 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Avg Voltage</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{Math.Round(avgVoltage)} V</div>
                                    </div>
                                </td>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #a8edea 0%, #fed6e3 100%); color: #333; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Avg Frequency</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{Math.Round(avgFrequency)} Hz</div>
                                    </div>
                                </td>
                            </tr>
                            <tr>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #56ab2f 0%, #a8e063 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>kWh (1h)</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{hourKWh:N2}</div>
                                    </div>
                                </td>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #56ab2f 0%, #a8e063 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Today kWh</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{todayKWh:N2}</div>
                                    </div>
                                </td>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #f7971e 0%, #ffd200 100%); color: #333; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Est. Cost (Today)</div>
                                        <div style='font-size: 28px; font-weight: bold;'>Rp {estimatedCostToday:N0}</div>
                                    </div>
                                </td>
                            </tr>
                        </table>

                        <!-- Anomaly Summary -->
                        <div style='margin-top: 20px; padding: 15px; background-color: #fff3cd; border-left: 4px solid #ffc107; border-radius: 5px;'>
                            <h4 style='margin: 0 0 10px 0; color: #856404;'>⚠️ Anomaly Summary (Last 1 Hour)</h4>
                            <table style='width: 100%;'>
                                <tr>
                                    <td style='padding: 5px;'><strong>Total Anomalies:</strong></td>
                                    <td style='padding: 5px; text-align: right;'><span style='background-color: #dc3545; color: white; padding: 3px 10px; border-radius: 12px;'>{anomalies.Count}</span></td>
                                    <td style='padding: 5px;'><strong>Overload:</strong></td>
                                    <td style='padding: 5px; text-align: right;'><span style='background-color: #dc3545; color: white; padding: 3px 10px; border-radius: 12px;'>{overloadCount}</span></td>
                                </tr>
                                <tr>
                                    <td style='padding: 5px;'><strong>Device Drop:</strong></td>
                                    <td style='padding: 5px; text-align: right;'><span style='background-color: #ffc107; color: #333; padding: 3px 10px; border-radius: 12px;'>{dropCount}</span></td>
                                    <td style='padding: 5px;'><strong>Affected Devices:</strong></td>
                                    <td style='padding: 5px; text-align: right;'><span style='background-color: #17a2b8; color: white; padding: 3px 10px; border-radius: 12px;'>{deviceCount}</span></td>
                                </tr>
                            </table>
                        </div>

                        <!-- Panel Details Table -->
                        <h3 style='color: #333; border-bottom: 2px solid #667eea; padding-bottom: 10px; margin-top: 25px;'>📋 Panel Details</h3>
                        <table style='width: 100%; border-collapse: collapse; font-size: 13px;'>
                            <thead>
                                <tr style='background-color: #667eea; color: white;'>
                                    <th style='padding: 10px; text-align: left;'>Device</th>
                                    <th style='padding: 10px; text-align: center;'>Power</th>
                                    <th style='padding: 10px; text-align: center;'>Voltage</th>
                                    <th style='padding: 10px; text-align: center;'>Current</th>
                                    <th style='padding: 10px; text-align: center;'>PF</th>
                                    <th style='padding: 10px; text-align: center;'>Status</th>
                                </tr>
                            </thead>
                            <tbody>
                                {panelRows}
                            </tbody>
                        </table>
                    </div>

                    <!-- Footer -->
                    <div style='background-color: #343a40; color: white; padding: 15px; text-align: center; font-size: 12px;'>
                        <p style='margin: 0;'>⚡ KWH Monitoring System - Automated Hourly Report</p>
                        <p style='margin: 5px 0 0 0; opacity: 0.7;'>Generated at {now:dd/MM/yyyy HH:mm:ss}</p>
                    </div>
                </div>";

            if (_settings.EnableEmailNotification)
            {
                await SendEmailAsync(subject, message);
            }

            if (_settings.EnableWhatsAppNotification)
            {
                var whatsappMessage = string.Format(
                    "⚡ *{0}HOURLY REPORT*\n\n" +
                    "📅 Period: {1:HH:mm} - {2:HH:mm}\n\n" +
                    "━━━━━━━━━━━━━━━━━━\n" +
                    "📊 *SUMMARY DASHBOARD*\n" +
                    "━━━━━━━━━━━━━━━━━━\n\n" +
                    "🖥️ Active Panels: *{3}*\n" +
                    "⚡ Total Power: *{4:N0} W*\n" +
                    "🔋 Total Energy: *{5:N0} Wh*\n" +
                    "📏 Avg PF: *{6:N2}*\n" +
                    "🔌 Avg Voltage: *{7:N0} V*\n" +
                    "🔄 Avg Frequency: *{8:N0} Hz*\n" +
                    "⚡ kWh (1h): *{9:N2}*\n" +
                    "📅 Today: *{10:N2} kWh*\n" +
                    "💰 Est. Cost (Today): *Rp {11:N0}*\n\n" +
                    "━━━━━━━━━━━━━━━━━━\n" +
                    "⚠️ *ANOMALY (1h)*\n" +
                    "━━━━━━━━━━━━━━━━━━\n\n" +
                    "📊 Total: *{12}*\n" +
                    "🔴 Overload: *{13}*\n" +
                    "🟡 Drop: *{14}*\n" +
                    "🖥️ Devices: *{15}*\n\n" +
                    "_KWH Monitoring System_",
                    isTest ? "[TEST] " : "", oneHourAgo, now, validPanels.Count, totalDaya, totalEnergy, avgPowerFactor,
                    avgVoltage, avgFrequency, hourKWh, todayKWh, estimatedCostToday,
                    anomalies.Count, overloadCount, dropCount, deviceCount);
                await SendWablasAsync(whatsappMessage);
            }

            _logger.LogInformation("Realtime hourly report sent successfully");
        }

        // ============================================
        // DAILY REPORT
        // [UNUSED] - Versi non-realtime. Semua pemanggilan menggunakan
        // SendRealtimeDailyReportAsync() yang lebih lengkap.
        // ============================================
        public async Task SendDailyReportAsync()
        {
            if (!_settings.EnableEmailNotification && !_settings.EnableWhatsAppNotification)
                return;

            if (!_settings.SendDailyReport)
                return;

            var yesterday = DateTime.Now.AddDays(-1);
            var startOfYesterday = yesterday.Date;
            var endOfYesterday = startOfYesterday.AddDays(1);

            var anomalies = await _context.AnomalyLogs
                .Where(x => x.DetectedTime >= startOfYesterday && x.DetectedTime < endOfYesterday)
                .OrderByDescending(x => x.DetectedTime)
                .ToListAsync();

            var overloadCount = anomalies.Count(x => x.AnomalyType == "OVERLOAD");
            var dropCount = anomalies.Count(x => x.AnomalyType == "DROP");
            var deviceCount = anomalies.Select(x => x.DeviceKey).Distinct().Count();

            var subject = string.Format("Daily Report - {0:dd/MM/yyyy}", yesterday);
            var message = string.Format(@"
                <h2>Daily Monitoring Report</h2>
                <p><strong>Date:</strong> {0:dddd, dd MMMM yyyy}</p>
                <hr>
                <h3>Summary</h3>
                <ul>
                    <li><strong>Total Anomalies:</strong> {1}</li>
                    <li><strong>Overload Events:</strong> {2}</li>
                    <li><strong>Device Drop Events:</strong> {3}</li>
                    <li><strong>Affected Devices:</strong> {4}</li>
                </ul>
                <hr>
                <p><em>This is an automated report from KWH Monitoring System</em></p>
            ", yesterday, anomalies.Count, overloadCount, dropCount, deviceCount);

            if (_settings.EnableEmailNotification)
            {
                await SendEmailAsync(subject, message);
            }

            if (_settings.EnableWhatsAppNotification)
            {
                var whatsappMessage = string.Format(
                    "📋 *DAILY REPORT*\n\n" +
                    "📅 Date: {0:dddd, dd MMMM yyyy}\n\n" +
                    "📊 Total Anomalies: {1}\n" +
                    "🔴 Overload: {2}\n" +
                    "🟡 Device Drop: {3}\n" +
                    "🖥️ Affected Devices: {4}\n\n" +
                    "_KWH Monitoring System_",
                    yesterday, anomalies.Count, overloadCount, dropCount, deviceCount);
                await SendWablasAsync(whatsappMessage);
            }

            _logger.LogInformation("Daily report sent successfully");
        }

        public async Task SendRealtimeDailyReportAsync(bool isTest = true)
        {
            if (!_settings.EnableEmailNotification && !_settings.EnableWhatsAppNotification)
                return;

            // Skip if daily report is disabled (unless it's a test call)
            if (!isTest && !_settings.SendDailyReport)
                return;

            var now = DateTime.Now;
            var startOfToday = now.Date;

            // Ambil data panel terbaru
            var latestPanels = await _context.KWH_Monitoring
                .GroupBy(x => x.DeviceKey)
                .Select(g => g.OrderByDescending(x => x.Waktu_Server).FirstOrDefault())
                .ToListAsync();

            var validPanels = latestPanels.Where(x => x != null).ToList();

            // Ambil anomaly log hari ini
            var anomalies = await _context.AnomalyLogs
                .Where(x => x.DetectedTime >= startOfToday)
                .OrderByDescending(x => x.DetectedTime)
                .ToListAsync();

            var overloadCount = anomalies.Count(x => x.AnomalyType == "OVERLOAD");
            var dropCount = anomalies.Count(x => x.AnomalyType == "DROP");
            var deviceCount = anomalies.Select(x => x.DeviceKey).Distinct().Count();

            var totalDaya = validPanels.Sum(x => x.Daya_Watt);
            var totalEnergy = validPanels.Sum(x => x.Total_Energy_Wh);
            var totalEnergiAktif = validPanels.Sum(x => x.Energi_Aktif_Wh);
            var totalW1M = validPanels.Sum(x => x.TotalW1M_Wh);
            var avgPowerFactor = validPanels.Any() ? validPanels.Average(x => x.Cos_Phi) : 0;
            var avgVoltage = validPanels.Any() ? validPanels.Average(x => x.AvgVoltage) : 0;
            var avgFrequency = validPanels.Any() ? validPanels.Average(x => x.Frekuensi_Hz) : 0;
            var tariffPerKWh = await GetTariffPerKWhAsync();

            // Calculate cost from aggregated energy data (same as Usage Statistics)
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var monthEnd = monthStart.AddMonths(1);

            var todayHourlyEnergy = await _context.HourlyEnergy
                .Where(x => x.Hour >= startOfToday && x.Hour < startOfToday.AddDays(1))
                .SumAsync(x => x.EnergyKWh);
            var todayKWh = Math.Round(todayHourlyEnergy, 2);

            var monthDailyEnergy = await _context.DailyEnergy
                .Where(x => x.Date >= monthStart && x.Date < monthEnd)
                .SumAsync(x => x.EnergyKWh);
            var monthKWh = Math.Round(monthDailyEnergy, 2);

            var estimatedCostMonth = Math.Round(monthKWh * tariffPerKWh, 2);

            // Load thresholds once (not per-panel)
            var (maxCap, mediumThresh, normalThresh) = await LoadThresholdsAsync();

            // Build panel detail rows
            var panelRows = new StringBuilder();
            foreach (var panel in validPanels.Take(20)) // Max 20 panels
            {
                var loadPercent = Math.Min((panel.Daya_Watt / maxCap) * 100, 100);
                var statusColor = loadPercent > mediumThresh ? "#dc3545" : loadPercent > normalThresh ? "#ffc107" : "#198754";
                var statusText = loadPercent > mediumThresh ? "HIGH" : loadPercent > normalThresh ? "MEDIUM" : "NORMAL";
                panelRows.Append($"<tr style='border-bottom: 1px solid #eee;'>");
                panelRows.Append($"<td style='padding: 8px; font-weight: bold;'>{panel.GroupName}</td>");
                panelRows.Append($"<td style='padding: 8px; text-align: center;'>{panel.Daya_Watt:N0} W</td>");
                panelRows.Append($"<td style='padding: 8px; text-align: center;'>{panel.Volt_R:N0} V</td>");
                panelRows.Append($"<td style='padding: 8px; text-align: center;'>{panel.Amp_R:N0} A</td>");
                panelRows.Append($"<td style='padding: 8px; text-align: center;'>{panel.Cos_Phi:N2}</td>");
                panelRows.Append($"<td style='padding: 8px; text-align: center;'>{Math.Round(panel.Frekuensi_Hz)} Hz</td>");
                panelRows.Append($"<td style='padding: 8px; text-align: center;'><span style='background-color: {statusColor}; color: white; padding: 2px 8px; border-radius: 4px; font-size: 11px;'>{statusText}</span></td>");
                panelRows.Append($"</tr>");
            }

            var subject = $"⚡ Daily Report - {now:dd/MM/yyyy}";
            var message = $@"
                <div style='font-family: Arial, sans-serif; max-width: 800px; margin: 0 auto;'>
                    <!-- Header -->
                    <div style='background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%); color: white; padding: 25px; text-align: center;'>
                        <h1 style='margin: 0; font-size: 24px;'>⚡ KWH MONITORING</h1>
                        <p style='margin: 5px 0 0 0; font-size: 16px; opacity: 0.9;'>Daily Report - Realtime Dashboard</p>
                    </div>

                    <!-- Report Info -->
                    <div style='padding: 20px; background-color: #f8f9fa; border-bottom: 3px solid #11998e;'>
                        <p style='margin: 0; font-size: 14px;'><strong>📅 Date:</strong> {now:dddd, dd MMMM yyyy}</p>
                        <p style='margin: 5px 0 0 0; font-size: 14px;'><strong>🕐 Generated:</strong> {now:HH:mm:ss} (Realtime)</p>
                    </div>

                    <!-- Summary Cards -->
                    <div style='padding: 20px;'>
                        <h3 style='color: #333; border-bottom: 2px solid #11998e; padding-bottom: 10px;'>📊 Summary Dashboard</h3>
                        <table style='width: 100%; border-collapse: collapse;'>
                            <tr>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Active Panels</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{validPanels.Count}</div>
                                    </div>
                                </td>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #fc4a1a 0%, #f7b733 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Total Power</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{totalDaya:N0} W</div>
                                    </div>
                                </td>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #5f2c82 0%, #49a09d 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Total Energy</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{totalEnergy:N0} Wh</div>
                                    </div>
                                </td>
                            </tr>
                            <tr>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #00c6ff 0%, #0072ff 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Aktif Energy</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{totalEnergiAktif:N0} Wh</div>
                                    </div>
                                </td>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #f12711 0%, #f5af19 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Total W1M</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{totalW1M:N0} Wh</div>
                                    </div>
                                </td>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #c0392b 0%, #8e44ad 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Avg Power Factor</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{Math.Round(avgPowerFactor, 2)}</div>
                                    </div>
                                </td>
                            </tr>
                            <tr>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #ff9966 0%, #ff5e62 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Avg Voltage</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{Math.Round(avgVoltage)} V</div>
                                    </div>
                                </td>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #00b4db 0%, #0083b0 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Avg Frequency</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{Math.Round(avgFrequency)} Hz</div>
                                    </div>
                                </td>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #56ab2f 0%, #a8e063 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Today kWh</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{todayKWh:N2}</div>
                                    </div>
                                </td>
                            </tr>
                            <tr>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #56ab2f 0%, #a8e063 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Month kWh</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{monthKWh:N2}</div>
                                    </div>
                                </td>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #f7971e 0%, #ffd200 100%); color: #333; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Est. Cost (Month)</div>
                                        <div style='font-size: 28px; font-weight: bold;'>Rp {estimatedCostMonth:N0}</div>
                                    </div>
                                </td>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Tariff/kWh</div>
                                        <div style='font-size: 28px; font-weight: bold;'>Rp {tariffPerKWh:N0}</div>
                                    </div>
                                </td>
                            </tr>
                        </table>

                        <!-- Anomaly Summary -->
                        <div style='margin-top: 20px; padding: 15px; background-color: #fff3cd; border-left: 4px solid #ffc107; border-radius: 5px;'>
                            <h4 style='margin: 0 0 10px 0; color: #856404;'>⚠️ Anomaly Summary (Today)</h4>
                            <table style='width: 100%;'>
                                <tr>
                                    <td style='padding: 5px;'><strong>Total Anomalies:</strong></td>
                                    <td style='padding: 5px; text-align: right;'><span style='background-color: #dc3545; color: white; padding: 3px 10px; border-radius: 12px;'>{anomalies.Count}</span></td>
                                    <td style='padding: 5px;'><strong>Overload:</strong></td>
                                    <td style='padding: 5px; text-align: right;'><span style='background-color: #dc3545; color: white; padding: 3px 10px; border-radius: 12px;'>{overloadCount}</span></td>
                                </tr>
                                <tr>
                                    <td style='padding: 5px;'><strong>Device Drop:</strong></td>
                                    <td style='padding: 5px; text-align: right;'><span style='background-color: #ffc107; color: #333; padding: 3px 10px; border-radius: 12px;'>{dropCount}</span></td>
                                    <td style='padding: 5px;'><strong>Affected Devices:</strong></td>
                                    <td style='padding: 5px; text-align: right;'><span style='background-color: #17a2b8; color: white; padding: 3px 10px; border-radius: 12px;'>{deviceCount}</span></td>
                                </tr>
                            </table>
                        </div>

                        <!-- Panel Details Table -->
                        <h3 style='color: #333; border-bottom: 2px solid #11998e; padding-bottom: 10px; margin-top: 25px;'>📋 Panel Details</h3>
                        <table style='width: 100%; border-collapse: collapse; font-size: 13px;'>
                            <thead>
                                <tr style='background-color: #11998e; color: white;'>
                                    <th style='padding: 10px; text-align: left;'>Device</th>
                                    <th style='padding: 10px; text-align: center;'>Power</th>
                                    <th style='padding: 10px; text-align: center;'>Voltage</th>
                                    <th style='padding: 10px; text-align: center;'>Current</th>
                                    <th style='padding: 10px; text-align: center;'>PF</th>
                                    <th style='padding: 10px; text-align: center;'>Freq</th>
                                    <th style='padding: 10px; text-align: center;'>Status</th>
                                </tr>
                            </thead>
                            <tbody>
                                {panelRows}
                            </tbody>
                        </table>
                    </div>

                    <!-- Footer -->
                    <div style='background-color: #343a40; color: white; padding: 15px; text-align: center; font-size: 12px;'>
                        <p style='margin: 0;'>⚡ KWH Monitoring System - Automated Daily Report</p>
                        <p style='margin: 5px 0 0 0; opacity: 0.7;'>Generated at {now:dd/MM/yyyy HH:mm:ss}</p>
                    </div>
                </div>";

            if (_settings.EnableEmailNotification)
            {
                await SendEmailAsync(subject, message);
            }

            if (_settings.EnableWhatsAppNotification)
            {
                var whatsappMessage = string.Format(
                    "⚡ *{0}DAILY REPORT*\n\n" +
                    "📅 Date: *{1:dddd, dd MMMM yyyy}*\n" +
                    "🕐 Time: {1:HH:mm:ss} (Realtime)\n\n" +
                    "━━━━━━━━━━━━━━━━━━\n" +
                    "📊 *SUMMARY DASHBOARD*\n" +
                    "━━━━━━━━━━━━━━━━━━\n\n" +
                    "🖥️ Active Panels: *{2}*\n" +
                    "⚡ Total Power: *{3:N0} W*\n" +
                    "🔋 Total Energy: *{4:N0} Wh*\n" +
                    "🔌 Aktif Energy: *{5:N0} Wh*\n" +
                    "📊 Total W1M: *{6:N0} Wh*\n" +
                    "📏 Avg PF: *{7:N2}*\n" +
                    "🔌 Avg Voltage: *{8:N0} V*\n" +
                    "🔄 Avg Freq: *{9:N0} Hz*\n" +
                    "⚡ Today: *{10:N2} kWh*\n" +
                    "📅 Month: *{11:N2} kWh*\n" +
                    "💰 Est. Cost (Month): *Rp {12:N0}*\n\n" +
                    "━━━━━━━━━━━━━━━━━━\n" +
                    "⚠️ *ANOMALY (TODAY)*\n" +
                    "━━━━━━━━━━━━━━━━━━\n\n" +
                    "📊 Total: *{13}*\n" +
                    "🔴 Overload: *{14}*\n" +
                    "🟡 Drop: *{15}*\n" +
                    "🖥️ Devices: *{16}*\n\n" +
                    "_KWH Monitoring System_",
                    isTest ? "[TEST] " : "", now, validPanels.Count, totalDaya, totalEnergy, totalEnergiAktif, totalW1M, avgPowerFactor,
                    avgVoltage, avgFrequency, todayKWh, monthKWh, estimatedCostMonth,
                    anomalies.Count, overloadCount, dropCount, deviceCount);
                await SendWablasAsync(whatsappMessage);
            }

            _logger.LogInformation("Realtime daily report sent successfully");
        }

        // ============================================
        // MONTHLY REPORT (scheduled)
        // [UNUSED] - Versi non-realtime. Semua pemanggilan menggunakan
        // SendRealtimeMonthlyReportAsync() yang lebih lengkap.
        // ============================================
        public async Task SendMonthlyReportAsync()
        {
            if (!_settings.EnableEmailNotification && !_settings.EnableWhatsAppNotification)
                return;

            if (!_settings.SendMonthlyReport)
                return;

            var now = DateTime.Now;
            var firstDayOfMonth = new DateTime(now.Year, now.Month, 1);
            var firstDayOfPrevMonth = firstDayOfMonth.AddMonths(-1);
            var monthName = firstDayOfPrevMonth.ToString("MMMM yyyy");

            var anomalies = await _context.AnomalyLogs
                .Where(x => x.DetectedTime >= firstDayOfPrevMonth && x.DetectedTime < firstDayOfMonth)
                .OrderByDescending(x => x.DetectedTime)
                .ToListAsync();

            var overloadCount = anomalies.Count(x => x.AnomalyType == "OVERLOAD");
            var dropCount = anomalies.Count(x => x.AnomalyType == "DROP");
            var deviceCount = anomalies.Select(x => x.DeviceKey).Distinct().Count();

            var subject = string.Format("Monthly Report - {0}", monthName);
            var message = string.Format(@"
                <h2>Monthly Monitoring Report</h2>
                <p><strong>Period:</strong> {0}</p>
                <hr>
                <h3>Summary</h3>
                <ul>
                    <li><strong>Total Anomalies:</strong> {1}</li>
                    <li><strong>Overload Events:</strong> {2}</li>
                    <li><strong>Device Drop Events:</strong> {3}</li>
                    <li><strong>Affected Devices:</strong> {4}</li>
                </ul>
                <hr>
                <p><em>This is an automated report from KWH Monitoring System</em></p>
            ", monthName, anomalies.Count, overloadCount, dropCount, deviceCount);

            if (_settings.EnableEmailNotification)
            {
                await SendEmailAsync(subject, message);
            }

            if (_settings.EnableWhatsAppNotification)
            {
                var whatsappMessage = string.Format(
                    "📋 *MONTHLY REPORT*\n\n" +
                    "📅 Period: {0}\n\n" +
                    "📊 Total Anomalies: {1}\n" +
                    "🔴 Overload: {2}\n" +
                    "🟡 Device Drop: {3}\n" +
                    "🖥️ Affected Devices: {4}\n\n" +
                    "_KWH Monitoring System_",
                    monthName, anomalies.Count, overloadCount, dropCount, deviceCount);
                await SendWablasAsync(whatsappMessage);
            }

            _logger.LogInformation("Monthly report sent successfully");
        }

        // ============================================
        // REALTIME MONTHLY REPORT (untuk test)
        // Mengirim laporan bulan ini (dari tanggal 1 sampai sekarang)
        // ============================================
        public async Task SendRealtimeMonthlyReportAsync(bool isTest = true)
        {
            if (!_settings.EnableEmailNotification && !_settings.EnableWhatsAppNotification)
                return;

            // Skip if monthly report is disabled (unless it's a test call)
            if (!isTest && !_settings.SendMonthlyReport)
                return;

            var now = DateTime.Now;
            var firstDayOfMonth = new DateTime(now.Year, now.Month, 1);
            var firstDayOfPrevMonth = firstDayOfMonth.AddMonths(-1);
            // Laporan bulanan: data 1 bulan kebelakang (dari tanggal 1 bulan lalu sampai tanggal 1 bulan sekarang)
            var reportStart = firstDayOfPrevMonth;
            var reportEnd = firstDayOfMonth;
            var monthName = firstDayOfPrevMonth.ToString("MMMM yyyy");

            // Ambil data panel terbaru
            var latestPanels = await _context.KWH_Monitoring
                .GroupBy(x => x.DeviceKey)
                .Select(g => g.OrderByDescending(x => x.Waktu_Server).FirstOrDefault())
                .ToListAsync();

            var validPanels = latestPanels.Where(x => x != null).ToList();

            // Ambil anomaly log 1 bulan kebelakang
            var anomalies = await _context.AnomalyLogs
                .Where(x => x.DetectedTime >= reportStart && x.DetectedTime < reportEnd)
                .OrderByDescending(x => x.DetectedTime)
                .ToListAsync();

            var overloadCount = anomalies.Count(x => x.AnomalyType == "OVERLOAD");
            var dropCount = anomalies.Count(x => x.AnomalyType == "DROP");
            var deviceCount = anomalies.Select(x => x.DeviceKey).Distinct().Count();

            var totalDaya = validPanels.Sum(x => x.Daya_Watt);
            var totalEnergy = validPanels.Sum(x => x.Total_Energy_Wh);
            var totalEnergiAktif = validPanels.Sum(x => x.Energi_Aktif_Wh);
            var totalW1M = validPanels.Sum(x => x.TotalW1M_Wh);
            var avgPowerFactor = validPanels.Any() ? validPanels.Average(x => x.Cos_Phi) : 0;
            var avgVoltage = validPanels.Any() ? validPanels.Average(x => x.AvgVoltage) : 0;
            var avgFrequency = validPanels.Any() ? validPanels.Average(x => x.Frekuensi_Hz) : 0;
            var tariffPerKWh = await GetTariffPerKWhAsync();

            // Calculate cost from aggregated energy data for the previous month
            var monthDailyEnergy = await _context.DailyEnergy
                .Where(x => x.Date >= reportStart && x.Date < reportEnd)
                .SumAsync(x => x.EnergyKWh);
            var monthKWh = Math.Round(monthDailyEnergy, 2);

            var prevMonthYear = firstDayOfPrevMonth.Year;
            var yearMonthlyEnergy = await _context.MonthlyEnergy
                .Where(x => x.Year == prevMonthYear)
                .SumAsync(x => x.EnergyKWh);
            var yearKWh = Math.Round(yearMonthlyEnergy, 2);

            var estimatedCostMonth = Math.Round(monthKWh * tariffPerKWh, 2);

            // Load thresholds once (not per-panel)
            var (maxCap, mediumThresh, normalThresh) = await LoadThresholdsAsync();

            // Build panel detail rows
            var panelRows = new StringBuilder();
            foreach (var panel in validPanels.Take(20))
            {
                var loadPercent = Math.Min((panel.Daya_Watt / maxCap) * 100, 100);
                var statusColor = loadPercent > mediumThresh ? "#dc3545" : loadPercent > normalThresh ? "#ffc107" : "#198754";
                var statusText = loadPercent > mediumThresh ? "HIGH" : loadPercent > normalThresh ? "MEDIUM" : "NORMAL";
                panelRows.Append($"<tr style='border-bottom: 1px solid #eee;'>");
                panelRows.Append($"<td style='padding: 8px; font-weight: bold;'>{panel.GroupName}</td>");
                panelRows.Append($"<td style='padding: 8px; text-align: center;'>{panel.Daya_Watt:N0} W</td>");
                panelRows.Append($"<td style='padding: 8px; text-align: center;'>{panel.Volt_R:N0} V</td>");
                panelRows.Append($"<td style='padding: 8px; text-align: center;'>{panel.Amp_R:N0} A</td>");
                panelRows.Append($"<td style='padding: 8px; text-align: center;'>{panel.Cos_Phi:N2}</td>");
                panelRows.Append($"<td style='padding: 8px; text-align: center;'>{Math.Round(panel.Frekuensi_Hz)} Hz</td>");
                panelRows.Append($"<td style='padding: 8px; text-align: center;'><span style='background-color: {statusColor}; color: white; padding: 2px 8px; border-radius: 4px; font-size: 11px;'>{statusText}</span></td>");
                panelRows.Append($"</tr>");
            }

            var daysInPrevMonth = DateTime.DaysInMonth(firstDayOfPrevMonth.Year, firstDayOfPrevMonth.Month);

            var subject = $"⚡ Monthly Report - {monthName}";
            var message = $@"
                <div style='font-family: Arial, sans-serif; max-width: 800px; margin: 0 auto;'>
                    <!-- Header -->
                    <div style='background: linear-gradient(135deg, #e65100 0%, #ff6d00 100%); color: white; padding: 25px; text-align: center;'>
                        <h1 style='margin: 0; font-size: 24px;'>⚡ KWH MONITORING</h1>
                        <p style='margin: 5px 0 0 0; font-size: 16px; opacity: 0.9;'>Monthly Report - {monthName}</p>
                    </div>

                    <!-- Report Info -->
                    <div style='padding: 20px; background-color: #f8f9fa; border-bottom: 3px solid #e65100;'>
                        <p style='margin: 0; font-size: 14px;'><strong>📅 Period:</strong> {reportStart:dd/MM/yyyy} - {reportEnd:dd/MM/yyyy}</p>
                        <p style='margin: 5px 0 0 0; font-size: 14px;'><strong>🕐 Generated:</strong> {now:dd/MM/yyyy HH:mm:ss}</p>
                        <p style='margin: 5px 0 0 0; font-size: 14px;'><strong>📊 Total Days:</strong> {daysInPrevMonth} days</p>
                    </div>

                    <!-- Summary Cards -->
                    <div style='padding: 20px;'>
                        <h3 style='color: #333; border-bottom: 2px solid #e65100; padding-bottom: 10px;'>📊 Summary Dashboard</h3>
                        <table style='width: 100%; border-collapse: collapse;'>
                            <tr>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #e65100 0%, #ff6d00 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Active Panels</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{validPanels.Count}</div>
                                    </div>
                                </td>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #fc4a1a 0%, #f7b733 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Total Power</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{totalDaya:N0} W</div>
                                    </div>
                                </td>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #5f2c82 0%, #49a09d 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Total Energy</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{totalEnergy:N0} Wh</div>
                                    </div>
                                </td>
                            </tr>
                            <tr>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #00c6ff 0%, #0072ff 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Aktif Energy</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{totalEnergiAktif:N0} Wh</div>
                                    </div>
                                </td>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #f12711 0%, #f5af19 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Total W1M</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{totalW1M:N0} Wh</div>
                                    </div>
                                </td>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #c0392b 0%, #8e44ad 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Avg Power Factor</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{Math.Round(avgPowerFactor, 2)}</div>
                                    </div>
                                </td>
                            </tr>
                            <tr>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #ff9966 0%, #ff5e62 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Avg Voltage</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{Math.Round(avgVoltage)} V</div>
                                    </div>
                                </td>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #00b4db 0%, #0083b0 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Avg Frequency</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{Math.Round(avgFrequency)} Hz</div>
                                    </div>
                                </td>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #56ab2f 0%, #a8e063 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Month kWh</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{monthKWh:N2}</div>
                                    </div>
                                </td>
                            </tr>
                            <tr>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #56ab2f 0%, #a8e063 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Year kWh</div>
                                        <div style='font-size: 28px; font-weight: bold;'>{yearKWh:N2}</div>
                                    </div>
                                </td>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #f7971e 0%, #ffd200 100%); color: #333; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Est. Cost (Month)</div>
                                        <div style='font-size: 28px; font-weight: bold;'>Rp {estimatedCostMonth:N0}</div>
                                    </div>
                                </td>
                                <td style='padding: 10px;'>
                                    <div style='background: linear-gradient(135deg, #e65100 0%, #ff6d00 100%); color: white; padding: 20px; border-radius: 10px; text-align: center;'>
                                        <div style='font-size: 12px; opacity: 0.8;'>Tariff/kWh</div>
                                        <div style='font-size: 28px; font-weight: bold;'>Rp {tariffPerKWh:N0}</div>
                                    </div>
                                </td>
                            </tr>
                        </table>

                        <!-- Anomaly Summary -->
                        <div style='margin-top: 20px; padding: 15px; background-color: #fff3cd; border-left: 4px solid #ffc107; border-radius: 5px;'>
                            <h4 style='margin: 0 0 10px 0; color: #856404;'>⚠️ Anomaly Summary ({monthName})</h4>
                            <table style='width: 100%;'>
                                <tr>
                                    <td style='padding: 5px;'><strong>Total Anomalies:</strong></td>
                                    <td style='padding: 5px; text-align: right;'><span style='background-color: #dc3545; color: white; padding: 3px 10px; border-radius: 12px;'>{anomalies.Count}</span></td>
                                    <td style='padding: 5px;'><strong>Overload:</strong></td>
                                    <td style='padding: 5px; text-align: right;'><span style='background-color: #dc3545; color: white; padding: 3px 10px; border-radius: 12px;'>{overloadCount}</span></td>
                                </tr>
                                <tr>
                                    <td style='padding: 5px;'><strong>Device Drop:</strong></td>
                                    <td style='padding: 5px; text-align: right;'><span style='background-color: #ffc107; color: #333; padding: 3px 10px; border-radius: 12px;'>{dropCount}</span></td>
                                    <td style='padding: 5px;'><strong>Affected Devices:</strong></td>
                                    <td style='padding: 5px; text-align: right;'><span style='background-color: #17a2b8; color: white; padding: 3px 10px; border-radius: 12px;'>{deviceCount}</span></td>
                                </tr>
                            </table>
                        </div>

                        <!-- Panel Details Table -->
                        <h3 style='color: #333; border-bottom: 2px solid #e65100; padding-bottom: 10px; margin-top: 25px;'>📋 Panel Details</h3>
                        <table style='width: 100%; border-collapse: collapse; font-size: 13px;'>
                            <thead>
                                <tr style='background-color: #e65100; color: white;'>
                                    <th style='padding: 10px; text-align: left;'>Device</th>
                                    <th style='padding: 10px; text-align: center;'>Power</th>
                                    <th style='padding: 10px; text-align: center;'>Voltage</th>
                                    <th style='padding: 10px; text-align: center;'>Current</th>
                                    <th style='padding: 10px; text-align: center;'>PF</th>
                                    <th style='padding: 10px; text-align: center;'>Freq</th>
                                    <th style='padding: 10px; text-align: center;'>Status</th>
                                </tr>
                            </thead>
                            <tbody>
                                {panelRows}
                            </tbody>
                        </table>
                    </div>

                    <!-- Footer -->
                    <div style='background-color: #343a40; color: white; padding: 15px; text-align: center; font-size: 12px;'>
                        <p style='margin: 0;'>⚡ KWH Monitoring System - Automated Monthly Report</p>
                        <p style='margin: 5px 0 0 0; opacity: 0.7;'>Generated at {now:dd/MM/yyyy HH:mm:ss}</p>
                    </div>
                </div>";

            if (_settings.EnableEmailNotification)
            {
                await SendEmailAsync(subject, message);
            }

            if (_settings.EnableWhatsAppNotification)
            {
                var whatsappMessage = string.Format(
                    "⚡ *{0}MONTHLY REPORT*\n\n" +
                    "📅 Month: *{1}*\n" +
                    "🕐 Period: {2:dd/MM/yyyy} - {3:dd/MM/yyyy}\n" +
                    "📊 Total Days: {4} days\n\n" +
                    "━━━━━━━━━━━━━━━━━━\n" +
                    "📊 *SUMMARY DASHBOARD*\n" +
                    "━━━━━━━━━━━━━━━━━━\n\n" +
                    "🖥️ Active Panels: *{5}*\n" +
                    "⚡ Total Power: *{6:N0} W*\n" +
                    "🔋 Total Energy: *{7:N0} Wh*\n" +
                    "🔌 Aktif Energy: *{8:N0} Wh*\n" +
                    "📊 Total W1M: *{9:N0} Wh*\n" +
                    "📏 Avg PF: *{10:N2}*\n" +
                    "🔌 Avg Voltage: *{11:N0} V*\n" +
                    "🔄 Avg Freq: *{12:N0} Hz*\n" +
                    "⚡ Month: *{13:N2} kWh*\n" +
                    "📅 Year: *{14:N2} kWh*\n" +
                    "💰 Est. Cost (Month): *Rp {15:N0}*\n\n" +
                    "━━━━━━━━━━━━━━━━━━\n" +
                    "⚠️ *ANOMALY ({16})*\n" +
                    "━━━━━━━━━━━━━━━━━━\n\n" +
                    "📊 Total: *{17}*\n" +
                    "🔴 Overload: *{18}*\n" +
                    "🟡 Drop: *{19}*\n" +
                    "🖥️ Devices: *{20}*\n\n" +
                    "_KWH Monitoring System_",
                    isTest ? "[TEST] " : "", monthName, reportStart, reportEnd, daysInPrevMonth,
                    validPanels.Count, totalDaya, totalEnergy, totalEnergiAktif, totalW1M,
                    avgPowerFactor, avgVoltage, avgFrequency, monthKWh, yearKWh, estimatedCostMonth,
                    monthName, anomalies.Count, overloadCount, dropCount, deviceCount);
                await SendWablasAsync(whatsappMessage);
            }

            _logger.LogInformation("Realtime monthly report sent successfully");
        }

        // ============================================
        // EMAIL SENDER
        // ============================================
        public async Task SendEmailAsync(string subject, string body)
        {
            if (string.IsNullOrEmpty(_settings.SmtpServer) ||
                string.IsNullOrEmpty(_settings.SenderEmail) ||
                string.IsNullOrEmpty(_settings.SenderPassword) ||
                string.IsNullOrEmpty(_settings.RecipientEmail))
            {
                _logger.LogWarning("Email settings not configured");
                return;
            }

            try
            {
                using (var client = new SmtpClient(_settings.SmtpServer, _settings.SmtpPort))
                {
                    client.Credentials = new NetworkCredential(_settings.SenderEmail, _settings.SenderPassword);
                    client.EnableSsl = true;

                    using (var mailMessage = new MailMessage())
                    {
                        mailMessage.From = new MailAddress(_settings.SenderEmail, "KWH Monitoring System");
                        mailMessage.Subject = subject;
                        mailMessage.Body = body;
                        mailMessage.IsBodyHtml = true;

                        // Support multiple recipients (separated by comma or semicolon)
                        var recipients = _settings.RecipientEmail.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var recipient in recipients)
                        {
                            var trimmedEmail = recipient.Trim();
                            if (!string.IsNullOrEmpty(trimmedEmail))
                            {
                                mailMessage.To.Add(trimmedEmail);
                                _logger.LogInformation("Adding email recipient: {0}", trimmedEmail);
                            }
                        }

                        await client.SendMailAsync(mailMessage);
                        _logger.LogInformation("Email sent successfully to {0} recipient(s)", recipients.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email to {0}: {1}", _settings.RecipientEmail, ex.Message);
            }
        }

        // ============================================
        // WABLAS WHATSAPP - SEND TEXT MESSAGE
        // ============================================
        public async Task<WablasResponse> SendWablasAsync(string message, string phone = null)
        {
            var response = new WablasResponse();

            if (string.IsNullOrEmpty(_settings.WablasServerUrl) || string.IsNullOrEmpty(_settings.WablasToken))
            {
                _logger.LogWarning("Wablas settings not configured (ServerUrl or Token is empty)");
                response.Success = false;
                response.Message = "Wablas settings not configured";
                return response;
            }

            var phoneNumbers = new List<string>();
            if (!string.IsNullOrEmpty(phone))
            {
                phoneNumbers.Add(phone);
            }
            else if (_settings.WablasPhoneNumbers != null && _settings.WablasPhoneNumbers.Count > 0)
            {
                phoneNumbers = _settings.WablasPhoneNumbers;
            }
            else
            {
                _logger.LogWarning("No WhatsApp phone numbers configured");
                response.Success = false;
                response.Message = "No WhatsApp phone numbers configured";
                return response;
            }

            // Log the message being sent (truncated for security)
            var msgPreview = message.Length > 100 ? message.Substring(0, 100) + "..." : message;
            _logger.LogInformation("Sending WhatsApp message to {Count} recipients: {Phones}. Message preview: {Msg}", 
                phoneNumbers.Count, string.Join(", ", phoneNumbers), msgPreview);

            try
            {
                var serverUrl = _settings.WablasServerUrl.TrimEnd('/');

                // Build request body for Wablas V2 API
                var data = new
                {
                    data = phoneNumbers.Select(p => new
                    {
                        phone = FormatPhoneNumber(p),
                        message = message,
                        @type = "text"
                    }).ToList()
                };

                var jsonBody = JsonConvert.SerializeObject(data);

                var request = new HttpRequestMessage(HttpMethod.Post, string.Format("{0}/api/v2/send-message", serverUrl));
                request.Headers.Add("Authorization", GetWablasAuthHeader());
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var httpResponse = await _httpClient.SendAsync(request);
                var responseContent = await httpResponse.Content.ReadAsStringAsync();

                response.RawResponse = responseContent;
                response.HttpStatus = (int)httpResponse.StatusCode;

                if (httpResponse.IsSuccessStatusCode)
                {
                    response.Success = true;
                    response.Message = "Message sent successfully";
                    _logger.LogInformation("Wablas message sent successfully to {0} recipient(s): {1}", 
                        phoneNumbers.Count, string.Join(", ", phoneNumbers));
                }
                else
                {
                    // If V2 fails with auth error, try V1 API as fallback for EACH recipient
                    if (httpResponse.StatusCode == HttpStatusCode.Forbidden || httpResponse.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        _logger.LogWarning("Wablas V2 auth failed ({0}), trying V1 API...", httpResponse.StatusCode);

                        // Send to each recipient individually via V1
                        var successCount = 0;
                        foreach (var phoneNumber in phoneNumbers)
                        {
                            var individualResponse = new WablasResponse();
                            await SendWablasV1Async(message, phoneNumber, individualResponse);
                            if (individualResponse.Success) successCount++;
                        }

                        response.Success = successCount > 0;
                        response.Message = $"Sent to {successCount}/{phoneNumbers.Count} recipients via V1 API";
                        return response;
                    }

                    response.Success = false;
                    response.Message = $"Wablas API error: {httpResponse.StatusCode} - {TruncateResponse(responseContent)}";
                    _logger.LogError("Wablas API error ({0}): {1}", httpResponse.StatusCode, responseContent);
                }
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error sending Wablas message: {ex.Message}";
                _logger.LogError(ex, "Error sending WhatsApp messages to: {0}", string.Join(", ", phoneNumbers));
            }

            return response;
        }

        // ============================================
        // WABLAS V1 FALLBACK - Send via Form URL Encoded
        // ============================================
        private async Task<WablasResponse> SendWablasV1Async(string message, string phone, WablasResponse response)
        {
            try
            {
                var serverUrl = _settings.WablasServerUrl.TrimEnd('/');
                var formattedPhone = FormatPhoneNumber(phone);

                _logger.LogInformation("Sending via Wablas V1 to: {0}", formattedPhone);

                // V1 uses query parameter token (V1 hanya butuh token, bukan token.secret_key)
                var url = $"{serverUrl}/api/send-message?token={_settings.WablasToken}";

                var formData = new Dictionary<string, string>
                {
                    { "phone", formattedPhone },
                    { "message", message }
                };

                var content = new FormUrlEncodedContent(formData);
                var httpResponse = await _httpClient.PostAsync(url, content);
                var responseContent = await httpResponse.Content.ReadAsStringAsync();

                response.RawResponse = responseContent;
                response.HttpStatus = (int)httpResponse.StatusCode;

                if (httpResponse.IsSuccessStatusCode)
                {
                    response.Success = true;
                    response.Message = "Message sent successfully via V1 API";
                    _logger.LogInformation("Wablas V1 message sent successfully to: {0}", formattedPhone);
                }
                else
                {
                    response.Success = false;
                    response.Message = $"Wablas V1 API error: {httpResponse.StatusCode} - {TruncateResponse(responseContent)}";
                    _logger.LogError("Wablas V1 API error for {0} ({1}): {2}", formattedPhone, httpResponse.StatusCode, responseContent);
                }
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"Error sending Wablas V1 message: {ex.Message}";
                _logger.LogError(ex, "Error sending WhatsApp V1 to: {0}", phone);
            }

            return response;
        }

        // ============================================
        // WABLAS - SEND IMAGE
        // ============================================
        public async Task<WablasResponse> SendWablasImageAsync(string imageUrl, string caption, string phone = null)
        {
            var response = new WablasResponse();

            if (string.IsNullOrEmpty(_settings.WablasServerUrl) || string.IsNullOrEmpty(_settings.WablasToken))
            {
                response.Success = false;
                response.Message = "Wablas settings not configured";
                return response;
            }

            var phoneNumbers = new List<string>();
            if (!string.IsNullOrEmpty(phone))
            {
                phoneNumbers.Add(phone);
            }
            else if (_settings.WablasPhoneNumbers != null && _settings.WablasPhoneNumbers.Count > 0)
            {
                phoneNumbers = _settings.WablasPhoneNumbers;
            }
            else
            {
                response.Success = false;
                response.Message = "No WhatsApp phone numbers configured";
                return response;
            }

            try
            {
                var serverUrl = _settings.WablasServerUrl.TrimEnd('/');

                var data = new
                {
                    data = phoneNumbers.Select(p => new
                    {
                        phone = FormatPhoneNumber(p),
                        image = imageUrl,
                        caption = caption,
                        @type = "image"
                    }).ToList()
                };

                var jsonBody = JsonConvert.SerializeObject(data);

                var request = new HttpRequestMessage(HttpMethod.Post, string.Format("{0}/api/v2/send-image", serverUrl));
                request.Headers.Add("Authorization", GetWablasAuthHeader());
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var httpResponse = await _httpClient.SendAsync(request);
                var responseContent = await httpResponse.Content.ReadAsStringAsync();

                var result = JObject.Parse(responseContent);
                response.RawResponse = responseContent;
                response.HttpStatus = (int)httpResponse.StatusCode;

                if (httpResponse.IsSuccessStatusCode)
                {
                    response.Success = result.Value<bool?>("status") ?? true;
                    response.Message = result.Value<string>("message") ?? "Image sent successfully";
                    _logger.LogInformation(string.Format("Wablas image sent successfully to {0} recipient(s)", phoneNumbers.Count));
                }
                else
                {
                    response.Success = false;
                    response.Message = string.Format("Wablas API error: {0} - {1}", httpResponse.StatusCode, responseContent);
                    _logger.LogError(string.Format("Wablas API error: {0}", responseContent));
                }
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = string.Format("Error sending Wablas image: {0}", ex.Message);
                _logger.LogError(ex, "Error sending Wablas image");
            }

            return response;
        }

        // ============================================
        // WABLAS - SEND DOCUMENT/FILE
        // ============================================
        public async Task<WablasResponse> SendWablasDocumentAsync(string documentUrl, string filename, string phone = null)
        {
            var response = new WablasResponse();

            if (string.IsNullOrEmpty(_settings.WablasServerUrl) || string.IsNullOrEmpty(_settings.WablasToken))
            {
                response.Success = false;
                response.Message = "Wablas settings not configured";
                return response;
            }

            var phoneNumbers = new List<string>();
            if (!string.IsNullOrEmpty(phone))
            {
                phoneNumbers.Add(phone);
            }
            else if (_settings.WablasPhoneNumbers != null && _settings.WablasPhoneNumbers.Count > 0)
            {
                phoneNumbers = _settings.WablasPhoneNumbers;
            }
            else
            {
                response.Success = false;
                response.Message = "No WhatsApp phone numbers configured";
                return response;
            }

            try
            {
                var serverUrl = _settings.WablasServerUrl.TrimEnd('/');

                var data = new
                {
                    data = phoneNumbers.Select(p => new
                    {
                        phone = FormatPhoneNumber(p),
                        document = documentUrl,
                        filename = filename,
                        @type = "document"
                    }).ToList()
                };

                var jsonBody = JsonConvert.SerializeObject(data);

                var request = new HttpRequestMessage(HttpMethod.Post, string.Format("{0}/api/v2/send-document", serverUrl));
                request.Headers.Add("Authorization", GetWablasAuthHeader());
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var httpResponse = await _httpClient.SendAsync(request);
                var responseContent = await httpResponse.Content.ReadAsStringAsync();

                var result = JObject.Parse(responseContent);
                response.RawResponse = responseContent;
                response.HttpStatus = (int)httpResponse.StatusCode;

                if (httpResponse.IsSuccessStatusCode)
                {
                    response.Success = result.Value<bool?>("status") ?? true;
                    response.Message = result.Value<string>("message") ?? "Document sent successfully";
                    _logger.LogInformation(string.Format("Wablas document sent successfully to {0} recipient(s)", phoneNumbers.Count));
                }
                else
                {
                    response.Success = false;
                    response.Message = string.Format("Wablas API error: {0} - {1}", httpResponse.StatusCode, responseContent);
                    _logger.LogError(string.Format("Wablas API error: {0}", responseContent));
                }
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = string.Format("Error sending Wablas document: {0}", ex.Message);
                _logger.LogError(ex, "Error sending Wablas document");
            }

            return response;
        }

        // ============================================
        // WABLAS - CHECK DEVICE STATUS
        // ============================================
        public async Task<WablasDeviceStatus> CheckWablasDeviceStatusAsync()
        {
            var status = new WablasDeviceStatus();

            if (string.IsNullOrEmpty(_settings.WablasServerUrl) || string.IsNullOrEmpty(_settings.WablasToken))
            {
                status.Connected = false;
                status.Message = "Wablas settings not configured";
                return status;
            }

            try
            {
                var serverUrl = _settings.WablasServerUrl.TrimEnd('/');

                // V1 endpoint (hanya butuh token, bukan token.secret_key)
                var url = string.Format("{0}/api/device/info?token={1}", serverUrl, _settings.WablasToken);

                var httpResponse = await _httpClient.GetAsync(url);
                var responseContent = await httpResponse.Content.ReadAsStringAsync();

                status.RawResponse = responseContent;

                if (httpResponse.IsSuccessStatusCode)
                {
                    var result = JObject.Parse(responseContent);
                    var data = result["data"];
                    if (data != null)
                    {
                        status.Connected = data.Value<bool?>("connected") ?? data.Value<bool?>("status") ?? false;
                        status.PhoneNumber = data.Value<string>("phone") ?? data.Value<string>("nomor") ?? "";
                        status.DeviceName = data.Value<string>("name") ?? data.Value<string>("nama") ?? "";
                        status.BatteryLevel = data.Value<int?>("battery") ?? data.Value<int?>("baterai") ?? 0;
                        status.Message = status.Connected ? "Device connected" : "Device disconnected";
                    }
                    else
                    {
                        status.Connected = false;
                        status.Message = "Unable to parse device status";
                    }
                }
                else
                {
                    status.Connected = false;
                    status.Message = string.Format("Wablas API error: {0} - {1}", httpResponse.StatusCode, TruncateResponse(responseContent));
                }
            }
            catch (Exception ex)
            {
                status.Connected = false;
                status.Message = string.Format("Error checking device: {0}", ex.Message);
                _logger.LogError(ex, "Error checking Wablas device status");
            }

            return status;
        }

        // ============================================
        // WABLAS - SAVE SETTINGS TO DATABASE
        // ============================================
        public async Task SaveWablasSettingsAsync(string serverUrl, string token, string secretKey, List<string> phoneNumbers, bool enableWhatsApp)
        {
            var settingsToSave = new Dictionary<string, string>
            {
                { "Notification.WablasServerUrl", serverUrl ?? "" },
                { "Notification.WablasToken", token ?? "" },
                { "Notification.WablasSecretKey", secretKey ?? "" },
                { "Notification.WablasPhoneNumbers", phoneNumbers != null ? string.Join(",", phoneNumbers) : "" },
                { "Notification.EnableWhatsApp", enableWhatsApp.ToString().ToLower() }
            };

            foreach (var kvp in settingsToSave)
            {
                var existing = await _context.AppSettingsRecords
                    .FirstOrDefaultAsync(x => x.SettingKey == kvp.Key);

                if (existing != null)
                {
                    existing.SettingValue = kvp.Value;
                    existing.UpdatedAt = DateTime.Now;
                }
                else
                {
                    _context.AppSettingsRecords.Add(new AppSettingsRecord
                    {
                        SettingKey = kvp.Key,
                        SettingValue = kvp.Value,
                        UpdatedAt = DateTime.Now
                    });
                }
            }

            await _context.SaveChangesAsync();
            LoadSettings();
        }

        // ============================================
        // PHONE NUMBER FORMATTING
        // ============================================
        private static string FormatPhoneNumber(string phone)
        {
            if (string.IsNullOrEmpty(phone))
                return phone;

            phone = phone.Trim().Replace("-", "").Replace(" ", "").Replace("(", "").Replace(")", "");

            // Convert 08xx to 628xx (Indonesia)
            if (phone.StartsWith("08"))
            {
                phone = "62" + phone.Substring(1);
            }
            // Convert +62 to 62
            else if (phone.StartsWith("+62"))
            {
                phone = phone.Substring(1);
            }
            // Already starts with 62, keep as is
            // No country code, assume Indonesia
            else if (!phone.StartsWith("62") && !phone.StartsWith("+"))
            {
                phone = "62" + phone;
            }

            // Wablas requires @s.whatsapp.net suffix for some endpoints
            // But for v2 API, just the phone number is enough
            return phone;
        }

        private static string TruncateResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                return "";
            return response.Length > 200 ? response.Substring(0, 200) + "..." : response;
        }

        private string GetWablasAuthHeader()
        {
            if (!string.IsNullOrEmpty(_settings.WablasSecretKey))
                return string.Format("{0}.{1}", _settings.WablasToken, _settings.WablasSecretKey);
            return _settings.WablasToken;
        }
    }

    // ============================================
    // WABLAS RESPONSE MODEL
    // ============================================
    public class WablasResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int HttpStatus { get; set; }
        public string RawResponse { get; set; }
    }

    // ============================================
    // WABLAS DEVICE STATUS MODEL
    // ============================================
    public class WablasDeviceStatus
    {
        public bool Connected { get; set; }
        public string PhoneNumber { get; set; }
        public string DeviceName { get; set; }
        public int BatteryLevel { get; set; }
        public string Message { get; set; }
        public string RawResponse { get; set; }
    }
}
