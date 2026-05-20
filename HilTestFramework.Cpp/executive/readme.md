# C# test executive

Provides the test executive operator an interface to configure and run HIL sequences on a EFM32 Pearl Gecko Development board running simulated telemetry (pressure, temperature, and rotational) over UART. This application is the layer to use to operate the parent project, hil-test-framework. Test executive app will have access to test tool integration like a Saleae logic analyzer and fault injector. Test logic layer planned to be pytest project.

## Stack
- .NET 10 / C#/ WPF