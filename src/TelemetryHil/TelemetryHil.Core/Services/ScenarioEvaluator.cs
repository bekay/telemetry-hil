using TelemetryHil.Core.Models;

namespace TelemetryHil.Core.Services
{
    /// <summary>
    /// Minimal Phase 1 pass/fail evaluation of a scenario run against
    /// captured signal data and any anomaly events raised during the run.
    /// </summary>
    public static class ScenarioEvaluator
    {
        /// <summary>
        /// Expected UART line rate is ~1 Hz per line type; a capture on the TX
        /// line should therefore see at least this fraction of duration-seconds
        /// worth of pulse activity to count as "signal present".
        /// </summary>
        public const double MinPulsesPerSecond = 0.25;

        public static (bool Passed, string? FailureReason) Evaluate(
            ScenarioDefinition scenario,
            CaptureResult capture,
            IReadOnlyList<AnomalyEvent> anomalies)
        {
            var reasons = new List<string>();

            if (capture.PulseCount == 0)
                reasons.Add("no pulses detected");
            else if (capture.CaptureDurationSeconds > 0 &&
                     capture.PulseCount < capture.CaptureDurationSeconds * MinPulsesPerSecond)
                reasons.Add($"pulse rate too low ({capture.PulseCount} in {capture.CaptureDurationSeconds:F0}s)");

            foreach (var a in anomalies.Where(a => a.OperatorAction == AnomalyAction.Fail))
                reasons.Add($"anomaly failed by operator: {a.PrimarySensor ?? "unknown"}");

            // A critical anomaly the operator never acted on fails the run —
            // silence is not a pass verdict.
            foreach (var a in anomalies.Where(a =>
                         a.Severity == AnomalySeverity.Critical &&
                         a.OperatorAction == AnomalyAction.None))
                reasons.Add($"unresolved critical anomaly: {a.PrimarySensor ?? "unknown"}");

            return reasons.Count > 0 ? (false, string.Join("; ", reasons)) : (true, null);
        }
    }
}
