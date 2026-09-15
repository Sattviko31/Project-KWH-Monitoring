using System;
using System.Collections.Generic;
using System.Linq;
using KWHMonitoring.Models;

namespace KWHMonitoring.Services
{
    public class AnomalyAnalysisService : IAnomalyAnalysisService
    {
        public AnomalyAnalysisResult Analyze(AnomalyLog log, IEnumerable<AnomalyLog> recentHistory)
        {
            var historyList = recentHistory as IList<AnomalyLog> ?? recentHistory?.ToList() ?? new List<AnomalyLog>();
            var isDowntime = !string.IsNullOrEmpty(log.Notes) && log.Notes.Contains("downtime period");
            var hasRepeatedPattern = DetectRepeatedPattern(log, historyList);

            var severity = AssessSeverity(log, isDowntime, hasRepeatedPattern);
            var rootCause = BuildRootCause(log, isDowntime);
            var recommendedAction = RecommendAction(log, isDowntime);
            var impactAssessment = BuildImpactAssessment(log, isDowntime, hasRepeatedPattern);

            return new AnomalyAnalysisResult
            {
                Severity = severity,
                RootCause = rootCause,
                RecommendedAction = recommendedAction,
                ImpactAssessment = impactAssessment,
                HasRepeatedPattern = hasRepeatedPattern
            };
        }

        public string AssessSeverity(AnomalyLog log, bool isDowntime, bool hasRepeatedPattern)
        {
            if (hasRepeatedPattern)
                return "critical";

            if (isDowntime && log.AnomalyType == "OVERLOAD")
                return "critical";

            if (log.AnomalyType == "DROP" && !isDowntime)
                return "high";

            if (log.AnomalyType == "OVERLOAD")
            {
                if (log.Deviation > 50)
                    return "critical";
                if (log.Deviation > 30)
                    return "high";
                if (log.Deviation > 10)
                    return "medium";
            }

            if (log.AnomalyType == "DROP")
            {
                if (log.Deviation > 50)
                    return "high";
                if (log.Deviation > 30)
                    return "medium";
            }

            return "low";
        }

        public string BuildRootCause(AnomalyLog log, bool isDowntime)
        {
            if (isDowntime && log.AnomalyType == "OVERLOAD")
                return string.Format(
                    "Daya masih terdeteksi selama periode jam mati ({0:00}:00-{1:00}:00). " +
                    "Kemungkinan relay tidak berfungsi atau beban tetap aktif saat seharusnya mati.",
                    log.Notes.Contains("22") ? 22 : 0, log.Notes.Contains("6") ? 6 : 0);

            switch (log.AnomalyType)
            {
                case "OVERLOAD":
                    return log.Deviation > 50
                        ? "Beban daya melebihi threshold dengan deviasi sangat tinggi (>50%). Kemungkinan ada peralatan tambahan yang menyala atau gangguan pada beban."
                        : log.Deviation > 30
                            ? "Beban daya melebihi threshold dengan deviasi signifikan (30-50%). Perlu pemantauan ketat."
                            : "Beban daya sedikit melebihi threshold. Peningkatan beban ringan.";

                case "DROP":
                case "DEVICE_DROP":
                    return isDowntime
                        ? "DROP terjadi saat periode jam mati. Ini adalah kondisi normal karena listrik sengaja dimatikan."
                        : "Daya turun drastis di bawah threshold. Kemungkinan device offline, mati listrik, atau gangguan komunikasi.";

                default:
                    return "Jenis anomali tidak dikenali. Perlu investigasi manual.";
            }
        }

        public string RecommendAction(AnomalyLog log, bool isDowntime)
        {
            if (isDowntime && log.AnomalyType == "OVERLOAD")
                return "Periksa relay dan jadwal downtime. Verifikasi apakah ada beban yang tetap aktif saat jam mati. Lakukan pemeriksaan fisik panel.";

            switch (log.AnomalyType)
            {
                case "OVERLOAD":
                    if (log.Deviation > 50)
                        return "Segera kurangi beban atau matikan peralatan non-esensial. Periksa kapasitas MCB dan kabel. Pertimbangkan penambahan kapasitas panel.";
                    if (log.Deviation > 30)
                        return "Kurangi beban secara bertahap. Identifikasi peralatan yang menyebabkan lonjakan daya.";
                    return "Pantau tren beban beberapa saat. Jika terus meningkat, lakukan audit perangkat.";

                case "DROP":
                case "DEVICE_DROP":
                    return isDowntime
                        ? "Tidak perlu tindakan. DROP ini terjadi saat periode jam mati dan merupakan kondisi normal."
                        : "Periksa koneksi MQTT, power supply, dan status MCB panel. Verifikasi apakah device benar-benar offline.";

                default:
                    return "Lakukan investigasi manual untuk menentukan tindakan yang tepat.";
            }
        }

        private string BuildImpactAssessment(AnomalyLog log, bool isDowntime, bool hasRepeatedPattern)
        {
            var parts = new List<string>();

            if (hasRepeatedPattern)
                parts.Add("Pola berulang terdeteksi pada device yang sama dalam 24 jam terakhir.");

            if (isDowntime && log.AnomalyType == "OVERLOAD")
                parts.Add("Risiko tinggi: listrik seharusnya mati tapi masih menyala.");

            if (log.AnomalyType == "OVERLOAD" && log.Deviation > 50)
                parts.Add("Risiko kerusakan peralatan atau kebakaran akibat overload berat.");

            if (log.AnomalyType == "DROP" && !isDowntime)
                parts.Add("Device mungkin tidak terpantau. Data monitoring bisa tidak akurat.");

            return string.Join(" ", parts).Trim();
        }

        private bool DetectRepeatedPattern(AnomalyLog log, IList<AnomalyLog> recentHistory)
        {
            if (recentHistory == null || recentHistory.Count == 0)
                return false;

            var last24Hours = recentHistory
                .Where(x => x.DeviceKey == log.DeviceKey
                    && x.AnomalyType == log.AnomalyType
                    && x.DetectedTime >= log.DetectedTime.AddHours(-24));

            return last24Hours.Count() >= 3;
        }
    }
}
