#pragma once
#include <vector>
#include <future>
#include <string>

namespace Hil {

struct CaptureConfig {
    double duration_seconds = 2.0;
    std::vector<int> channels = { 0 };  // 0=UART, 1=I2C SDA, 2=I2C SCL
};

struct PulseResult {
    int     channel           = 0;
    int     pulse_count       = 0;
    double  avg_pulse_width_us = 0.0;
    double  min_pulse_width_us = 0.0;
    double  max_pulse_width_us = 0.0;
    double  capture_duration_s = 0.0;
};

struct HealthStatus {
    bool        saleae_connected = false;
    std::string status;
};

/// Abstraction over the Saleae Logic 2 signal capture service.
/// Real implementation: SaleaeHttpClient (HTTP to Python FastAPI wrapper)
/// Mock implementation: MockSaleaeClient (returns scripted capture data)
class ISaleaeClient {
public:
    virtual ~ISaleaeClient() = default;

    /// Trigger a capture and return pulse metrics.
    virtual std::future<PulseResult> CaptureAsync(const CaptureConfig& config) = 0;

    /// Check liveness of the Saleae service and device connection.
    virtual std::future<HealthStatus> GetHealthAsync() = 0;
};

} // namespace Hil
