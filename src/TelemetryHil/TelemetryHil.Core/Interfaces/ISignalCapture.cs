using TelemetryHil.Core.Models;

namespace TelemetryHil.Core.Interfaces
{
    /// <summary>
    /// Abstracts Saleae signal capture via the FastAPI wrapper
    /// </summary>
    public interface ISignalCapture
    {
        bool IsConnected { get; }

        Task<bool> CheckHealthAsync(CancellationToken ct = default);
        Task<CaptureResult> CaptureAsync(
            double durationSeconds,
            int[] digitalChannels,
            int sampleRate = 10_000_000,
            CancellationToken ct = default);
    }
}
