using System.Runtime.InteropServices;

namespace Remotely.Desktop.Native.MacOS;

/// <summary>
/// P/Invoke bindings into the macOS CoreGraphics (Quartz) and CoreFoundation
/// frameworks used for screen capture and synthetic input.
///
/// These mirror the role of <c>LibX11</c>/<c>LibXtst</c> on Linux. Like those,
/// the <see cref="DllImport"/> entries are just metadata and compile on any OS;
/// they only resolve at runtime on macOS.
///
/// Runtime requirements on macOS:
///  - Screen capture (CGDisplayCreateImage) requires the "Screen Recording"
///    permission (System Settings → Privacy &amp; Security) for the host process.
///  - Synthetic input (CGEventPost) requires the "Accessibility" permission.
///  - CGDisplayCreateImage is deprecated as of macOS 14 in favor of
///    ScreenCaptureKit; it still works but should eventually be replaced.
/// </summary>
public static class CoreGraphics
{
    private const string CoreGraphicsFramework =
        "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string CoreFoundationFramework =
        "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    // ---------- Geometry (CGFloat is 64-bit double on modern macOS) ----------

    [StructLayout(LayoutKind.Sequential)]
    public struct CGPoint
    {
        public double X;
        public double Y;

        public CGPoint(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CGSize
    {
        public double Width;
        public double Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CGRect
    {
        public CGPoint Origin;
        public CGSize Size;
    }

    // ---------- Constants ----------

    // CGEventType
    public const uint LeftMouseDown = 1;
    public const uint LeftMouseUp = 2;
    public const uint RightMouseDown = 3;
    public const uint RightMouseUp = 4;
    public const uint MouseMoved = 5;
    public const uint ScrollWheel = 22;
    public const uint OtherMouseDown = 25;
    public const uint OtherMouseUp = 26;

    // CGMouseButton
    public const uint MouseButtonLeft = 0;
    public const uint MouseButtonRight = 1;
    public const uint MouseButtonCenter = 2;

    // CGEventTapLocation
    public const uint HidEventTap = 0;

    // CGScrollEventUnit
    public const uint ScrollUnitPixel = 0;
    public const uint ScrollUnitLine = 1;

    // ---------- Display enumeration ----------

    [DllImport(CoreGraphicsFramework)]
    public static extern uint CGMainDisplayID();

    [DllImport(CoreGraphicsFramework)]
    public static extern int CGGetActiveDisplayList(uint maxDisplays, [Out] uint[]? activeDisplays, out uint displayCount);

    [DllImport(CoreGraphicsFramework)]
    public static extern CGRect CGDisplayBounds(uint display);

    // ---------- Screen capture ----------

    [DllImport(CoreGraphicsFramework)]
    public static extern nint CGDisplayCreateImage(uint displayId);

    [DllImport(CoreGraphicsFramework)]
    public static extern nint CGImageGetWidth(nint image);

    [DllImport(CoreGraphicsFramework)]
    public static extern nint CGImageGetHeight(nint image);

    [DllImport(CoreGraphicsFramework)]
    public static extern nint CGImageGetBytesPerRow(nint image);

    [DllImport(CoreGraphicsFramework)]
    public static extern nint CGImageGetDataProvider(nint image);

    [DllImport(CoreGraphicsFramework)]
    public static extern void CGImageRelease(nint image);

    [DllImport(CoreGraphicsFramework)]
    public static extern nint CGDataProviderCopyData(nint provider);

    // ---------- CoreFoundation (CFData) ----------

    [DllImport(CoreFoundationFramework)]
    public static extern nint CFDataGetBytePtr(nint theData);

    [DllImport(CoreFoundationFramework)]
    public static extern nint CFDataGetLength(nint theData);

    [DllImport(CoreFoundationFramework)]
    public static extern void CFRelease(nint cf);

    // ---------- Synthetic input ----------

    [DllImport(CoreGraphicsFramework)]
    public static extern nint CGEventCreateMouseEvent(nint source, uint mouseType, CGPoint mouseCursorPosition, uint mouseButton);

    [DllImport(CoreGraphicsFramework)]
    public static extern nint CGEventCreateKeyboardEvent(nint source, ushort virtualKey, [MarshalAs(UnmanagedType.I1)] bool keyDown);

    [DllImport(CoreGraphicsFramework)]
    public static extern nint CGEventCreateScrollWheelEvent(nint source, uint units, uint wheelCount, int wheel1);

    [DllImport(CoreGraphicsFramework)]
    public static extern void CGEventKeyboardSetUnicodeString(nint @event, nint stringLength, [MarshalAs(UnmanagedType.LPWStr)] string unicodeString);

    [DllImport(CoreGraphicsFramework)]
    public static extern void CGEventPost(uint tapLocation, nint @event);

    [DllImport(CoreGraphicsFramework)]
    public static extern int CGWarpMouseCursorPosition(CGPoint newCursorPosition);
}
