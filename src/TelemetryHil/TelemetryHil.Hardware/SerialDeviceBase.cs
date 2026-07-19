using System.IO;
using TelemetryHil.Core.Interfaces;
using TelemetryHil.Core.Models;
using TelemetryHil.Core.Services;

namespace TelemetryHil.Hardware
{
    /// <summary>
    /// Shared ISerialDevice implementation over any line-oriented Stream.
    /// Subclasses supply the transport (local serial port, TCP bridge);
    /// this base owns the read loop, UartLineParser dispatch, and the
    /// ID? handshake that populates FirmwareId.
    /// </summary>
    public abstract class SerialDeviceBase : ISerialDevice
    {
        private Stream? _stream;
        private CancellationTokenSource? _readCts;
        private Task? _readLoop;
        private TaskCompletionSource<string>? _idReply;

        public DeviceConnectionState ConnectionState { get; protected set; }
            = DeviceConnectionState.Disconnected;
        public string? FirmwareId { get; private set; }
        public DateTimeOffset? LastSeen { get; private set; }

        public event EventHandler<DetectionFrame>? FrameReceived;
        public event EventHandler<PressureFrame>? PressureReceived;
        public event EventHandler<TemperatureFrame>? TemperatureReceived;
        public event EventHandler<RotationFrame>? RotationReceived;
        public event EventHandler<DepthFrame>? DepthReceived;
        public event EventHandler<TensionFrame>? TensionReceived;
        public event EventHandler<LineSpeedFrame>? LineSpeedReceived;
        public event EventHandler<string>? RawLineReceived;

        /// <summary>Open the transport and return its bidirectional stream.</summary>
        protected abstract Task<Stream> OpenTransportAsync(string target, int baudRate, CancellationToken ct);

        /// <summary>Close/dispose transport-specific resources (idempotent).</summary>
        protected abstract void CloseTransport();

        public async Task<bool> ConnectAsync(string target, int baudRate, CancellationToken ct = default)
        {
            try
            {
                ConnectionState = DeviceConnectionState.Connecting;
                _stream = await OpenTransportAsync(target, baudRate, ct);

                _readCts = new CancellationTokenSource();
                _readLoop = Task.Run(() => ReadLoopAsync(_stream, _readCts.Token), CancellationToken.None);

                // Ask the firmware to identify itself; the reply arrives on the
                // read loop interleaved with frame traffic.
                _idReply = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                await SendCommandAsync("ID?", ct);
                var idTask = _idReply.Task;
                var done = await Task.WhenAny(idTask, Task.Delay(3000, ct));
                FirmwareId = done == idTask ? idTask.Result : "unknown";

                ConnectionState = DeviceConnectionState.Connected;
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException
                                        or ArgumentException or InvalidOperationException
                                        or System.Net.Sockets.SocketException)
            {
                ConnectionState = DeviceConnectionState.Error;
                CleanUp();
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            _readCts?.Cancel();
            if (_readLoop is not null)
            {
                try { await _readLoop; } catch (OperationCanceledException) { }
            }
            CleanUp();
            ConnectionState = DeviceConnectionState.Disconnected;
            FirmwareId = null;
        }

        public async Task SendCommandAsync(string command, CancellationToken ct = default)
        {
            var stream = _stream ?? throw new InvalidOperationException("not connected");
            var bytes = System.Text.Encoding.ASCII.GetBytes(command + "\r\n");
            await stream.WriteAsync(bytes, ct);
            await stream.FlushAsync(ct);
        }

        private async Task ReadLoopAsync(Stream stream, CancellationToken ct)
        {
            using var reader = new StreamReader(stream, System.Text.Encoding.ASCII, false, 1024, leaveOpen: true);

            while (!ct.IsCancellationRequested)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync(ct);
                }
                catch (Exception e) when (e is IOException or OperationCanceledException
                                            or InvalidOperationException or ObjectDisposedException)
                {
                    if (!ct.IsCancellationRequested)
                        ConnectionState = DeviceConnectionState.Error;
                    return;
                }
                if (line is null)
                {
                    // Transport closed from the far side (e.g. ser2net restart)
                    if (!ct.IsCancellationRequested)
                        ConnectionState = DeviceConnectionState.Error;
                    return;
                }

                line = line.Trim();
                if (line.Length == 0) continue;

                LastSeen = DateTimeOffset.Now;
                RawLineReceived?.Invoke(this, line);

                switch (UartLineParser.Parse(line))
                {
                    case DetectionFrame f: FrameReceived?.Invoke(this, f); break;
                    case PressureFrame f: PressureReceived?.Invoke(this, f); break;
                    case TemperatureFrame f: TemperatureReceived?.Invoke(this, f); break;
                    case RotationFrame f: RotationReceived?.Invoke(this, f); break;
                    case DepthFrame f: DepthReceived?.Invoke(this, f); break;
                    case TensionFrame f: TensionReceived?.Invoke(this, f); break;
                    case LineSpeedFrame f: LineSpeedReceived?.Invoke(this, f); break;
                    case null:
                        // Non-frame line: NOD/ERR/OK, or the ID? reply we may be waiting on.
                        if (_idReply is { Task.IsCompleted: false } &&
                            line != "OK" && !line.StartsWith("NOD:") && !line.StartsWith("ERR:"))
                            _idReply.TrySetResult(line);
                        break;
                }
            }
        }

        private void CleanUp()
        {
            try { _stream?.Dispose(); } catch (IOException) { }
            _stream = null;
            CloseTransport();
            _readCts?.Dispose();
            _readCts = null;
            _readLoop = null;
        }

        public void Dispose()
        {
            _readCts?.Cancel();
            CleanUp();
            GC.SuppressFinalize(this);
        }
    }
}
