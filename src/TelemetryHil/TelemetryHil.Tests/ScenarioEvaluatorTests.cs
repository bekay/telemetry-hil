using FluentAssertions;
using TelemetryHil.Core.Models;
using TelemetryHil.Core.Services;
using Xunit;

public class ScenarioEvaluatorTests
{
    private static readonly ScenarioDefinition Scenario =
        new("normal_operation", SensorMode: 0, FaultInjection: "none", DurationSeconds: 30);

    private static CaptureResult Capture(int pulses, double duration = 30) =>
        new(Channel: 0, PulseCount: pulses, AvgPulseWidthUs: 10,
            MinPulseWidthUs: 8, MaxPulseWidthUs: 12, CaptureDurationSeconds: duration);

    private static AnomalyEvent Anomaly(AnomalySeverity severity, AnomalyAction action) =>
        new("normal_operation", DateTimeOffset.Now, 1000, "pressure",
            14800, 5000, 196, 0.95, severity) { OperatorAction = action };

    [Fact]
    public void Passes_WhenSignalPresent_AndNoAnomalies()
    {
        var (passed, reason) = ScenarioEvaluator.Evaluate(Scenario, Capture(30), []);
        passed.Should().BeTrue();
        reason.Should().BeNull();
    }

    [Fact]
    public void Fails_WhenNoPulses()
    {
        var (passed, reason) = ScenarioEvaluator.Evaluate(Scenario, Capture(0), []);
        passed.Should().BeFalse();
        reason.Should().Contain("no pulses");
    }

    [Fact]
    public void Fails_WhenPulseRateTooLow()
    {
        // 2 pulses over 30s is below the 0.25/s floor
        var (passed, reason) = ScenarioEvaluator.Evaluate(Scenario, Capture(2), []);
        passed.Should().BeFalse();
        reason.Should().Contain("pulse rate too low");
    }

    [Fact]
    public void Fails_WhenOperatorFailedAnomaly()
    {
        var (passed, reason) = ScenarioEvaluator.Evaluate(
            Scenario, Capture(30), [Anomaly(AnomalySeverity.Warning, AnomalyAction.Fail)]);
        passed.Should().BeFalse();
        reason.Should().Contain("anomaly failed by operator: pressure");
    }

    [Fact]
    public void Fails_WhenCriticalAnomalyUnresolved()
    {
        var (passed, reason) = ScenarioEvaluator.Evaluate(
            Scenario, Capture(30), [Anomaly(AnomalySeverity.Critical, AnomalyAction.None)]);
        passed.Should().BeFalse();
        reason.Should().Contain("unresolved critical anomaly: pressure");
    }

    [Fact]
    public void Passes_WhenCriticalAnomalyAcknowledged()
    {
        var (passed, _) = ScenarioEvaluator.Evaluate(
            Scenario, Capture(30), [Anomaly(AnomalySeverity.Critical, AnomalyAction.WarnContinue)]);
        passed.Should().BeTrue();
    }

    [Fact]
    public void Passes_WarningAnomalyWithNoAction()
    {
        var (passed, _) = ScenarioEvaluator.Evaluate(
            Scenario, Capture(30), [Anomaly(AnomalySeverity.Warning, AnomalyAction.None)]);
        passed.Should().BeTrue();
    }

    [Fact]
    public void CollectsMultipleFailureReasons()
    {
        var (passed, reason) = ScenarioEvaluator.Evaluate(
            Scenario, Capture(0), [Anomaly(AnomalySeverity.Critical, AnomalyAction.None)]);
        passed.Should().BeFalse();
        reason.Should().Contain("no pulses").And.Contain("unresolved critical");
    }
}
