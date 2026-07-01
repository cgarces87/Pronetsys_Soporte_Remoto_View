using Android.Media.Projection;

namespace Pronetsys.Desktop.Android.Services;

/// <summary>
/// Fase 1 — captura de pantalla vía MediaProjection. El <see cref="MediaProjection"/> se obtiene
/// tras el consentimiento del usuario (diálogo del sistema, lanzado desde la Activity). En la
/// Fase 1 se implementa: ImageReader + VirtualDisplay + listener de frames → SKBitmap → JPEG
/// (SkiaSharp) → enviar por el hub. Debe correr con un foreground service activo
/// (ver <see cref="ScreenCaptureForegroundService"/>).
///
/// Por ahora es un stub que compila; el cableado exacto de la API de Android se hace en Fase 1
/// contra un dispositivo/emulador.
/// </summary>
public class AndroidScreenCapturer : IDisposable
{
    private MediaProjection? _projection;

    /// <summary>Cada frame ya codificado (JPEG) listo para enviar por el hub.</summary>
    public event Action<byte[]>? FrameEncoded;

    public void Start(MediaProjection projection, int width, int height, int densityDpi)
    {
        _projection = projection;

        // TODO Fase 1:
        //   var reader = ImageReader.NewInstance(width, height, (int)Android.Graphics.Format.Rgba8888, 2);
        //   _projection.CreateVirtualDisplay("PronetsysCapture", width, height, densityDpi,
        //       (int)Android.Hardware.Display.DisplayManagerFlags.VirtualDisplayFlagAutoMirror,
        //       reader.Surface, callback: null, handler: null);
        //   reader.SetOnImageAvailableListener(...) -> leer planes -> SKBitmap -> JPEG -> FrameEncoded.
        _ = FrameEncoded; // evita CS0067 mientras es stub
    }

    public void Dispose()
    {
        _projection?.Stop();
        GC.SuppressFinalize(this);
    }
}
