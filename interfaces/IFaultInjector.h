#pragma once
#include <future>
#include <string>

namespace Hil {

enum class FaultType {
    BitFlip,
    FramingError,
    TimingViolation,
    OutOfRange
};

/// Abstraction over the Microstick II USB fault injector.
/// Sits physically on the UART line between EFM32 and agent machine.
/// Real implementation: MicrostickFaultInjector (USB serial to Microstick)
/// Mock implementation: MockFaultInjector (records injections for assertion)
class IFaultInjector {
public:
    virtual ~IFaultInjector() = default;

    /// Inject a fault pattern into the UART line.
    /// @param fault  The fault type to inject.
    /// @param param  Optional parameter (e.g. OOB value).
    virtual std::future<void> InjectAsync(FaultType fault,
                                          const std::string& param = "") = 0;

    /// Clear any active injection and return to pass-through mode.
    virtual std::future<void> ResetAsync() = 0;

    /// Returns true if the USB connection to the Microstick is open.
    virtual bool IsConnected() const = 0;

    /// Open the USB serial connection.
    virtual void Connect() = 0;

    /// Close the USB serial connection.
    virtual void Disconnect() = 0;
};

} // namespace Hil
