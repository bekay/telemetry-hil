using TelemetryHil.Core.Interfaces;
using TelemetryHil.Core.Models;

namespace TelemetryHil.Hardware
{
    /// <summary>
    /// Stable ISerialDevice for DI that picks the transport at ConnectAsync
    /// time from the target string: "tcp://host:port" → TcpSerialDevice,
    /// anything else (COM3, /dev/ttyACM0) → SerialPortDevice. Lets the WPF
    /// port textbox switch transports without any DI or UI changes.
    /// </summary>
    public class RoutingSerialDevice : ISerialDevice
    {
        private SerialDeviceBase? _inner;

        public DeviceConnectionState ConnectionState => _inner?.ConnectionState ?? DeviceConnectionState.Disconnected;
        public string? FirmwareId => _inner?.FirmwareId;
        public DateTimeOffset? LastSeen => _inner?.LastSeen;

        public event EventHandler<DetectionFrame>? FrameReceived;
        public event EventHandler<PressureFrame>? PressureReceived;
        public event EventHandler<TemperatureFrame>? TemperatureReceived;
        public event EventHandler<RotationFrame>? RotationReceived;
        public event EventHandler<DepthFrame>? DepthReceived;
        public event EventHandler<TensionFrame>? TensionReceived;
        public event EventHandler<LineSpeedFrame>? LineSpeedReceived;
        public event EventHandler<string>? RawLineReceived;

        public static SerialDeviceBase Create(string target) =>
            TcpSerialDevice.IsTcpTarget(target)
                ? new TcpSerialDevice()
                : new SerialPortDevice();

        public async Task<bool> ConnectAsync(string target, int baudRate, CancellationToken ct = default)
        {
            if (_inner is not null)
                await _inner.DisconnectAsync();
            _inner?.Dispose();

            _inner = Create(target);
            _inner.FrameReceived += (_, f) => FrameReceived?.Invoke(this, f);
            _inner.PressureReceived += (_, f) => PressureReceived?.Invoke(this, f);
            _inner.TemperatureReceived += (_, f) => TemperatureReceived?.Invoke(this, f);
            _inner.RotationReceived += (_, f) => RotationReceived?.Invoke(this, f);
            _inner.DepthReceived += (_, f) => DepthReceived?.Invoke(this, f);
            _inner.TensionReceived += (_, f) => TensionReceived?.Invoke(this, f);
            _inner.LineSpeedReceived += (_, f) => LineSpeedReceived?.Invoke(this, f);
            _inner.RawLineReceived += (_, l) => RawLineReceived?.Invoke(this, l);

            return await _inner.ConnectAsync(target, baudRate, ct);
        }

        public Task DisconnectAsync() => _inner?.DisconnectAsync() ?? Task.CompletedTask;

        public Task SendCommandAsync(string command, CancellationToken ct = default) =>
            _inner?.SendCommandAsync(command, ct) ?? throw new InvalidOperationException("not connected");

        public void Dispose() => _inner?.Dispose();
    }
}
