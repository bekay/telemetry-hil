#include "MockFaultInjector.h"

namespace Hil::Mocks {

const std::vector<InjectionRecord>& MockFaultInjector::InjectionHistory() const
{
    return _history;
}

bool MockFaultInjector::WasReset() const
{
    return _wasReset;
}

std::future<void> MockFaultInjector::InjectAsync(FaultType fault,
                                                  const std::string& param)
{
    _history.push_back({ fault, param });
    _wasReset = false;

    std::promise<void> promise;
    promise.set_value();
    return promise.get_future();
}

std::future<void> MockFaultInjector::ResetAsync()
{
    _wasReset = true;

    std::promise<void> promise;
    promise.set_value();
    return promise.get_future();
}

bool MockFaultInjector::IsConnected() const
{
    return _connected;
}

void MockFaultInjector::Connect()
{
    _connected = true;
}

void MockFaultInjector::Disconnect()
{
    _connected = false;
}

} // namespace Hil::Mocks
