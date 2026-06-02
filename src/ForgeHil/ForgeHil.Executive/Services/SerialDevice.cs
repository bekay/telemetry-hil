using ForgeHil.Core.Interfaces;
using ForgeHil.Core.Models;

namespace ForgeHil.Executive.Services
{
    /// <summary>
    /// Serial device — simulates full downhole sensor suite.
    /// Emits all seven line types at 1Hz so the full data pipeline is exercised
    /// before Phase 2 hardware is available.
    ///
    /// Sensor ranges:
    ///   Pressure     0–15000 PSI
    ///   Temperature  0–175 °C
    ///   Rotation     0–3000 RPM
    ///   Depth        0–5000 m
    ///   Tension      0–50 kN
    ///   Line speed   0–5 m/s
    /// </summary>
    public class StubSerialDevice : ISerialDevice
    {
        private CancellationTokenSource? _cts;
        private DeviceConnectionState _state = DeviceConnectionState.Disconnected;

        public DeviceConnectionState ConnectionState => _state;
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

        // Simulation values — set from scenario before connecting
        public double SimPressurePsi { get; set; } = 5000.0;
        public double SimTemperatureC { get; set; } = 85.0;
        public double SimRotationRpm { get; set; } = 1500.0;
        public double SimDepthM { get; set; } = 1500.0;
        public double SimTensionKn { get; set; } = 25.0;
        public double SimLineSpeedMs { get; set; } = 1.0;

        public async Task<bool> ConnectAsync(string portName, int baudRate, CancellationToken ct = default)
        {
            await Task.Delay(500, ct);
            _state = DeviceConnectionState.Connected;
            FirmwareId = "RADAR-SIM-EFM32-001";

            _cts = new CancellationTokenSource();
            _ = SimulateOutputAsync(_cts.Token);
            return true;
        }

        public async Task DisconnectAsync()
        {
            _cts?.Cancel();
            await Task.Delay(100);
            _state = DeviceConnectionState.Disconnected;
            FirmwareId = null;
        }

        public Task SendCommandAsync(string command, CancellationToken ct = default)
            => Task.CompletedTask;

        private async Task SimulateOutputAsync(CancellationToken ct)
        {
            long ts = 600_000;

            while (!ct.IsCancellationRequested)
            {
                LastSeen = DateTimeOffset.Now;

                // DET — radar sim
                Emit("DET", $"{ts},10,200,95,1",
                    () => FrameReceived?.Invoke(this, new DetectionFrame(ts, 10, 200, 95, 1)));

                // PRS — pressure ±50 PSI variance
                var prs = SimPressurePsi + Variance(50.0);
                Emit("PRS", $"{ts},{prs:F1}",
                    () => PressureReceived?.Invoke(this, new PressureFrame(ts, prs)));

                // TMP — temperature ±0.5°C variance
                var tmp = SimTemperatureC + Variance(0.5);
                Emit("TMP", $"{ts},{tmp:F2}",
                    () => TemperatureReceived?.Invoke(this, new TemperatureFrame(ts, tmp)));

                // ROT — rotation ±30 RPM variance
                var rot = SimRotationRpm + Variance(30.0);
                Emit("ROT", $"{ts},{rot:F1}",
                    () => RotationReceived?.Invoke(this, new RotationFrame(ts, rot)));

                // DEP — depth ±1 m variance
                var dep = SimDepthM + Variance(1.0);
                Emit("DEP", $"{ts},{dep:F2}",
                    () => DepthReceived?.Invoke(this, new DepthFrame(ts, dep)));

                // TEN — tension ±0.5 kN variance
                var ten = SimTensionKn + Variance(0.5);
                Emit("TEN", $"{ts},{ten:F2}",
                    () => TensionReceived?.Invoke(this, new TensionFrame(ts, ten)));

                // SPD — line speed ±0.05 m/s variance
                var spd = SimLineSpeedMs + Variance(0.05);
                Emit("SPD", $"{ts},{spd:F3}",
                    () => LineSpeedReceived?.Invoke(this, new LineSpeedFrame(ts, spd)));

                ts += 1000;
                await Task.Delay(1000, ct).ConfigureAwait(false);
            }
        }

        private void Emit(string prefix, string payload, Action raise)
        {
            RawLineReceived?.Invoke(this, $"{prefix}:{payload}");
            raise();
        }

        private static double Variance(double range) =>
            (Random.Shared.NextDouble() - 0.5) * 2.0 * range;

        public void Dispose() => _cts?.Cancel();
    }
}
