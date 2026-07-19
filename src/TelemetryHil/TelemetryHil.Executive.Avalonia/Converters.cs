using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using TelemetryHil.Core.Models;

namespace TelemetryHil.Executive.Avalonia;

/// <summary>
/// Value converters for the executive views. Color literals match the
/// palette in App.axaml (which the WPF executive also used).
/// </summary>
public static class Converters
{
    private static readonly IBrush Success = new SolidColorBrush(Color.Parse("#FF00C896"));
    private static readonly IBrush Warning = new SolidColorBrush(Color.Parse("#FFFFC107"));
    private static readonly IBrush Error = new SolidColorBrush(Color.Parse("#FFFF4B5C"));
    private static readonly IBrush Secondary = new SolidColorBrush(Color.Parse("#FF8890A8"));
    private static readonly IBrush WarnBg = new SolidColorBrush(Color.Parse("#1AFFC107"));
    private static readonly IBrush ErrorBg = new SolidColorBrush(Color.Parse("#1AFF4B5C"));

    /// <summary>Device status dot: Connected → green, Connecting → amber, else red.</summary>
    public static readonly IValueConverter DeviceStateToBrush =
        new FuncValueConverter<DeviceConnectionState, IBrush>(s => s switch
        {
            DeviceConnectionState.Connected => Success,
            DeviceConnectionState.Connecting => Warning,
            _ => Error
        });

    /// <summary>NATS status dot: Connected → green, Reconnecting → amber, else red.</summary>
    public static readonly IValueConverter NatsStateToBrush =
        new FuncValueConverter<NatsConnectionState, IBrush>(s => s switch
        {
            NatsConnectionState.Connected => Success,
            NatsConnectionState.Reconnecting => Warning,
            _ => Error
        });

    /// <summary>Generic bool status dot: true → green, false → red.</summary>
    public static readonly IValueConverter BoolToStatusBrush =
        new FuncValueConverter<bool, IBrush>(b => b ? Success : Error);

    public static readonly IValueConverter PassFail =
        new FuncValueConverter<bool, string>(b => b ? "Pass" : "Fail");

    public static readonly IValueConverter PassFailBrush =
        new FuncValueConverter<bool, IBrush>(b => b ? Success : Error);

    /// <summary>0 → "—", N → "⚠ N".</summary>
    public static readonly IValueConverter AnomalyCount =
        new FuncValueConverter<int, string>(n => n == 0 ? "—" : $"⚠ {n}");

    public static readonly IValueConverter CriticalToBrush =
        new FuncValueConverter<bool, IBrush>(critical => critical ? Error : Secondary);

    // Anomaly banner theming: amber for Warning, red for Critical
    public static readonly IValueConverter BannerBackground =
        new FuncValueConverter<bool, IBrush>(critical => critical ? ErrorBg : WarnBg);

    public static readonly IValueConverter BannerBorder =
        new FuncValueConverter<bool, IBrush>(critical => critical ? Error : Warning);

    public static readonly IValueConverter BannerForeground =
        new FuncValueConverter<bool, IBrush>(critical => critical ? Error : Warning);
}
