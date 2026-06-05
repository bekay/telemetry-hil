namespace TelemetryHil.Core.Models
{
    public enum DeviceConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Error
    }

    public enum NatsConnectionState
    {
        Disconnected,
        Connected,
        Reconnecting,
        Error
    }

}
