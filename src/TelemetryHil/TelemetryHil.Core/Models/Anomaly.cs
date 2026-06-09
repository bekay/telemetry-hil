namespace TelemetryHil.Core.Models
{

    /// <summary>
    /// Anomaly event received from the PyOD VM pod via NATS anomaly.events.
    /// In stub mode, injected manually via the WPF executive.
    /// </summary>
    public record AnomalyEvent(
        string ScenarioName,
        DateTimeOffset DetectedAt,
        long TimestampMs,
        string? PrimarySensor,
        double? PrimaryValue,
        double? BaselineMean,
        double? DeviationPct,
        double IForestScore,
        AnomalySeverity Severity
    )
    {
        /// <summary>Set by operator in WPF executive after receiving the event.</summary>
        public AnomalyAction OperatorAction { get; init; } = AnomalyAction.None;
    }

    public enum AnomalySeverity { Warning, Critical }

    public enum AnomalyAction
    {
        None,
        Fail,
        WarnContinue,
        WarnRetest,
        Dismiss
    }
}
