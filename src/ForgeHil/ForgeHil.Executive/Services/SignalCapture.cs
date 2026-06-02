using ForgeHil.Core.Interfaces;
using ForgeHil.Core.Models;

namespace ForgeHil.Executive.Services
{
    public class SignalCapture : ISignalCapture
    {
        public bool IsConnected { get; private set; }

        public async Task<bool> CheckHealthAsync(CancellationToken ct = default)
        {
            await Task.Delay(300, ct);
            IsConnected = true;
            return true;
        }

        public async Task<CaptureResult> CaptureAsync(
            double durationSeconds,
            int[] digitalChannels,
            int sampleRate = 10_000_000,
            CancellationToken ct = default)
        {
            await Task.Delay((int)(durationSeconds * 1000), ct);
            var pulses = Random.Shared.Next(8, 32);
            return new CaptureResult(
                Channel: digitalChannels[0],
                PulseCount: pulses,
                AvgPulseWidthUs: Random.Shared.NextDouble() * 50 + 10,
                MinPulseWidthUs: 8.0,
                MaxPulseWidthUs: 65.0,
                CaptureDurationSeconds: durationSeconds);
        }
    }
}
