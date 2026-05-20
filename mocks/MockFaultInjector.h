#pragma once
#include "../interfaces/IFaultInjector.h"
#include <vector>

namespace Hil::Mocks {

struct InjectionRecord {
    FaultType   fault;
    std::string param;
};

/// Mock fault injector for use in CI and unit tests.
/// Records all injections for assertion in tests.
class MockFaultInjector : public IFaultInjector {
public:
    MockFaultInjector() = default;

    /// Returns all fault injections that were requested — use for assertions.
    const std::vector<InjectionRecord>& InjectionHistory() const;

    /// Returns true if ResetAsync was called since last injection.
    bool WasReset() const;

    // IFaultInjector
    std::future<void> InjectAsync(FaultType fault,
                                  const std::string& param = "") override;
    std::future<void> ResetAsync() override;
    bool IsConnected() const override;
    void Connect() override;
    void Disconnect() override;

private:
    bool                         _connected = true;
    bool                         _wasReset  = false;
    std::vector<InjectionRecord> _history;
};

} // namespace Hil::Mocks
