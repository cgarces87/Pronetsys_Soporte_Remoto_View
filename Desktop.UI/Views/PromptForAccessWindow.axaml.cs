using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Pronetsys.Desktop.UI.Views;

public partial class PromptForAccessWindow : Window
{
    public PromptForAccessWindow()
    {
        InitializeComponent();
        Loaded += Window_Loaded;
    }

    private void Window_Loaded(object? sender, RoutedEventArgs e)
    {
        Topmost = false;
    }
}
