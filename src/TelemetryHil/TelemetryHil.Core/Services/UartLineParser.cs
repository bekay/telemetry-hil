using System.Globalization;
using TelemetryHil.Core.Models;

namespace TelemetryHil.Core.Services
{
    /// <summary>
    /// Parses raw UART lines from the EFM32 into typed frames.
    /// Line types: DET / PRS / TMP / ROT / DEP / TEN / SPD (typed frames),
    /// NOD / ERR / OK and command replies pass through as unparsed.
    /// </summary>
    public static class UartLineParser
    {
        /// <summary>
        /// Parses a single UART line. Returns the typed frame record
        /// (DetectionFrame, PressureFrame, …) or null when the line is not a
        /// sensor frame (NOD:, ERR:, OK, ID reply, garbage).
        /// </summary>
        public static object? Parse(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;
            line = line.Trim();

            var colon = line.IndexOf(':');
            if (colon != 3) return null;

            var prefix = line[..3];
            var fields = line[4..].Split(',');

            try
            {
                switch (prefix)
                {
                    case "DET":
                        if (fields.Length != 5) return null;
                        return new DetectionFrame(
                            ParseLong(fields[0]),
                            (int)ParseLong(fields[1]),
                            (int)ParseLong(fields[2]),
                            (int)ParseLong(fields[3]),
                            (int)ParseLong(fields[4]));
                    case "PRS":
                        return TwoField(fields, (ts, v) => new PressureFrame(ts, v));
                    case "TMP":
                        return TwoField(fields, (ts, v) => new TemperatureFrame(ts, v));
                    case "ROT":
                        return TwoField(fields, (ts, v) => new RotationFrame(ts, v));
                    case "DEP":
                        return TwoField(fields, (ts, v) => new DepthFrame(ts, v));
                    case "TEN":
                        return TwoField(fields, (ts, v) => new TensionFrame(ts, v));
                    case "SPD":
                        return TwoField(fields, (ts, v) => new LineSpeedFrame(ts, v));
                    default:
                        return null;
                }
            }
            catch (FormatException) { return null; }
            catch (OverflowException) { return null; }
        }

        private static object? TwoField<T>(string[] fields, Func<long, double, T> make) where T : class
            => fields.Length == 2 ? make(ParseLong(fields[0]), ParseDouble(fields[1])) : null;

        private static long ParseLong(string s) =>
            long.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);

        private static double ParseDouble(string s) =>
            double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
