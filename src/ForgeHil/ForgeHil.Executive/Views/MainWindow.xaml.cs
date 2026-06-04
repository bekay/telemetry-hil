using System.Windows;
using System.Windows.Controls;

namespace ForgeHil.Executive.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    // Auto-scroll the UART log to the bottom on text change
    private void UartLogBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.ScrollToEnd();
        }
    }
}
