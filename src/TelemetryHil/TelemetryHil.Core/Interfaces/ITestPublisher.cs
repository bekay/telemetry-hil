using TelemetryHil.Core.Models;

namespace TelemetryHil.Core.Interfaces
{
    /// <summary>
    /// Abstracts result publishing to NATS.
    /// </summary>
    public interface ITestPublisher : IAsyncDisposable
    {
        NatsConnectionState ConnectionState { get; }

        Task<bool> ConnectAsync(string natsUrl, CancellationToken ct = default);
        Task PublishScenarioResultAsync(ScenarioResult result, CancellationToken ct = default);
        Task PublishFrameAsync(DetectionFrame frame, string subject, CancellationToken ct = default);
        Task PublishSnapshotAsync(DownholeSensorSnapshot snapshot, string? scenarioName = null, CancellationToken ct = default);
    }
}
