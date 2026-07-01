using Android.Graphics;
using Android.Hardware.Display;
using Android.Media;
using Android.Media.Projection;
using SkiaSharp;

namespace Pronetsys.Desktop.Android.Services;

/// <summary>
/// Fase 1 — captura de pantalla vía MediaProjection. El <see cref="MediaProjection"/> se obtiene
/// tras el consentimiento del usuario (diálogo del sistema, lanzado desde <see cref="MainActivity"/>).
/// Cada frame llega por <see cref="ImageReader"/>, se convierte a <see cref="SKBitmap"/> y se
/// codifica a JPEG. El JPEG se entrega por <see cref="FrameEncoded"/> para que la capa de red
/// (DesktopHubConnectionAndroid) lo empaquete y envíe por el hub (siguiente incremento).
///
/// Debe correr con un foreground service activo (ver <see cref="ScreenCaptureForegroundService"/>).
/// </summary>
public class AndroidScreenCapturer : IDisposable
{
    private MediaProjection? _projection;
    private VirtualDisplay? _virtualDisplay;
    private ImageReader? _imageReader;

    public int Width { get; private set; }
    public int Height { get; private set; }

    /// <summary>Cada frame ya codificado a JPEG, listo para enviar por el hub.</summary>
    public event Action<byte[]>? FrameEncoded;

    public void Start(MediaProjection projection, int width, int height, int densityDpi)
    {
        _projection = projection;
        Width = width;
        Height = height;

        _imageReader = ImageReader.NewInstance(width, height, (ImageFormatType)Format.Rgba8888, maxImages: 2);
        _imageReader.SetOnImageAvailableListener(new FrameListener(this), handler: null);

        // flags 0 = sin banderas especiales; la superficie del ImageReader recibe el contenido
        // reflejado de la pantalla. (Se puede afinar en dispositivo si algún OEM lo requiere.)
        _virtualDisplay = _projection.CreateVirtualDisplay(
            name: "PronetsysCapture",
            width: width,
            height: height,
            dpi: densityDpi,
            flags: 0,
            surface: _imageReader.Surface,
            callback: null,
            handler: null);
    }

    // Lee la imagen del ImageReader (RGBA_8888, con posible padding por rowStride),
    // arma un SKBitmap y lo codifica a JPEG.
    private void ProcessAvailableImage()
    {
        using var image = _imageReader?.AcquireLatestImage();
        if (image is null)
        {
            return;
        }

        try
        {
            var planes = image.GetPlanes();
            if (planes is null || planes.Length == 0)
            {
                return;
            }

            var buffer = planes[0].Buffer;
            if (buffer is null)
            {
                return;
            }
            var pixelStride = planes[0].PixelStride;
            var rowStride = planes[0].RowStride;
            var rowPadding = rowStride - pixelStride * Width;

            // El ancho real de la bitmap incluye el padding de fila; luego recortamos.
            var bitmapWidth = Width + rowPadding / pixelStride;

            var info = new SKImageInfo(bitmapWidth, Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var bitmap = new SKBitmap(info);

            var length = buffer.Remaining();
            var managed = new byte[length];
            buffer.Get(managed);
            System.Runtime.InteropServices.Marshal.Copy(managed, 0, bitmap.GetPixels(), length);

            // Recorte al ancho real (sin el padding) si hizo falta.
            using var surface = SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul));
            if (surface is null)
            {
                return;
            }
            surface.Canvas.DrawBitmap(bitmap, 0, 0);
            using var snapshot = surface.Snapshot();
            using var data = snapshot?.Encode(SKEncodedImageFormat.Jpeg, 70);
            if (data is not null)
            {
                FrameEncoded?.Invoke(data.ToArray());
            }
        }
        finally
        {
            image.Close();
        }
    }

    public void Dispose()
    {
        _virtualDisplay?.Release();
        _imageReader?.Close();
        _projection?.Stop();
        GC.SuppressFinalize(this);
    }

    // Listener de frames del ImageReader.
    private sealed class FrameListener : Java.Lang.Object, ImageReader.IOnImageAvailableListener
    {
        private readonly AndroidScreenCapturer _owner;
        public FrameListener(AndroidScreenCapturer owner) => _owner = owner;
        public void OnImageAvailable(ImageReader? reader) => _owner.ProcessAvailableImage();
    }
}
