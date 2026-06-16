using Pronetsys.Desktop.Shared.Abstractions;
using Pronetsys.Desktop.Shared.Services;
using Microsoft.Extensions.Logging;
using Pronetsys.Shared.Primitives;
using SkiaSharp;
using System.Drawing;
using Pronetsys.Desktop.Native.MacOS;

namespace Pronetsys.Desktop.MacOS.Services;

/// <summary>
/// Screen capturer for macOS using CoreGraphics (CGDisplayCreateImage).
/// Mirrors <c>ScreenCapturerLinux</c>.
///
/// Caveats (to validate on real hardware):
///  - Requires the "Screen Recording" privacy permission, or frames come back empty.
///  - CGDisplayBounds reports points; on Retina/HiDPI displays the captured image
///    is larger (2x) than the bounds. Input coordinate mapping uses point-space
///    bounds, which is correct for CGEvent, but the diff/cropping math operates in
///    pixel space. A scale factor pass is a TODO for HiDPI correctness.
///  - CGDisplayCreateImage is deprecated in macOS 14 (ScreenCaptureKit replacement).
/// </summary>
public class ScreenCapturerMac : IScreenCapturer
{
    private readonly IImageHelper _imageHelper;
    private readonly ILogger<ScreenCapturerMac> _logger;
    private readonly object _screenBoundsLock = new();
    private readonly Dictionary<string, uint> _displays = new();
    private SKBitmap? _currentFrame;
    private SKBitmap? _previousFrame;

    public ScreenCapturerMac(
        IImageHelper imageHelper,
        ILogger<ScreenCapturerMac> logger)
    {
        _imageHelper = imageHelper;
        _logger = logger;
        Init();
    }

    public event EventHandler<Rectangle>? ScreenChanged;

    public bool CaptureFullscreen { get; set; } = true;
    public Rectangle CurrentScreenBounds { get; private set; }
    public bool IsGpuAccelerated => false;
    public string SelectedScreen { get; private set; } = string.Empty;

    public void Dispose()
    {
        _currentFrame?.Dispose();
        _previousFrame?.Dispose();
        GC.SuppressFinalize(this);
    }

    public IEnumerable<string> GetDisplayNames() => _displays.Keys;

    public SKRect GetFrameDiffArea()
    {
        if (_currentFrame is null)
        {
            return SKRect.Empty;
        }

        return _imageHelper.GetDiffArea(_currentFrame, _previousFrame, CaptureFullscreen);
    }

    public Result<SKBitmap> GetImageDiff()
    {
        if (_currentFrame is null)
        {
            return Result.Fail<SKBitmap>("Current frame is null.");
        }

        return _imageHelper.GetImageDiff(_currentFrame, _previousFrame);
    }

    public Result<SKBitmap> GetNextFrame()
    {
        lock (_screenBoundsLock)
        {
            try
            {
                if (_currentFrame != null)
                {
                    _previousFrame?.Dispose();
                    _previousFrame = _currentFrame;
                }

                _currentFrame = GetMacCapture();
                return Result.Ok(_currentFrame);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting next frame.");
                Init();
                return Result.Fail<SKBitmap>(ex);
            }
        }
    }

    public int GetScreenCount() => _displays.Count;

    public Rectangle GetVirtualScreenBounds()
    {
        var lowestX = 0;
        var highestX = 0;
        var lowestY = 0;
        var highestY = 0;

        foreach (var display in _displays.Values)
        {
            var bounds = CoreGraphics.CGDisplayBounds(display);
            lowestX = Math.Min(lowestX, (int)bounds.Origin.X);
            highestX = Math.Max(highestX, (int)(bounds.Origin.X + bounds.Size.Width));
            lowestY = Math.Min(lowestY, (int)bounds.Origin.Y);
            highestY = Math.Max(highestY, (int)(bounds.Origin.Y + bounds.Size.Height));
        }

        return new Rectangle(lowestX, lowestY, highestX - lowestX, highestY - lowestY);
    }

