using TelemetryHil.Core.Models;

namespace TelemetryHil.Core.Interfaces
{
    /// <summary>
    /// Abstracts the serial device (EFM32 Pearl Gecko over UART).
    /// </summary>
    public interface ISerialDevice : IDisposable
    {
        DeviceConnectionState ConnectionState { get; }
        string? FirmwareId { get; }
        DateTimeOffset? LastSeen { get; }

        Task<bool> ConnectAsync(string portName, int baudRate, CancellationToken ct = default);
        Task DisconnectAsync();
        Task SendCommandAsync(string command, CancellationToken ct = default);

        /// <summary>Raised on every parsed DET: frame (radar sim).</summary>
        event EventHandler<DetectionFrame> FrameReceived;

        /// <summary>Raised on every parsed PRS: frame.</summary>
        event EventHandler<PressureFrame> PressureReceived;

        /// <summary>Raised on every parsed TMP: frame.</summary>
        event EventHandler<TemperatureFrame> TemperatureReceived;

        /// <summary>Raised on every parsed ROT: frame.</summary>
        event EventHandler<RotationFrame> RotationReceived;

        /// <summary>Raised on every parsed DEP: frame.</summary>
        event EventHandler<DepthFrame> DepthReceived;

        /// <summary>Raised on every parsed TEN: frame.</summary>
        event EventHandler<TensionFrame> TensionReceived;

        /// <summary>Raised on every parsed SPD: frame.</summary>
        event EventHandler<LineSpeedFrame> LineSpeedReceived;

        /// <summary>Raised on every raw line (for live UART feed display).</summary>
        event EventHandler<string> RawLineReceived;
    }
}
