using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Remotely.Agent.Interfaces;
using Remotely.Shared.Models;
using Remotely.Shared.Services;
using Remotely.Shared.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Remotely.Agent.Services.MacOS;

/// <summary>
/// Launches the Remotely desktop (screen-control) client on macOS.
/// Mirrors <see cref="Linux.AppLauncherLinux"/>.
///
/// Caveats (to validate on real hardware):
///  - When the agent runs as a root LaunchDaemon, the desktop client must be
///    started inside the logged-in user's Aqua (GUI) session; this uses
///    `launchctl asuser &lt;uid&gt;` to do so. Running as the console user directly
///    would not need this.
///  - The desktop client needs the "Screen Recording" and "Accessibility"
///    privacy permissions granted before it can capture/inject.
/// </summary>
public class AppLauncherMac : IAppLauncher
{
    private readonly ConnectionInfo _connectionInfo;
    private readonly ILogger<AppLauncherMac> _logger;
    private readonly IProcessInvoker _processInvoker;

    private readonly string _rcBinaryPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "Desktop",
        EnvironmentHelper.DesktopExecutableFileName);

    public AppLauncherMac(
        IConfigService configService,
        IProcessInvoker processInvoker,
        ILogger<AppLauncherMac> logger)
    {
        _processInvoker = processInvoker;
        _connectionInfo = configService.GetConnectionInfo();
        _logger = logger;
    }

    public async Task<int> LaunchChatService(string pipeName, string userConnectionId, string requesterName, string orgName, string orgId, HubConnection hubConnection)
    {
        try
        {
            if (!File.Exists(_rcBinaryPath))
            {
                await hubConnection.SendAsync("DisplayMessage",
                    "Chat executable not found on target device.",
                    "Executable not found on device.",
                    "bg-danger",
                    userConnectionId);
            }

            await hubConnection.SendAsync("DisplayMessage", "Starting chat service.", "Starting chat service.", "bg-success", userConnectionId);
            var args =
                _rcBinaryPath +
                $" --mode Chat" +
                $" --host \"{_connectionInfo.Host}\"" +
                $" --pipe-name {pipeName}" +
                $" --requester-name \"{requesterName}\"" +
                $" --org-name \"{orgName}\"" +
                $" --org-id \"{orgId}\"";
            return StartMacDesktopApp(args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while starting chat.");
            await hubConnection.SendAsync("DisplayMessage", "Chat service failed to start on target device.", "Failed to start chat service.", "bg-danger", userConnectionId);
        }
        return -1;
    }

    public async Task LaunchRemoteControl(int targetSessionId, string sessionId, string accessKey, string userConnectionId, string requesterName, string orgName, string orgId, HubConnection hubConnection)
    {
        try
        {
            if (!File.Exists(_rcBinaryPath))
            {
                await hubConnection.SendAsync("DisplayMessage",
                    "Remote control executable not found on target device.",
                    "Executable not found on device.",
                    "bg-danger",
                    userConnectionId);
                return;
            }

            await hubConnection.SendAsync("DisplayMessage", "Starting remote control.", "Starting remote control.", "bg-success", userConnectionId);
            var args =
                _rcBinaryPath +
                $" --mode Unattended" +
                $" --host {_connectionInfo.Host}" +
                $" --requester-name \"{requesterName}\"" +
                $" --org-name \"{orgName}\"" +
                $" --org-id \"{orgId}\"" +
                $" --session-id \"{sessionId}\"" +
                $" --access-key \"{accessKey}\"";
            StartMacDesktopApp(args);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while launching remote control.");
            await hubConnection.SendAsync("DisplayMessage", "Remote control failed to start on target device.", "Failed to start remote control.", "bg-danger", userConnectionId);
        }
    }

    public async Task RestartScreenCaster(string[] viewerIds, string sessionId, string accessKey, string userConnectionId, string requesterName, string orgName, string orgId, HubConnection hubConnection, int targetSessionID = -1)
    {
        try
        {
            var args =
                _rcBinaryPath +
                $" --mode Unattended" +
                $" --host {_connectionInfo.Host}" +
                $" --requester-name \"{requesterName}\"" +
                $" --org-name \"{orgName}\"" +
                $" --org-id \"{orgId}\"" +
                $" --session-id \"{sessionId}\"" +
                $" --access-key \"{accessKey}\"";
            StartMacDesktopApp(args);
        }
        catch (Exception ex)
        {
            await hubConnection.SendAsync("SendConnectionFailedToViewers", viewerIds);
            _logger.LogError(ex, "Error while restarting screen caster.");
            throw;
        }
    }

    private int StartMacDesktopApp(string args)
    {
        // Resolve the user currently owning the GUI (console) session so we can
        // launch the desktop client inside that user's Aqua session.
        var consoleUser = _processInvoker.InvokeProcessOutput("stat", "-f%Su /dev/console")?.Trim();

        ProcessStartInfo psi;
        if (!string.IsNullOrWhiteSpace(consoleUser) && consoleUser != "root")
        {
            var uid = _processInvoker.InvokeProcessOutput("id", $"-u {consoleUser}")?.Trim();
            psi = new ProcessStartInfo
            {
                FileName = "launchctl",
                Arguments = $"asuser {uid} {args}"
            };
            _logger.LogInformation(
                "Launching desktop client into GUI session for user {user} (uid {uid}). Args: {args}",
                consoleUser, uid, args);
        }
        else
        {
            // No separate GUI user (or already running as the session owner):
            // launch the binary directly.
            psi = new ProcessStartInfo
            {
                FileName = _rcBinaryPath,
                Arguments = args[_rcBinaryPath.Length..].TrimStart()
            };
            _logger.LogInformation("Launching desktop client directly. Args: {args}", args);
        }

        return Process.Start(psi)?.Id ?? throw new InvalidOperationException("Failed to launch desktop app.");
    }
}