    public void Init()
    {
        try
        {
            CaptureFullscreen = true;
            _displays.Clear();

            CoreGraphics.CGGetActiveDisplayList(0, null, out var displayCount);

            if (displayCount > 0)
            {
                var displayIds = new uint[displayCount];
                CoreGraphics.CGGetActiveDisplayList(displayCount, displayIds, out displayCount);

                for (var i = 0; i < displayCount; i++)
                {
                    var bounds = CoreGraphics.CGDisplayBounds(displayIds[i]);
                    _logger.LogInformation("Found display {index}: {width}x{height} at ({x},{y}).",
                        i, bounds.Size.Width, bounds.Size.Height, bounds.Origin.X, bounds.Origin.Y);
                    _displays.Add(i.ToString(), displayIds[i]);
                }
            }

            if (_displays.Count == 0)
            {
                _logger.LogWarning("No active displays returned by CoreGraphics.");
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedScreen) ||
                !_displays.ContainsKey(SelectedScreen))
            {
                SelectedScreen = _displays.Keys.First();
                RefreshCurrentScreenBounds();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while initializing.");
        }
    }

    public void SetSelectedScreen(string displayName)
    {
        lock (_screenBoundsLock)
        {
            try
            {
                _logger.LogInformation("Setting display to {displayName}.", displayName);
                if (displayName == SelectedScreen)
                {
                    return;
                }

                SelectedScreen = _displays.ContainsKey(displayName)
                    ? displayName
                    : _displays.Keys.First();

                RefreshCurrentScreenBounds();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while setting selected display.");
            }
        }
    }

    private SKBitmap GetMacCapture()
    {
        var displayId = _displays[SelectedScreen];
        var imageRef = CoreGraphics.CGDisplayCreateImage(displayId);

        if (imageRef == nint.Zero)
        {
            // Most commonly this means the Screen Recording permission is missing.
            return new SKBitmap(Math.Max(1, CurrentScreenBounds.Width), Math.Max(1, CurrentScreenBounds.Height));
        }

        try
        {
            var width = (int)CoreGraphics.CGImageGetWidth(imageRef);
            var height = (int)CoreGraphics.CGImageGetHeight(imageRef);
            var bytesPerRow = (int)CoreGraphics.CGImageGetBytesPerRow(imageRef);
            var provider = CoreGraphics.CGImageGetDataProvider(imageRef);
            var data = CoreGraphics.CGDataProviderCopyData(provider);

            try
            {
                var srcPtr = CoreGraphics.CFDataGetBytePtr(data);
                if (srcPtr == nint.Zero)
                {
                    return new SKBitmap(Math.Max(1, width), Math.Max(1, height));
                }

                // CGDisplayCreateImage returns 32-bit little-endian ARGB,
                // i.e. BGRA byte order, which matches SKColorType.Bgra8888.
                var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                var destPtr = bitmap.GetPixels();
                var destRowBytes = bitmap.RowBytes;
                var rowCopyBytes = Math.Min(bytesPerRow, destRowBytes);

                unsafe
                {
                    var src = (byte*)srcPtr.ToPointer();
                    var dest = (byte*)destPtr.ToPointer();
                    for (var y = 0; y < height; y++)
                    {
                        Buffer.MemoryCopy(
                            src + (long)y * bytesPerRow,
                            dest + (long)y * destRowBytes,
                            destRowBytes,
                            rowCopyBytes);
                    }
                }

                return bitmap;
            }
            finally
            {
                if (data != nint.Zero)
                {
                    CoreGraphics.CFRelease(data);
                }
            }
        }
        finally
        {
            CoreGraphics.CGImageRelease(imageRef);
        }
    }

    private void RefreshCurrentScreenBounds()
    {
        var bounds = CoreGraphics.CGDisplayBounds(_displays[SelectedScreen]);

        _logger.LogInformation("Setting new screen bounds: {width},{height},{x},{y}.",
            bounds.Size.Width, bounds.Size.Height, bounds.Origin.X, bounds.Origin.Y);

        CurrentScreenBounds = new Rectangle(
            (int)bounds.Origin.X,
            (int)bounds.Origin.Y,
            (int)bounds.Size.Width,
            (int)bounds.Size.Height);
        CaptureFullscreen = true;
        ScreenChanged?.Invoke(this, CurrentScreenBounds);
    }
}
