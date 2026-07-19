using TelemetryHil.Core.Models;

namespace TelemetryHil.Core.Interfaces
{
    /// <summary>
    /// Subscribes to anomaly.events on NATS and surfaces parsed events —
    /// the live-PyOD counterpart to the stub anomaly injector.
    /// </summary>
    public interface IAnomalySubscriber : IAsyncDisposable
    {
        bool IsRunning { get; }

        /// <summary>Connect and start delivering AnomalyReceived events.</summary>
        Task StartAsync(string natsUrl, CancellationToken ct = default);
        Task StopAsync();

        event EventHandler<AnomalyEvent> AnomalyReceived;
    }
}
