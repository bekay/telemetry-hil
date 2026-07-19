using NATS.Client.Core;
using TelemetryHil.Core.Interfaces;
using TelemetryHil.Core.Models;
using TelemetryHil.Core.Services;

namespace TelemetryHil.Hardware
{
    /// <summary>
    /// Subscribes to anomaly.events on NATS and raises parsed AnomalyEvents —
    /// feeds MainViewModel.ReceiveAnomalyEvent from the live PyOD pod.
    /// </summary>
    public class NatsAnomalySubscriber : IAnomalySubscriber
    {
        public const string AnomalySubject = "anomaly.events";

        private NatsConnection? _nats;
        private CancellationTokenSource? _cts;
        private Task? _loop;

        public bool IsRunning => _loop is { IsCompleted: false };

        public event EventHandler<AnomalyEvent>? AnomalyReceived;

        public async Task StartAsync(string natsUrl, CancellationToken ct = default)
        {
            await StopAsync();

            _nats = new NatsConnection(new NatsOpts { Url = natsUrl });
            await _nats.ConnectAsync();

            _cts = new CancellationTokenSource();
            _loop = Task.Run(() => SubscribeLoopAsync(_nats, _cts.Token), CancellationToken.None);
        }

        private async Task SubscribeLoopAsync(NatsConnection nats, CancellationToken ct)
        {
            await foreach (var msg in nats.SubscribeAsync<byte[]>(AnomalySubject, cancellationToken: ct))
            {
                if (msg.Data is null) continue;
                var anomaly = NatsPayloads.ParseAnomalyEvent(msg.Data);
                if (anomaly is not null)
                    AnomalyReceived?.Invoke(this, anomaly);
            }
        }

        public async Task StopAsync()
        {
            _cts?.Cancel();
            if (_loop is not null)
            {
                try { await _loop; } catch (OperationCanceledException) { }
            }
            if (_nats is not null) await _nats.DisposeAsync();
            _nats = null;
            _cts?.Dispose();
            _cts = null;
            _loop = null;
        }

        public async ValueTask DisposeAsync() => await StopAsync();
    }
}
