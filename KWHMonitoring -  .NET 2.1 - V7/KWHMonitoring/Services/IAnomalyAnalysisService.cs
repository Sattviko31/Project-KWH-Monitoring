using System.Collections.Generic;
using KWHMonitoring.Models;

namespace KWHMonitoring.Services
{
    public interface IAnomalyAnalysisService
    {
        AnomalyAnalysisResult Analyze(AnomalyLog log, IEnumerable<AnomalyLog> recentHistory);
        string AssessSeverity(AnomalyLog log, bool isDowntime, bool hasRepeatedPattern);
        string BuildRootCause(AnomalyLog log, bool isDowntime);
        string RecommendAction(AnomalyLog log, bool isDowntime);
    }

    public class AnomalyAnalysisResult
    {
        public string Severity { get; set; }
        public string RootCause { get; set; }
        public string RecommendedAction { get; set; }
        public string ImpactAssessment { get; set; }
        public bool HasRepeatedPattern { get; set; }
    }
}
