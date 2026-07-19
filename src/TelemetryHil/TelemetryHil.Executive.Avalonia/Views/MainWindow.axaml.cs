using Avalonia.Controls;

namespace TelemetryHil.Executive.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Auto-scroll the UART log to the bottom whenever new text arrives
        var logBox = this.FindControl<TextBox>("UartLogBox");
        if (logBox is not null)
        {
            logBox.PropertyChanged += (_, e) =>
            {
                if (e.Property == TextBox.TextProperty && logBox.Text is { } text)
                    logBox.CaretIndex = text.Length;
            };
        }
    }
}
