using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using TelemetryHil.Core.Interfaces;
using TelemetryHil.Core.Services;
using TelemetryHil.Executive.Avalonia.ViewModels;
using TelemetryHil.Executive.Avalonia.Views;
using TelemetryHil.Hardware;
using TelemetryHil.Hardware.Stubs;

namespace TelemetryHil.Executive.Avalonia;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = new ServiceCollection();
            bool useStubs = !(desktop.Args ?? []).Contains("--hardware");

            if (useStubs)
            {
                services.AddSingleton<ISerialDevice, StubSerialDevice>();
                services.AddSingleton<ISignalCapture, StubSignalCapture>();
                services.AddSingleton<ITestPublisher, StubTestPublisher>();
                // no IAnomalySubscriber in stub mode — banner is exercised via Inject-Anomaly
            }
            else
            {
                // --hardware: real EFM32 over serial or a tcp:// ser2net bridge
                // (RoutingSerialDevice picks the transport from the port textbox),
                // Saleae FastAPI service, live NATS. On the P52s itself the
                // defaults (/dev/ttyACM0, http://localhost:8000) are already right.
                var saleaeUrl = Environment.GetEnvironmentVariable("SALEAE_URL")
                                ?? HttpSignalCapture.DefaultBaseUrl;

                services.AddSingleton<ISerialDevice, RoutingSerialDevice>();
                services.AddSingleton<ISignalCapture>(_ => new HttpSignalCapture(saleaeUrl));
                services.AddSingleton<ITestPublisher, NatsTestPublisher>();
                services.AddSingleton<IAnomalySubscriber, NatsAnomalySubscriber>();
            }

            services.AddSingleton<MainViewModel>();
            Services = services.BuildServiceProvider();

            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainViewModel>()
            };

            desktop.ShutdownRequested += (_, _) =>
            {
                if (Services is IAsyncDisposable ad)
                    ad.DisposeAsync().AsTask().GetAwaiter().GetResult();
                else if (Services is IDisposable d)
                    d.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
