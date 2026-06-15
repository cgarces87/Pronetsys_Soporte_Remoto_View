using Remotely.Desktop.Shared.Abstractions;
using Remotely.Shared.Models;
using System.Drawing;

namespace Remotely.Desktop.MacOS.Services;

public class CursorIconWatcherMac : ICursorIconWatcher
{
#pragma warning disable CS0067
    public event EventHandler<CursorInfo>? OnChange;
#pragma warning restore


    public CursorInfo GetCurrentCursor() => new(Array.Empty<byte>(), Point.Empty, "default");
}
