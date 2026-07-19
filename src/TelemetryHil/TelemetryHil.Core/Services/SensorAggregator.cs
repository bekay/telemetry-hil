using TelemetryHil.Core.Interfaces;
using TelemetryHil.Core.Models;

namespace TelemetryHil.Core.Services
{

    /// <summary>
    /// Stitches individual sensor frames into a DownholeSensorSnapshot.
    /// Last-value-wins per sensor type. Emits snapshot on every update.
    /// Phase 2: add time-alignment window if sensor rates diverge.
    /// </summary>
    public class SensorAggregator : ISensorAggregator
    {
        private double? _pressure, _temperature, _rotation;
        private double? _depth, _tension, _lineSpeed;
        private long _lastTs;

        public event EventHandler<DownholeSensorSnapshot>? SnapshotReady;

        public void OnDetectionFrame(DetectionFrame frame) { _lastTs = frame.TimestampMs; Emit(); }
        public void OnPressureFrame(PressureFrame frame) { _pressure = frame.PressureRaw; _lastTs = frame.TimestampMs; Emit(); }
        public void OnTemperatureFrame(TemperatureFrame frame) { _temperature = frame.TemperatureRaw; _lastTs = frame.TimestampMs; Emit(); }
        public void OnRotationFrame(RotationFrame frame) { _rotation = frame.RotationRaw; _lastTs = frame.TimestampMs; Emit(); }
        public void OnDepthFrame(DepthFrame frame) { _depth = frame.DepthRaw; _lastTs = frame.TimestampMs; Emit(); }
        public void OnTensionFrame(TensionFrame frame) { _tension = frame.TensionRaw; _lastTs = frame.TimestampMs; Emit(); }
        public void OnLineSpeedFrame(LineSpeedFrame frame) { _lineSpeed = frame.SpeedRaw; _lastTs = frame.TimestampMs; Emit(); }

        private void Emit() =>
            SnapshotReady?.Invoke(this, new DownholeSensorSnapshot(
                _lastTs,
                _pressure, _temperature, _rotation,
                _depth, _tension, _lineSpeed));
    }
}
