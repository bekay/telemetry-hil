using System.Text;
using System.Text.Json;
using FluentAssertions;
using TelemetryHil.Core.Models;
using TelemetryHil.Core.Services;
using Xunit;

public class NatsPayloadsTests
{
    [Fact]
    public void Snapshot_MatchesPyodSensorSnapshotSchema()
    {
        var snapshot = new DownholeSensorSnapshot(
            TimestampMs: 622000,
            PressureRaw: 5012.5, TemperatureRaw: 85.2, RotationRaw: 1500,
            DepthRaw: 1499.0, TensionRaw: 25.1, LineSpeedRaw: 1.01);

        using var doc = JsonDocument.Parse(NatsPayloads.Snapshot(snapshot, "normal_operation"));
        var root = doc.RootElement;

        root.GetProperty("timestamp_ms").GetInt64().Should().Be(622000);
        root.GetProperty("scenario_name").GetString().Should().Be("normal_operation");
        root.GetProperty("pressure_raw").GetDouble().Should().Be(5012.5);
        root.GetProperty("temperature_raw").GetDouble().Should().Be(85.2);
        root.GetProperty("rotation_raw").GetDouble().Should().Be(1500);
        root.GetProperty("depth_raw").GetDouble().Should().Be(1499.0);
        root.GetProperty("tension_raw").GetDouble().Should().Be(25.1);
        root.GetProperty("line_speed_raw").GetDouble().Should().Be(1.01);
    }

    [Fact]
    public void Snapshot_OmitsNullSensors()
    {
        var snapshot = new DownholeSensorSnapshot(1000, 5000.0, null, null, null, null, null);
        using var doc = JsonDocument.Parse(NatsPayloads.Snapshot(snapshot, "s"));
        doc.RootElement.TryGetProperty("temperature_raw", out _).Should().BeFalse();
        doc.RootElement.GetProperty("pressure_raw").GetDouble().Should().Be(5000.0);
    }

    [Fact]
    public void ScenarioResult_SerializesVerdictAndCapture()
    {
        var result = new ScenarioResult(
            "sensor_dropout", Passed: false, FailureReason: "no pulses detected",
            ExecutedAt: DateTimeOffset.Parse("2026-07-17T12:00:00Z"),
            SignalCapture: new CaptureResult(0, 12, 10.5, 8, 12, 15),
            Anomalies: []);

        using var doc = JsonDocument.Parse(NatsPayloads.ScenarioResult(result));
        var root = doc.RootElement;

        root.GetProperty("scenario_name").GetString().Should().Be("sensor_dropout");
        root.GetProperty("passed").GetBoolean().Should().BeFalse();
        root.GetProperty("failure_reason").GetString().Should().Be("no pulses detected");
        root.GetProperty("pulse_count").GetInt32().Should().Be(12);
        root.GetProperty("avg_pulse_width_us").GetDouble().Should().Be(10.5);
        root.GetProperty("anomaly_count").GetInt32().Should().Be(0);
        root.GetProperty("has_critical").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void ParseAnomalyEvent_ParsesPyodPayload()
    {
        // Shape published by services/pyod-service (models.py AnomalyEvent)
        var payload = Encoding.UTF8.GetBytes("""
        {
          "scenario_name": "normal_operation",
          "detected_at": "2026-07-17T12:34:56+00:00",
          "timestamp_ms": 622000,
          "iforest_score": 0.93,
          "iforest_flagged": true,
          "hbos_scores": {"pressure": 0.95, "temperature": 0.12},
          "hbos_flagged": ["pressure"],
          "primary_sensor": "pressure",
          "primary_value": 14800.0,
          "baseline_mean": 5001.2,
          "deviation_pct": 195.9,
          "severity": "critical"
        }
        """);

        var anomaly = NatsPayloads.ParseAnomalyEvent(payload);

        anomaly.Should().NotBeNull();
        anomaly!.ScenarioName.Should().Be("normal_operation");
        anomaly.TimestampMs.Should().Be(622000);
        anomaly.PrimarySensor.Should().Be("pressure");
        anomaly.PrimaryValue.Should().Be(14800.0);
        anomaly.BaselineMean.Should().Be(5001.2);
        anomaly.DeviationPct.Should().Be(195.9);
        anomaly.IForestScore.Should().Be(0.93);
        anomaly.Severity.Should().Be(AnomalySeverity.Critical);
        anomaly.OperatorAction.Should().Be(AnomalyAction.None);
    }

    [Fact]
    public void ParseAnomalyEvent_WarningSeverity_AndNullPrimaryFields()
    {
        var payload = Encoding.UTF8.GetBytes("""
        {
          "scenario_name": "s", "detected_at": "2026-07-17T12:00:00Z",
          "timestamp_ms": 1, "iforest_score": 0.75,
          "primary_sensor": null, "primary_value": null,
          "baseline_mean": null, "deviation_pct": null,
          "severity": "warning"
        }
        """);

        var anomaly = NatsPayloads.ParseAnomalyEvent(payload);

        anomaly!.Severity.Should().Be(AnomalySeverity.Warning);
        anomaly.PrimarySensor.Should().BeNull();
        anomaly.PrimaryValue.Should().BeNull();
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{}")]
    [InlineData("{\"scenario_name\": \"x\"}")]
    public void ParseAnomalyEvent_MalformedPayload_ReturnsNull(string payload)
        => NatsPayloads.ParseAnomalyEvent(Encoding.UTF8.GetBytes(payload)).Should().BeNull();
}
