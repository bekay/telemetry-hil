namespace TelemetryHil.Core.Models
{
    /// <summary>
    /// Full downhole sensor suite snapshot — stitched upstream from individual frames.
    /// All six sensors simulated from Phase 1. Physical hardware in Phase 2:
    ///   PTS  — EFM32 onboard I2C breakout
    ///   Depth/Tension/LineSpeed — Dragonboard I2C slave via Pearl Gecko sensor hub
    /// </summary>
    public record DownholeSensorSnapshot(
        long TimestampMs,
        double? PressureRaw,
        double? TemperatureRaw,
        double? RotationRaw,
        double? DepthRaw,
        double? TensionRaw,
        double? LineSpeedRaw
    );
}
