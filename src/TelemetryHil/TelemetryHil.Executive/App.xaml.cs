using TelemetryHil.Core.Interfaces;
using TelemetryHil.Executive.Services;
using TelemetryHil.Executive.ViewModels;
using TelemetryHil.Executive.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

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
            }
            else
            {
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
            if (Services is IDisposable d) d.Dispose();
            base.OnExit(e);
        }
    }

}
