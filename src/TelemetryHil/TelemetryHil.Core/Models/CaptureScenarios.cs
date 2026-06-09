namespace TelemetryHil.Core.Models
{
    public record CaptureResult(
        int Channel,
        int PulseCount,
        double AvgPulseWidthUs,
        double MinPulseWidthUs,
        double MaxPulseWidthUs,
        double CaptureDurationSeconds
    );

    public record ScenarioDefinition(
        string Name,
        int SensorMode,
        string FaultInjection,
        double DurationSeconds,
        double SimPressurePsi = 5000.0,
        double SimTemperatureC = 85.0,
        double SimRotationRpm = 1500.0,
        double SimDepthM = 1500.0,
        double SimTensionKn = 25.0,
        double SimLineSpeedMs = 1.0
    );

    public record ScenarioResult(
        string ScenarioName,
        bool Passed,
        string? FailureReason,
        DateTimeOffset ExecutedAt,
        CaptureResult? SignalCapture,
        IReadOnlyList<AnomalyEvent> Anomalies
    )
    {
        /// <summary>Count of anomaly events that fired during this run.</summary>
        public int AnomalyCount => Anomalies.Count;

        /// <summary>True if any anomaly reached Critical severity.</summary>
        public bool HasCritical => Anomalies.Any(a => a.Severity == AnomalySeverity.Critical);
    }
}
