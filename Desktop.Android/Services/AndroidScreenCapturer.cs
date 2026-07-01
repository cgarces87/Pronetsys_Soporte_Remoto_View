using Android.Hardware.Display;
using Android.Media;
using Android.Media.Projection;

namespace Pronetsys.Desktop.Android.Services;

/// <summary>
/// Fase 1 — captura de pantalla vía MediaProjection. El <see cref="MediaProjection"/> se obtiene
/// tras el consentimiento del usuario (diálogo del sistema, lanzado desde la Activity). Los frames
/// llegan por <see cref="ImageReader"/>; se codifican (SkiaSharp → JPEG) y se envían por el hub.
///
/// Debe correr con un foreground service activo (ver <see cref="ScreenCaptureForegroundService"/>).
/// </summary>
public class AndroidScreenCapturer : IDisposable
{
    private MediaProjection? _projection;
    private VirtualDisplay? _virtualDisplay;
    private ImageReader? _imageReader;

    /// <summary>Evento con cada frame ya codificado (JPEG) listo para enviar por el hub.</summary>
    public event Action<byte[]>? FrameEncoded;

    public void Start(MediaProjection projection, int width, int height, int densityDpi)
    {
        _projection = projection;

        _imageReader = ImageReader.NewInstance(width, height, ImageFormatType.Rgba8888, maxImages: 2);

        _virtualDisplay = _projection.CreateVirtualDisplay(
            name: "PronetsysCapture",
            width: width,
            height: height,
            densityDpi: densityDpi,
            flags: (DisplayFlags)VirtualDisplayFlags.AutoMirror,
            surface: _imageReader.Surface,
            callback: null,
            handler: null);

        // TODO Fase 1: SetOnImageAvailableListener -> leer el Image (planes/rowStride),
        // construir SKBitmap, comparar con el frame anterior (solo enviar cambios),
        // codificar a JPEG con calidad ajustable y disparar FrameEncoded.
    }

    public void Dispose()
    {
        _virtualDisplay?.Release();
        _imageReader?.Close();
        _projection?.Stop();
        GC.SuppressFinalize(this);
    }
}
