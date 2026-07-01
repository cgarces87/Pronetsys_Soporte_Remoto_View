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

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        Instance = this;
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
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

        _capturer?.Dispose();
        _capturer = new AndroidScreenCapturer();
        _capturer.FrameEncoded += bytes =>
            Log.Info(LogTag, $"Frame JPEG capturado: {bytes.Length} bytes ({width}x{height})");
        _capturer.Start(projection, width, height, dpi);

        Log.Info(LogTag, "Captura de pantalla iniciada.");
    }
}
