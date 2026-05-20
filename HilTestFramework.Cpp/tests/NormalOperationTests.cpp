#include <gtest/gtest.h>
#include <regex>
#include "../mocks/MockUartDevice.h"
#include "../mocks/MockSaleaeClient.h"

using namespace Hil;
using namespace Hil::Mocks;

// ---------------------------------------------------------------------------
// Test fixture — shared setup for normal operation tests
// ---------------------------------------------------------------------------
class NormalOperationFixture : public ::testing::Test {
protected:
    MockUartDevice  uart;
    MockSaleaeClient saleae;

    void SetUp() override
    {
        // Simulate EFM32 identity handshake passing
        uart.EnqueueResponse("RADAR-SIM-EFM32-001\r\n");
        uart.EnqueueResponse("OK\r\n"); // ST? response
    }
};

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

TEST_F(NormalOperationFixture, DeviceIdentity_ReturnsExpectedId)
{
    auto response = uart.SendCommandAsync("ID?").get();
    EXPECT_EQ(response, "RADAR-SIM-EFM32-001\r\n");
}

TEST_F(NormalOperationFixture, DeviceStatus_ReturnsOk)
{
    // consume ID? response first
    uart.SendCommandAsync("ID?").get();

    auto response = uart.SendCommandAsync("ST?").get();
    EXPECT_EQ(response, "OK\r\n");
}

TEST_F(NormalOperationFixture, SaleaeCapture_ReturnsPulseData)
{
    PulseResult expected;
    expected.channel = 0;
    expected.pulse_count = 5;
    expected.avg_pulse_width_us = 10.0;
    expected.capture_duration_s = 2.0;
    saleae.SetNextCaptureResult(expected);

    CaptureConfig config;
    config.duration_seconds = 2.0;
    config.channels = { 0 };

    auto result = saleae.CaptureAsync(config).get();

    EXPECT_EQ(result.pulse_count, 5);
    EXPECT_NEAR(result.avg_pulse_width_us, 10.0, 0.01);
}

TEST_F(NormalOperationFixture, SaleaeCapture_ValidatesPulseWidthInRange)
{
    // Normal operation: pulse width should be 10us ± 2us
    PulseResult result;
    result.pulse_count = 3;
    result.avg_pulse_width_us = 10.5;
    saleae.SetNextCaptureResult(result);

    auto capture = saleae.CaptureAsync({}).get();

    EXPECT_GE(capture.avg_pulse_width_us, 8.0) << "Pulse width below minimum";
    EXPECT_LE(capture.avg_pulse_width_us, 12.0) << "Pulse width above maximum";
}

TEST_F(NormalOperationFixture, CommandsSent_RecordedCorrectly)
{
    uart.SendCommandAsync("ID?").get();
    uart.SendCommandAsync("ST?").get();

    const auto& sent = uart.SentCommands();
    ASSERT_EQ(sent.size(), 2u);
    EXPECT_EQ(sent[0], "ID?");
    EXPECT_EQ(sent[1], "ST?");
}