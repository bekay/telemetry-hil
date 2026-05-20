#include "MockSaleaeClient.h"

namespace Hil::Mocks {

void MockSaleaeClient::SetNextCaptureResult(const PulseResult& result)
{
    _nextResult = result;
}

void MockSaleaeClient::SetHealthStatus(const HealthStatus& status)
{
    _healthStatus = status;
}

std::future<PulseResult> MockSaleaeClient::CaptureAsync(const CaptureConfig& /*config*/)
{
    std::promise<PulseResult> promise;
    promise.set_value(_nextResult);
    return promise.get_future();
}

std::future<HealthStatus> MockSaleaeClient::GetHealthAsync()
{
    std::promise<HealthStatus> promise;
    promise.set_value(_healthStatus);
    return promise.get_future();
}

} // namespace Hil::Mocks
