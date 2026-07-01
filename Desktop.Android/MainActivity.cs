using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;

namespace Pronetsys.Desktop.Android;

// Punto de entrada Android. Avalonia corre en modo single-view dentro de esta Activity.
[Activity(
    Label = "Pronetsys Asistencia Remota",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    // Referencia estatica para que los servicios (captura/accesibilidad) alcancen el contexto.
    public static MainActivity? Instance { get; private set; }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        Instance = this;
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
