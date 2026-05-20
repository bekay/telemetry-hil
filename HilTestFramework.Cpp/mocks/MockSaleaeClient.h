#pragma once
#include "../interfaces/ISaleaeClient.h"

namespace Hil::Mocks {

/// Mock Saleae client for use in CI and unit tests.
/// Returns configurable scripted capture results.
class MockSaleaeClient : public ISaleaeClient {
public:
    MockSaleaeClient() = default;

    /// Configure the pulse result returned by the next CaptureAsync call.
    void SetNextCaptureResult(const PulseResult& result);

    /// Configure the health status returned by GetHealthAsync.
    void SetHealthStatus(const HealthStatus& status);

    // ISaleaeClient
    std::future<PulseResult>  CaptureAsync(const CaptureConfig& config) override;
    std::future<HealthStatus> GetHealthAsync() override;

private:
    PulseResult  _nextResult  { .pulse_count = 5,
                                .avg_pulse_width_us = 10.0,
                                .capture_duration_s = 2.0 };
    HealthStatus _healthStatus{ .saleae_connected = true,
                                .status = "ok" };
};

} // namespace Hil::Mocks
