#include <gtest/gtest.h>
#include "../mocks/MockUartDevice.h"
#include "../mocks/MockSaleaeClient.h"
#include "../mocks/MockFaultInjector.h"

using namespace Hil;
using namespace Hil::Mocks;

// ---------------------------------------------------------------------------
// Test fixture
// ---------------------------------------------------------------------------
class UartFaultFixture : public ::testing::Test {
protected:
    MockUartDevice   uart;
    MockSaleaeClient saleae;
    MockFaultInjector faultInjector;
};

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

TEST_F(UartFaultFixture, FramingError_InjectionRecorded)
{
    faultInjector.InjectAsync(FaultType::FramingError).get();

    const auto& history = faultInjector.InjectionHistory();
    ASSERT_EQ(history.size(), 1u);
    EXPECT_EQ(history[0].fault, FaultType::FramingError);
}

TEST_F(UartFaultFixture, BitFlip_InjectionRecorded)
{
    faultInjector.InjectAsync(FaultType::BitFlip).get();

    const auto& history = faultInjector.InjectionHistory();
    ASSERT_EQ(history.size(), 1u);
    EXPECT_EQ(history[0].fault, FaultType::BitFlip);
}

TEST_F(UartFaultFixture, OutOfRange_InjectionWithParam)
{
    faultInjector.InjectAsync(FaultType::OutOfRange, "9999").get();

    const auto& history = faultInjector.InjectionHistory();
    ASSERT_EQ(history.size(), 1u);
    EXPECT_EQ(history[0].fault, FaultType::OutOfRange);
    EXPECT_EQ(history[0].param, "9999");
}

TEST_F(UartFaultFixture, Reset_ClearsInjectionState)
{
    faultInjector.InjectAsync(FaultType::TimingViolation).get();
    faultInjector.ResetAsync().get();

    EXPECT_TRUE(faultInjector.WasReset());
}

TEST_F(UartFaultFixture, MultipleInjections_AllRecorded)
{
    faultInjector.InjectAsync(FaultType::BitFlip).get();
    faultInjector.InjectAsync(FaultType::FramingError).get();
    faultInjector.InjectAsync(FaultType::TimingViolation).get();

    EXPECT_EQ(faultInjector.InjectionHistory().size(), 3u);
}

TEST_F(UartFaultFixture, SaleaeCapture_AfterFaultInjection_CaptureStillValid)
{
    // Inject fault then verify Saleae can still capture
    faultInjector.InjectAsync(FaultType::FramingError).get();

    PulseResult degraded;
    degraded.pulse_count = 1;
    degraded.avg_pulse_width_us = 10.0;
    saleae.SetNextCaptureResult(degraded);

    auto result = saleae.CaptureAsync({}).get();

    // After fault injection pulse count should be lower than normal (5)
    EXPECT_LT(result.pulse_count, 5);
}