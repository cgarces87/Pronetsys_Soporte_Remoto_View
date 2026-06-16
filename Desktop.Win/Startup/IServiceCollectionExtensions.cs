using Pronetsys.Desktop.Shared.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Pronetsys.Desktop.Shared.Startup;
using Pronetsys.Desktop.UI.Startup;
using System.Runtime.Versioning;
using Pronetsys.Desktop.Win.Services;

namespace Pronetsys.Desktop.Win.Startup;

public static class IServiceCollectionExtensions
{
    /// <summary>
    /// Adds Windows and cross-platform remote control services to the service collection.
    /// All methods on <see cref="IRemoteControlClientBuilder"/> must be called to register
    /// required services.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="clientConfig"></param>
    [SupportedOSPlatform("windows")]
    public static void AddRemoteControlWindows(this IServiceCollection services)
    {
        services.AddRemoteControlXplat();
        services.AddRemoteControlUi();

        services.AddSingleton<ICursorIconWatcher, CursorIconWatcherWin>();
        services.AddSingleton<IKeyboardMouseInput, KeyboardMouseInputWin>();
        services.AddSingleton<IAudioCapturer, AudioCapturerWin>();
        services.AddSingleton<IShutdownService, ShutdownServiceWin>();
        services.AddSingleton<IMessageLoop, MessageLoop>();
        services.AddSingleton<IAppStartup, AppStartup>();
        services.AddTransient<IFileTransferService, FileTransferServiceWin>();
        services.AddTransient<IScreenCapturer, ScreenCapturerWin>();
    }
}
