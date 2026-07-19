using NATS.Client.Core;
using TelemetryHil.Core.Interfaces;
using TelemetryHil.Core.Models;
using TelemetryHil.Core.Services;
using NatsState = TelemetryHil.Core.Models.NatsConnectionState;

namespace TelemetryHil.Hardware
{
    /// <summary>
    /// Real ITestPublisher over NATS.Net. Subjects:
    ///   sensor.snapshots            — 1Hz stitched snapshots (consumed by pyod-service, influxdb-writer)
    ///   test.results.&lt;scenario&gt; — scenario completion records
    /// Payloads are snake_case JSON built by NatsPayloads (Core).
    /// </summary>
    public class NatsTestPublisher : ITestPublisher
    {
        public const string SnapshotSubject = "sensor.snapshots";
        public const string ResultSubjectPrefix = "test.results";

        private NatsConnection? _nats;

        public NatsState ConnectionState { get; private set; } = NatsState.Disconnected;

        public async Task<bool> ConnectAsync(string natsUrl, CancellationToken ct = default)
        {
            try
            {
                ConnectionState = NatsState.Reconnecting;
                _nats = new NatsConnection(new NatsOpts { Url = natsUrl });
                await _nats.ConnectAsync();
                ConnectionState = NatsState.Connected;
                return true;
            }
            catch (Exception)
            {
                ConnectionState = NatsState.Error;
                if (_nats is not null) await _nats.DisposeAsync();
                _nats = null;
                return false;
            }
        }

        public Task PublishScenarioResultAsync(ScenarioResult result, CancellationToken ct = default)
            => PublishAsync($"{ResultSubjectPrefix}.{result.ScenarioName}",
                            NatsPayloads.ScenarioResult(result), ct);

        public Task PublishFrameAsync(DetectionFrame frame, string subject, CancellationToken ct = default)
            => PublishAsync(subject, NatsPayloads.Frame(frame), ct);

        public Task PublishSnapshotAsync(DownholeSensorSnapshot snapshot, string? scenarioName = null, CancellationToken ct = default)
            => PublishAsync(SnapshotSubject,
                            NatsPayloads.Snapshot(snapshot, scenarioName ?? "unknown"), ct);

        private async Task PublishAsync(string subject, byte[] payload, CancellationToken ct)
        {
            if (_nats is null || ConnectionState != NatsState.Connected)
                return; // publishing is best-effort; UI state gates the calls

            await _nats.PublishAsync(subject, payload, cancellationToken: ct);
        }

        public async ValueTask DisposeAsync()
        {
            if (_nats is not null)
            {
                await _nats.DisposeAsync();
                _nats = null;
            }
            ConnectionState = NatsState.Disconnected;
        }
    }
}
