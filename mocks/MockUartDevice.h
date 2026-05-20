#pragma once
#include "../interfaces/IUartDevice.h"
#include <queue>
#include <string>

namespace Hil::Mocks {

/// Mock UART device for use in CI and unit tests.
/// Pre-load responses via EnqueueResponse() before calling SendCommandAsync().
class MockUartDevice : public IUartDevice {
public:
    MockUartDevice() = default;

    /// Queue a response that will be returned by the next SendCommandAsync call.
    void EnqueueResponse(const std::string& response);

    /// Returns all commands that were sent — use for assertions.
    const std::vector<std::string>& SentCommands() const;

    // IUartDevice
    std::future<std::string> SendCommandAsync(const std::string& command) override;
    std::future<std::string> ReadLineAsync() override;
    bool IsConnected() const override;
    void Connect() override;
    void Disconnect() override;

private:
    bool                     _connected = true;
    std::queue<std::string>  _responses;
    std::vector<std::string> _sentCommands;
};

} // namespace Hil::Mocks
