using Pronetsys.Shared.Primitives;
using SkiaSharp;
using System.Drawing;

namespace Pronetsys.Desktop.Shared.Abstractions;

public interface IScreenCapturer : IDisposable
{
    event EventHandler<Rectangle> ScreenChanged;

    bool CaptureFullscreen { get; set; }
    Rectangle CurrentScreenBounds { get; }
    bool IsGpuAccelerated { get; }
    string SelectedScreen { get; }
    IEnumerable<string> GetDisplayNames();
    SKRect GetFrameDiffArea();

    Result<SKBitmap> GetImageDiff();

    Result<SKBitmap> GetNextFrame();

    int GetScreenCount();

    Rectangle GetVirtualScreenBounds();

    void Init();

    void SetSelectedScreen(string displayName);
}
