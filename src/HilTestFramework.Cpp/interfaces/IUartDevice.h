#pragma once
#include <string>
#include <future>

namespace Hil {

/// Abstraction over a UART serial connection to a device under test.
/// Real implementation: EfmUartDevice (SerialPort to EFM32)
/// Mock implementation: MockUartDevice (returns scripted responses)
class IUartDevice {
public:
    virtual ~IUartDevice() = default;

    /// Send a command string and return the response line.
    virtual std::future<std::string> SendCommandAsync(const std::string& command) = 0;

    /// Read the next available line from the device.
    virtual std::future<std::string> ReadLineAsync() = 0;

    /// Returns true if the serial connection is open and healthy.
    virtual bool IsConnected() const = 0;

    /// Open the serial connection.
    virtual void Connect() = 0;

    /// Close the serial connection.
    virtual void Disconnect() = 0;
};

} // namespace Hil
