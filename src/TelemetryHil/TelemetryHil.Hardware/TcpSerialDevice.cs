using System.IO;
using System.Net.Sockets;

namespace TelemetryHil.Hardware
{
    /// <summary>
    /// ISerialDevice over a TCP serial bridge (ser2net/socat exposing
    /// /dev/ttyACM0 on the P52s). Target format: "tcp://host:port".
    /// The baud rate is configured on the bridge side and ignored here.
    /// </summary>
    public class TcpSerialDevice : SerialDeviceBase
    {
        public const string Scheme = "tcp://";
        public const int DefaultPort = 5000;

        private TcpClient? _client;

        public static bool IsTcpTarget(string target) =>
            target.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase);

        internal static (string Host, int Port) ParseTarget(string target)
        {
            if (!IsTcpTarget(target))
                throw new ArgumentException($"not a tcp:// target: {target}");

            var hostPort = target[Scheme.Length..].TrimEnd('/');
            var idx = hostPort.LastIndexOf(':');
            return idx < 0
                ? (hostPort, DefaultPort)
                : (hostPort[..idx], int.Parse(hostPort[(idx + 1)..]));
        }

        protected override async Task<Stream> OpenTransportAsync(string target, int baudRate, CancellationToken ct)
        {
            var (host, port) = ParseTarget(target);
            _client = new TcpClient { NoDelay = true };

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                await _client.ConnectAsync(host, port, timeout.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new IOException($"connection to {host}:{port} timed out");
            }
            return _client.GetStream();
        }

        protected override void CloseTransport()
        {
            _client?.Dispose();
            _client = null;
        }
    }
}
