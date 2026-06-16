using SkiaSharp;

namespace Pronetsys.Desktop.Shared.Extensions;

public static class SKBitmapExtensions
{
    public static SKRect ToRectangle(this SKBitmap bitmap)
    {
        return new SKRect(0, 0, bitmap.Width, bitmap.Height);
    }
}
