using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Pronetsys.Desktop.Android.Views;

namespace Pronetsys.Desktop.Android;

// 'Application' es ambiguo en Android (Android.App.Application vs Avalonia.Application);
// calificamos explícitamente el base de Avalonia.
public partial class App : Avalonia.Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // Android es single-view: una sola vista raiz dentro de la Activity.
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new MainView();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
