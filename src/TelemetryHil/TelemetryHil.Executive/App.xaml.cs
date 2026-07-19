using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using TelemetryHil.Core.Interfaces;
using TelemetryHil.Executive.Services;
using TelemetryHil.Hardware.Stubs;
using TelemetryHil.Executive.ViewModels;
using TelemetryHil.Executive.Views;
using static System.Net.WebRequestMethods;

namespace TelemetryHil
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            var services = new ServiceCollection();

            bool useStubs = !e.Args.Contains("--hardware");

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
                // Saleae FastAPI service, live NATS.
                // SALEAE_URL overrides the default, e.g. http://<p52s-ip>:8000
                // when the executive runs on Windows against P52s-attached hardware.
                var saleaeUrl = Environment.GetEnvironmentVariable("SALEAE_URL")
                                ?? TelemetryHil.Core.Services.HttpSignalCapture.DefaultBaseUrl;

                services.AddSingleton<ISerialDevice, Hardware.RoutingSerialDevice>();
                services.AddSingleton<ISignalCapture>(_ =>
                    new TelemetryHil.Core.Services.HttpSignalCapture(saleaeUrl));
                services.AddSingleton<ITestPublisher, Hardware.NatsTestPublisher>();
                services.AddSingleton<Core.Interfaces.IAnomalySubscriber, Hardware.NatsAnomalySubscriber>();
            }

            // ViewModels
            services.AddSingleton<MainViewModel>();

            Services = services.BuildServiceProvider();

            var window = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainViewModel>()
            };
            window.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // NATS services are IAsyncDisposable-only; a sync Dispose on the
            // provider would throw for them.
            if (Services is IAsyncDisposable ad) ad.DisposeAsync().AsTask().GetAwaiter().GetResult();
            else if (Services is IDisposable d) d.Dispose();
            base.OnExit(e);
        }
    }

}
