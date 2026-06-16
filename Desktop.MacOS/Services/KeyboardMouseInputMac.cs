using Pronetsys.Desktop.Shared.Abstractions;
using Pronetsys.Desktop.Shared.Enums;
using Pronetsys.Desktop.Shared.Services;
using Microsoft.Extensions.Logging;
using Pronetsys.Desktop.Native.MacOS;

namespace Pronetsys.Desktop.MacOS.Services;

/// <summary>
/// Synthetic keyboard/mouse input for macOS using CoreGraphics CGEvent APIs.
/// Mirrors <c>KeyboardMouseInputLinux</c>.
///
/// Caveats (to validate on real hardware):
///  - Requires the "Accessibility" privacy permission, or events are silently dropped.
///  - Printable characters are typed via CGEventKeyboardSetUnicodeString (layout
///    independent); named/special keys use macOS virtual key codes. Holding a
///    printable key for auto-repeat is not modeled (down types the char, up is a no-op).
///  - Input blocking (ToggleBlockInput) is not available without special entitlements.
/// </summary>
public class KeyboardMouseInputMac : IKeyboardMouseInput
{
    private readonly ILogger<KeyboardMouseInputMac> _logger;

    public KeyboardMouseInputMac(ILogger<KeyboardMouseInputMac> logger)
    {
        _logger = logger;
    }

    public void Init()
    {
        // Nothing to do here.  The Windows implementation needs a processing
        // queue to keep all input simulation on the same thread.  macOS doesn't.
    }

    public void SendKeyDown(string key)
    {
        try
        {
            if (TryGetVirtualKey(key, out var virtualKey))
            {
                PostKeyEvent(virtualKey, true);
            }
            else if (key.Length >= 1)
            {
                // Printable character: type it (down + up) layout-independently.
                PostUnicode(key);
            }
            else
            {
                _logger.LogError("Key not mapped: {key}", key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending key down.");
        }
    }

    public void SendKeyUp(string key)
    {
        try
        {
            if (TryGetVirtualKey(key, out var virtualKey))
            {
                PostKeyEvent(virtualKey, false);
            }
            // Printable characters were already produced on key-down.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending key up.");
        }
    }

    public void SendMouseMove(double percentX, double percentY, IViewer viewer)
    {
        try
        {
            var point = GetAbsolutePoint(percentX, percentY, viewer);
            var ev = CoreGraphics.CGEventCreateMouseEvent(nint.Zero, CoreGraphics.MouseMoved, point, CoreGraphics.MouseButtonLeft);
            PostAndRelease(ev);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending mouse move.");
        }
    }

    public void SendMouseButtonAction(int button, ButtonAction buttonAction, double percentX, double percentY, IViewer viewer)
    {
        try
        {
            var isPressed = buttonAction == ButtonAction.Down;
            var point = GetAbsolutePoint(percentX, percentY, viewer);

            // Browser buttons: 0 = left, 1 = middle, 2 = right.
            var (eventType, cgButton) = button switch
            {
                0 => (isPressed ? CoreGraphics.LeftMouseDown : CoreGraphics.LeftMouseUp, CoreGraphics.MouseButtonLeft),
                1 => (isPressed ? CoreGraphics.OtherMouseDown : CoreGraphics.OtherMouseUp, CoreGraphics.MouseButtonCenter),
                2 => (isPressed ? CoreGraphics.RightMouseDown : CoreGraphics.RightMouseUp, CoreGraphics.MouseButtonRight),
                _ => (isPressed ? CoreGraphics.LeftMouseDown : CoreGraphics.LeftMouseUp, CoreGraphics.MouseButtonLeft),
            };

            var ev = CoreGraphics.CGEventCreateMouseEvent(nint.Zero, eventType, point, cgButton);
            PostAndRelease(ev);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending mouse button action.");
        }
    }

    public void SendMouseWheel(int deltaY)
    {
        try
        {
            // Browser deltaY is positive when scrolling down; macOS scroll is
            // positive when scrolling up, so invert.
            var lines = deltaY > 0 ? -1 : 1;
            var ev = CoreGraphics.CGEventCreateScrollWheelEvent(nint.Zero, CoreGraphics.ScrollUnitLine, 1, lines);
            PostAndRelease(ev);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending mouse wheel.");
        }
    }

    public void SendText(string transferText)
    {
        foreach (var character in transferText)
        {
            PostUnicode(character.ToString());
        }
    }

    public void SetKeyStatesUp()
    {
        // Not implemented.
    }

    public void ToggleBlockInput(bool toggleOn)
    {
        // Not implemented.  macOS has no public equivalent of Win32 BlockInput.
    }

    private static CoreGraphics.CGPoint GetAbsolutePoint(double percentX, double percentY, IViewer viewer)
    {
        var bounds = viewer.Capturer.CurrentScreenBounds;
        return new CoreGraphics.CGPoint(
            bounds.X + bounds.Width * percentX,
            bounds.Y + bounds.Height * percentY);
    }

    private void PostKeyEvent(ushort virtualKey, bool keyDown)
    {
        var ev = CoreGraphics.CGEventCreateKeyboardEvent(nint.Zero, virtualKey, keyDown);
        PostAndRelease(ev);
    }

    private void PostUnicode(string text)
    {
        // Keycode 0 with an explicit unicode string types the character regardless
        // of the active keyboard layout.
        var down = CoreGraphics.CGEventCreateKeyboardEvent(nint.Zero, 0, true);
        if (down != nint.Zero)
        {
            CoreGraphics.CGEventKeyboardSetUnicodeString(down, text.Length, text);
            PostAndRelease(down);
        }

        var up = CoreGraphics.CGEventCreateKeyboardEvent(nint.Zero, 0, false);
        if (up != nint.Zero)
        {
            CoreGraphics.CGEventKeyboardSetUnicodeString(up, text.Length, text);
            PostAndRelease(up);
        }
    }

    private void PostAndRelease(nint @event)
    {
        if (@event == nint.Zero)
        {
            return;
        }
        CoreGraphics.CGEventPost(CoreGraphics.HidEventTap, @event);
        CoreGraphics.CFRelease(@event);
    }

    /// <summary>
    /// Maps JavaScript key names to macOS virtual key codes (Carbon kVK_* values)
    /// for keys that must be sent as real key presses (modifiers, navigation, etc.).
    /// Returns false for printable characters, which are handled via unicode strings.
    /// </summary>
    private static bool TryGetVirtualKey(string key, out ushort virtualKey)
    {
        virtualKey = key switch
        {
            "Enter" => 0x24,
            "Tab" => 0x30,
            " " => 0x31,
            "Backspace" => 0x33,
            "Esc" or "Escape" => 0x35,
            "Delete" => 0x75,
            "ArrowLeft" => 0x7B,
            "ArrowRight" => 0x7C,
            "ArrowDown" => 0x7D,
            "ArrowUp" => 0x7E,
            "Home" => 0x73,
            "End" => 0x77,
            "PageUp" => 0x74,
            "PageDown" => 0x79,
            "Meta" => 0x37, // Command
            "Shift" => 0x38,
            "CapsLock" => 0x39,
            "Alt" => 0x3A, // Option
            "Control" => 0x3B,
            "F1" => 0x7A,
            "F2" => 0x78,
            "F3" => 0x63,
            "F4" => 0x76,
            "F5" => 0x60,
            "F6" => 0x61,
            "F7" => 0x62,
            "F8" => 0x64,
            "F9" => 0x65,
            "F10" => 0x6D,
            "F11" => 0x67,
            "F12" => 0x6F,
            _ => ushort.MaxValue,
        };
        return virtualKey != ushort.MaxValue;
    }
}
