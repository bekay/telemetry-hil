using ForgeHil.Core.Models;

namespace ForgeHil.Core.Interfaces
{
    /// <summary>
    /// Stitches individual sensor frames into a single downhole snapshot.
    /// Last-value-wins per sensor type. Emits a snapshot on every update.
    /// Phase 2: add time-alignment window if sensor rates diverge.
    /// </summary>
    public interface ISensorAggregator
    {
        void OnDetectionFrame(DetectionFrame frame);
        void OnPressureFrame(PressureFrame frame);
        void OnTemperatureFrame(TemperatureFrame frame);
        void OnRotationFrame(RotationFrame frame);
        void OnDepthFrame(DepthFrame frame);
        void OnTensionFrame(TensionFrame frame);
        void OnLineSpeedFrame(LineSpeedFrame frame);

        event EventHandler<DownholeSensorSnapshot> SnapshotReady;
    }
}
