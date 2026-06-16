using Pronetsys.Desktop.Shared.Abstractions;
using Pronetsys.Shared.Models;
using System.Drawing;

namespace Pronetsys.Desktop.MacOS.Services;

public class CursorIconWatcherMac : ICursorIconWatcher
{
#pragma warning disable CS0067
    public event EventHandler<CursorInfo>? OnChange;
#pragma warning restore


    public CursorInfo GetCurrentCursor() => new(Array.Empty<byte>(), Point.Empty, "default");
}
