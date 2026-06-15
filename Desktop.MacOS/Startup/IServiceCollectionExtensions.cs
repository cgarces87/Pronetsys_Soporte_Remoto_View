using Remotely.Desktop.Shared.Abstractions;
using Remotely.Desktop.Shared.Startup;
using Microsoft.Extensions.DependencyInjection;
using Remotely.Desktop.MacOS.Services;
using Remotely.Desktop.UI.Services;
using Remotely.Desktop.UI.Startup;

namespace Remotely.Desktop.MacOS.Startup;

public static class IServiceCollectionExtensions
{
    /// <summary>
    /// Adds macOS and cross-platform remote control services to the service collection.
    /// Mirrors <c>AddRemoteControlLinux</c>; shared UI services (clipboard, session
    /// indicator) are reused from Desktop.UI, while capture/input are macOS-specific.
    /// </summary>
    public static void AddRemoteControlMacOS(this IServiceCollection services)
    {
        services.AddRemoteControlXplat();
        services.AddRemoteControlUi();

        services.AddSingleton<IAppStartup, AppStartup>();
        services.AddSingleton<ICursorIconWatcher, CursorIconWatcherMac>();
        services.AddSingleton<IKeyboardMouseInput, KeyboardMouseInputMac>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IAudioCapturer, AudioCapturerMac>();
        services.AddTransient<IScreenCapturer, ScreenCapturerMac>();
        services.AddScoped<IFileTransferService, FileTransferServiceMac>();
        services.AddSingleton<ISessionIndicator, SessionIndicator>();
        services.AddSingleton<IShutdownService, ShutdownServiceMac>();
    }
}
