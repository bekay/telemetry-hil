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

    /// <summary>Pressure sensor frame. PRS:<ts>,<pressure_raw> (PSI, 0–15000)</summary>
    public record PressureFrame(
        long TimestampMs,
        double PressureRaw
    );

    /// <summary>Temperature sensor frame. TMP:<ts>,<temperature_raw> (°C, 0–175)</summary>
    public record TemperatureFrame(
        long TimestampMs,
        double TemperatureRaw
    );

    /// <summary>Rotational velocity frame. ROT:<ts>,<rotation_raw> (RPM, 0–3000)</summary>
    public record RotationFrame(
        long TimestampMs,
        double RotationRaw
    );

    /// <summary>Depth sensor frame. DEP:<ts>,<depth_raw></summary>
    public record DepthFrame(
        long TimestampMs,
        double DepthRaw          // metres
    );

    /// <summary>Tension sensor frame. TEN:<ts>,<tension_raw></summary>
    public record TensionFrame(
        long TimestampMs,
        double TensionRaw        // kN
    );

    /// <summary>Line speed sensor frame. SPD:<ts>,<speed_raw></summary>
    public record LineSpeedFrame(
        long TimestampMs,
        double SpeedRaw          // m/s
    );
}
