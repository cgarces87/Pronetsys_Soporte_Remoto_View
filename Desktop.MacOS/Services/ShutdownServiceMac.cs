using Pronetsys.Desktop.Shared.Abstractions;
using Pronetsys.Desktop.Shared.Services;
using Microsoft.Extensions.Logging;
using Pronetsys.Desktop.UI.Services;

namespace Pronetsys.Desktop.MacOS.Services;

public class ShutdownServiceMac : IShutdownService
{
    private readonly IDesktopHubConnection _hubConnection;
    private readonly IUiDispatcher _dispatcher;
    private readonly IAppState _appState;
    private readonly ILogger<ShutdownServiceMac> _logger;

    public ShutdownServiceMac(
        IDesktopHubConnection hubConnection,
        IUiDispatcher dispatcher,
        IAppState appState,
        ILogger<ShutdownServiceMac> logger)
    {
        _hubConnection = hubConnection;
        _dispatcher = dispatcher;
        _appState = appState;
        _logger = logger;
    }

    public async Task Shutdown()
    {
        _logger.LogDebug("Exiting process ID {processId}.", Environment.ProcessId);
        await TryDisconnectViewers();
        _dispatcher.Shutdown();
    }

    private async Task TryDisconnectViewers()
    {
        try
        {
            if (_hubConnection.IsConnected && _appState.Viewers.Any())
            {
                await _hubConnection.DisconnectAllViewers();
                await _hubConnection.Disconnect();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending shutdown notice to viewers.");
        }
    }
}
