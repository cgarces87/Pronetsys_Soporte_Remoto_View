using Pronetsys.Desktop.Shared.Abstractions;
using Pronetsys.Shared.Models;
using System.Drawing;

namespace Pronetsys.Desktop.Linux.Services;

public class CursorIconWatcherLinux : ICursorIconWatcher
{
#pragma warning disable CS0067
    public event EventHandler<CursorInfo>? OnChange;
#pragma warning restore


    public CursorInfo GetCurrentCursor() => new(Array.Empty<byte>(), Point.Empty, "default");
}
