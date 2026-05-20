#include "MockUartDevice.h"
#include <stdexcept>

namespace Hil::Mocks {

void MockUartDevice::EnqueueResponse(const std::string& response)
{
    _responses.push(response);
}

const std::vector<std::string>& MockUartDevice::SentCommands() const
{
    return _sentCommands;
}

std::future<std::string> MockUartDevice::SendCommandAsync(const std::string& command)
{
    _sentCommands.push_back(command);

    std::string response;
    if (!_responses.empty()) {
        response = _responses.front();
        _responses.pop();
    }

    std::promise<std::string> promise;
    promise.set_value(response);
    return promise.get_future();
}

std::future<std::string> MockUartDevice::ReadLineAsync()
{
    std::string line;
    if (!_responses.empty()) {
        line = _responses.front();
        _responses.pop();
    }

    std::promise<std::string> promise;
    promise.set_value(line);
    return promise.get_future();
}

bool MockUartDevice::IsConnected() const
{
    return _connected;
}

void MockUartDevice::Connect()
{
    _connected = true;
}

void MockUartDevice::Disconnect()
{
    _connected = false;
}

} // namespace Hil::Mocks
