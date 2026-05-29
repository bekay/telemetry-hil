namespace ForgeHil.Core.Models
{
    // ── Sensor frames ─────────────────────────────────────────────────────────────

    /// <summary>EFM32 radar simulator detection frame. DET:<ts>,<width>,<amp>,<qual>,<ch></summary>
    public record DetectionFrame(
        long TimestampMs,
        int PulseWidth,
        int Amplitude,
        int Quality,
        int Channel
    );

    /// <summary>Depth sensor frame. DEP:<ts>,<depth_raw></summary>
    public record DepthFrame(
        long TimestampMs,
        double DepthRaw          // metres, store raw — UI converts
    );

    /// <summary>Tension sensor frame. TEN:<ts>,<tension_raw></summary>
    public record TensionFrame(
        long TimestampMs,
        double TensionRaw        // kN, store raw — UI converts
    );

    /// <summary>Line speed sensor frame. SPD:<ts>,<speed_raw></summary>
    public record LineSpeedFrame(
        long TimestampMs,
        double SpeedRaw          // m/s, store raw — UI converts
    );
}
