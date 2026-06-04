using ForgeHil.Core.Interfaces;
using ForgeHil.Executive.Services;
using ForgeHil.Executive.ViewModels;
using ForgeHil.Executive.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace ForgeHil
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var services = new ServiceCollection();

            services.AddSingleton<ISerialDevice, SerialDevice>();
            services.AddSingleton<ISignalCapture, SignalCapture>();
            services.AddSingleton<ITestPublisher, StubTestPublisher>();

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
