using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Media.Projection;
using Android.Runtime;
using Android.Util;
using Avalonia;
using Avalonia.Android;
using Pronetsys.Desktop.Android.Services;

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
    private const int RequestMediaProjection = 1001;
    private const string LogTag = "Pronetsys";

    public static MainActivity? Instance { get; private set; }

    private MediaProjectionManager? _mpm;
    private AndroidScreenCapturer? _capturer;
    private bool _hubEventsWired;

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        Instance = this;
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }

    protected override void OnResume()
    {
        base.OnResume();
        // Refrescar el estado del botón de accesibilidad al volver de Ajustes.
        Views.MainView.Current?.RefreshFromActivity();
    }

    /// <summary>Lanza el diálogo de consentimiento de captura de pantalla (llamado desde la UI).</summary>
    public void RequestScreenCapture()
    {
        _mpm ??= (MediaProjectionManager?)GetSystemService(MediaProjectionService);
        var intent = _mpm?.CreateScreenCaptureIntent();
        if (intent is not null)
        {
            StartActivityForResult(intent, RequestMediaProjection);
        }
    }

    protected override void OnActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        if (requestCode != RequestMediaProjection || resultCode != Result.Ok || data is null)
        {
            return;
        }

        // Android 10+/14: el foreground service (tipo mediaProjection) debe estar activo
        // antes de obtener la proyección.
        StartForegroundService(new Intent(this, typeof(ScreenCaptureForegroundService)));

        var projection = _mpm?.GetMediaProjection((int)resultCode, data);
        if (projection is null)
        {
            return;
        }

        var metrics = Resources?.DisplayMetrics;
        var width = metrics?.WidthPixels ?? 1080;
        var height = metrics?.HeightPixels ?? 1920;
        var dpi = metrics is not null ? (int)metrics.DensityDpi : 320;

        // Informar al hub las dimensiones para el DTO ScreenData y el encabezado de cada frame.
        var hub = Views.MainView.ActiveHub;
        hub?.SetCaptureInfo(width, height);

        // Reflejar el estado de la sesión en la notificación persistente (una sola suscripción).
        if (hub is not null && !_hubEventsWired)
        {
            _hubEventsWired = true;
            hub.SessionActiveChanged += active =>
                ScreenCaptureForegroundService.UpdateStatus(active
                    ? "Un técnico está viendo tu pantalla."
                    : "Sesión de soporte activa: listo para compartir.");
        }

        _capturer?.Dispose();
        _capturer = new AndroidScreenCapturer();
        _capturer.FrameEncoded += bytes =>
        {
            // Entregar el JPEG al hub para que lo transmita al técnico (si hay sesión activa).
            Views.MainView.ActiveHub?.PushFrame(bytes);
        };
        _capturer.Start(projection, width, height, dpi);

        Log.Info(LogTag, $"Captura de pantalla iniciada ({width}x{height}).");
    }

    /// <summary>Detiene la captura y el compartir (lo llama la acción "Detener" de la notificación).</summary>
    public void StopScreenCapture()
    {
        Views.MainView.ActiveHub?.StopSharing();
        _capturer?.Dispose();
        _capturer = null;
        Log.Info(LogTag, "Captura de pantalla detenida por el usuario.");
    }

    /// <summary>Abre Ajustes → Accesibilidad para que el usuario habilite el control remoto.</summary>
    public void OpenAccessibilitySettings()
    {
        var intent = new Intent(global::Android.Provider.Settings.ActionAccessibilitySettings);
        intent.AddFlags(ActivityFlags.NewTask);
        StartActivity(intent);
    }

    /// <summary>True si el servicio de accesibilidad de esta app ya está habilitado.</summary>
    public bool IsAccessibilityEnabled()
    {
        try
        {
            var enabled = global::Android.Provider.Settings.Secure.GetString(
                ContentResolver,
                global::Android.Provider.Settings.Secure.EnabledAccessibilityServices);
            return enabled?.Contains(PackageName ?? "com.pronetsys.asistenciaremota") == true;
        }
        catch
        {
            return false;
        }
    }
}
