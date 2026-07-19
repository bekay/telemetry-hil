using FluentAssertions;
using TelemetryHil.Core.Models;
using TelemetryHil.Core.Services;
using Xunit;

public class UartLineParserTests
{
    [Fact]
    public void Parse_DetectionLine_ReturnsDetectionFrame()
    {
        var frame = UartLineParser.Parse("DET:622000,10,200,95,1");

        frame.Should().BeOfType<DetectionFrame>()
             .Which.Should().Be(new DetectionFrame(622000, 10, 200, 95, 1));
    }

    [Theory]
    [InlineData("PRS:1000,5012.5", typeof(PressureFrame), 5012.5)]
    [InlineData("TMP:1000,85.25", typeof(TemperatureFrame), 85.25)]
    [InlineData("ROT:1000,1500", typeof(RotationFrame), 1500.0)]
    [InlineData("DEP:1000,1499.02", typeof(DepthFrame), 1499.02)]
    [InlineData("TEN:1000,25.11", typeof(TensionFrame), 25.11)]
    [InlineData("SPD:1000,1.005", typeof(LineSpeedFrame), 1.005)]
    public void Parse_SensorLines_ReturnTypedFrames(string line, Type expectedType, double expectedValue)
    {
        var frame = UartLineParser.Parse(line);

        frame.Should().BeOfType(expectedType);
        var value = frame switch
        {
            PressureFrame f => f.PressureRaw,
            TemperatureFrame f => f.TemperatureRaw,
            RotationFrame f => f.RotationRaw,
            DepthFrame f => f.DepthRaw,
            TensionFrame f => f.TensionRaw,
            LineSpeedFrame f => f.SpeedRaw,
            _ => double.NaN
        };
        value.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData("NOD:5000")]
    [InlineData("ERR:SENSOR_FAULT")]
    [InlineData("OK")]
    [InlineData("RADAR-SIM-EFM32-001")]
    [InlineData("")]
    [InlineData("DET:garbage,not,numeric,at,all")]
    [InlineData("DET:1,2,3")]           // wrong field count
    [InlineData("PRS:1000")]            // missing value
    [InlineData("XYZ:1000,42")]         // unknown prefix
    public void Parse_NonFrameOrMalformedLines_ReturnNull(string line)
        => UartLineParser.Parse(line).Should().BeNull();

    [Fact]
    public void Parse_TrimsLineEndings()
        => UartLineParser.Parse("PRS:1000,5000.0\r\n").Should().BeOfType<PressureFrame>();
}
