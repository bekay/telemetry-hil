using ForgeHil.Core.Interfaces;
using ForgeHil.Core.Models;

namespace ForgeHil.Executive.Services
{
    /// <summary>
    /// Stub NATS publisher — logs to console.
    /// Replace with real NatsTestPublisher once NATS.Net integration is implemented.
    /// </summary>
    public class StubTestPublisher : ITestPublisher
    {
        public NatsConnectionState ConnectionState { get; private set; }
            = NatsConnectionState.Disconnected;

        public async Task<bool> ConnectAsync(string natsUrl, CancellationToken ct = default)
        {
            await Task.Delay(400, ct);
            ConnectionState = NatsConnectionState.Connected;
            return true;
        }

        public Task PublishScenarioResultAsync(ScenarioResult result, CancellationToken ct = default)
        {
            Console.WriteLine($"[stub-nats] publish: {result.ScenarioName} → {(result.Passed ? "PASS" : "FAIL")}");
            return Task.CompletedTask;
        }

        public Task PublishFrameAsync(DetectionFrame frame, string subject, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task PublishSnapshotAsync(DownholeSensorSnapshot snapshot, CancellationToken ct = default)
        {
            Console.WriteLine(
                $"[stub-nats] snapshot: " +
                $"prs={snapshot.PressureRaw:F0}PSI " +
                $"tmp={snapshot.TemperatureRaw:F1}°C " +
                $"rot={snapshot.RotationRaw:F0}RPM " +
                $"dep={snapshot.DepthRaw:F1}m " +
                $"ten={snapshot.TensionRaw:F1}kN " +
                $"spd={snapshot.LineSpeedRaw:F2}m/s");
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
