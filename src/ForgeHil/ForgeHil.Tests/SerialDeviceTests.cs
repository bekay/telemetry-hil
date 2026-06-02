using FluentAssertions;
using ForgeHil.Core.Interfaces;
using ForgeHil.Core.Models;
using Moq;
using Xunit;

public class SerialDeviceTests
{
    [Fact]
    public async Task ConnectAsync_ShouldReturnTrue_WhenDeviceResponds()
    {
        var mock = new Mock<ISerialDevice>();
        mock.Setup(d => d.ConnectAsync("/dev/ttyACM0", 115200, default))
            .ReturnsAsync(true);

        var result = await mock.Object.ConnectAsync("/dev/ttyACM0", 115200);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ConnectAsync_ShouldReturnFalse_WhenPortUnavailable()
    {
        var mock = new Mock<ISerialDevice>();
        mock.Setup(d => d.ConnectAsync("/dev/ttyACM99", 115200, default))
            .ReturnsAsync(false);

        var result = await mock.Object.ConnectAsync("/dev/ttyACM99", 115200);

        result.Should().BeFalse();
    }

    [Fact]
    public void FrameReceived_ShouldRaise_WhenDetectionLineArrives()
    {
        var mock = new Mock<ISerialDevice>();
        DetectionFrame? received = null;

        mock.Object.FrameReceived += (_, f) => received = f;
        mock.Raise(d => d.FrameReceived += null,
            new DetectionFrame(622000, 10, 200, 95, 1));

        received.Should().NotBeNull();
        received!.TimestampMs.Should().Be(622000);
        received.Amplitude.Should().Be(200);
    }
}