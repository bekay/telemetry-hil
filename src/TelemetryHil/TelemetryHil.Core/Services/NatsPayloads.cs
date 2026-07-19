using System.Text.Json;
using System.Text.Json.Serialization;
using TelemetryHil.Core.Models;

namespace TelemetryHil.Core.Services
{
    /// <summary>
    /// JSON payload builders/parsers for the NATS bus. Field names are
    /// snake_case to match the Python side (pyod-service models.py,
    /// influxdb-writer). Kept in Core so they are unit-testable without a
    /// live NATS connection.
    /// </summary>
    public static class NatsPayloads
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>Payload for sensor.snapshots — matches pyod-service SensorSnapshot.</summary>
        public static byte[] Snapshot(DownholeSensorSnapshot s, string scenarioName) =>
            JsonSerializer.SerializeToUtf8Bytes(new
            {
                timestamp_ms = s.TimestampMs,
                scenario_name = scenarioName,
                pressure_raw = s.PressureRaw,
                temperature_raw = s.TemperatureRaw,
                rotation_raw = s.RotationRaw,
                depth_raw = s.DepthRaw,
                tension_raw = s.TensionRaw,
                line_speed_raw = s.LineSpeedRaw,
            }, Options);

        /// <summary>Payload for test.results.&lt;scenario&gt;.</summary>
        public static byte[] ScenarioResult(ScenarioResult r) =>
            JsonSerializer.SerializeToUtf8Bytes(new
            {
                scenario_name = r.ScenarioName,
                passed = r.Passed,
                failure_reason = r.FailureReason,
                executed_at = r.ExecutedAt.ToString("O"),
                pulse_count = r.SignalCapture?.PulseCount,
                avg_pulse_width_us = r.SignalCapture?.AvgPulseWidthUs,
                anomaly_count = r.AnomalyCount,
                has_critical = r.HasCritical,
            }, Options);

        /// <summary>Payload for a single detection frame.</summary>
        public static byte[] Frame(DetectionFrame f) =>
            JsonSerializer.SerializeToUtf8Bytes(new
            {
                timestamp_ms = f.TimestampMs,
                pulse_width = f.PulseWidth,
                amplitude = f.Amplitude,
                quality = f.Quality,
                channel = f.Channel,
            }, Options);

        /// <summary>
        /// Parses an anomaly.events payload published by pyod-service into the
        /// Core AnomalyEvent record. Returns null on malformed payloads.
        /// </summary>
        public static AnomalyEvent? ParseAnomalyEvent(ReadOnlySpan<byte> payload)
        {
            try
            {
                using var doc = JsonDocument.Parse(payload.ToArray());
                var root = doc.RootElement;

                return new AnomalyEvent(
                    ScenarioName: root.GetProperty("scenario_name").GetString() ?? "unknown",
                    DetectedAt: root.TryGetProperty("detected_at", out var da) &&
                                DateTimeOffset.TryParse(da.GetString(), out var dt)
                        ? dt : DateTimeOffset.Now,
                    TimestampMs: root.GetProperty("timestamp_ms").GetInt64(),
                    PrimarySensor: GetNullableString(root, "primary_sensor"),
                    PrimaryValue: GetNullableDouble(root, "primary_value"),
                    BaselineMean: GetNullableDouble(root, "baseline_mean"),
                    DeviationPct: GetNullableDouble(root, "deviation_pct"),
                    IForestScore: root.GetProperty("iforest_score").GetDouble(),
                    Severity: root.TryGetProperty("severity", out var sev) &&
                              sev.GetString() == "critical"
                        ? AnomalySeverity.Critical
                        : AnomalySeverity.Warning);
            }
            catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                return null;
            }
        }

        private static string? GetNullableString(JsonElement root, string name) =>
            root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
                ? p.GetString() : null;

        private static double? GetNullableDouble(JsonElement root, string name) =>
            root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number
                ? p.GetDouble() : null;
    }
}
