using Pronetsys.Server.Services;
using Bitbound.SimpleMessenger;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Pronetsys.Server.Models.Messages;
using Pronetsys.Shared;
using Pronetsys.Shared.Dtos;
using Pronetsys.Shared.Entities;
using Pronetsys.Shared.Enums;
using Pronetsys.Shared.Interfaces;
using Pronetsys.Shared.Models;
using Pronetsys.Shared.Utilities;
using System.Security.Cryptography;
using System.Text;

namespace Pronetsys.Server.Hubs;

public class AgentHub : Hub<IAgentHubClient>
{
    private readonly IDataService _dataService;
    private readonly ICircuitManager _circuitManager;
    private readonly IExpiringTokenService _expiringTokenService;
    private readonly ILogger<AgentHub> _logger;
    private readonly IMessenger _messenger;
    private readonly IRemoteControlSessionCache _remoteControlSessions;
    private readonly IAgentHubSessionCache _serviceSessionCache;
    private readonly IHubContext<ViewerHub> _viewerHubContext;

    public AgentHub(
        IDataService dataService,
        IAgentHubSessionCache serviceSessionCache,
        IHubContext<ViewerHub> viewerHubContext,
        ICircuitManager circuitManager,
        IExpiringTokenService expiringTokenService,
        IRemoteControlSessionCache remoteControlSessionCache,
        IMessenger messenger,
        ILogger<AgentHub> logger)
    {
        _dataService = dataService;
        _serviceSessionCache = serviceSessionCache;
        _viewerHubContext = viewerHubContext;
        _circuitManager = circuitManager;
        _expiringTokenService = expiringTokenService;
        _remoteControlSessions = remoteControlSessionCache;
        _messenger = messenger;
        _logger = logger;
    }

    // TODO: Replace with new invoke capability in .NET 7 in ScriptingController.
    public static IMemoryCache ApiScriptResults { get; } = new MemoryCache(new MemoryCacheOptions());

    private Device? Device
    {
        get
        {
            if (Context.Items["Device"] is Device device)
            {
                return device;
            }
            _logger.LogWarning("Device has not been set in the context items.");
            return null;
        }
        set
        {
            Context.Items["Device"] = value;
        }
    }

    public async Task Chat(string messageText, bool disconnected, string browserConnectionId)
    {
        if (Device is null)
        {
            return;
        }

        if (_circuitManager.TryGetConnection(browserConnectionId, out _))
        {
            var message = new ChatReceivedMessage(Device.ID, $"{Device.DeviceName}", messageText, disconnected);
            await _messenger.Send(message, browserConnectionId);
        }
        else
        {
            await Clients.Caller.SendChatMessage(
                senderName: string.Empty,
                message: string.Empty,
                orgName: string.Empty,
                orgId: string.Empty,
                disconnected: true,
                senderConnectionId: browserConnectionId);
        }
    }

    public async Task CheckForPendingRemoteControlSessions()
    {
        try
        {
            if (Device is null)
            {
                return;
            }

            _logger.LogDebug(
                "Checking for pending remote control sessions for device {deviceId}.",
                Device.ID);

            var waitingSessions = _remoteControlSessions
                .Sessions
                .Where(x => x.DeviceId == Device.ID);

            foreach (var session in waitingSessions)
            {
                _logger.LogDebug(
                    "Restarting remote control session {sessionId}.",
                    session.UnattendedSessionId);

                session.AgentConnectionId = Context.ConnectionId;
                await Clients.Caller.RestartScreenCaster(
                    session.ViewerList.ToArray(),
                    $"{session.UnattendedSessionId}",
                    session.AccessKey,
                    session.UserConnectionId,
                    session.RequesterName,
                    session.OrganizationName,
                    session.OrganizationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking for pending remote control sessions.");
        }
    }

    public async Task CheckForPendingScriptRuns()
    {
        if (Device is null)
        {
            return;
        }

        var authToken = _expiringTokenService.GetToken(Time.Now.AddMinutes(AppConstants.ScriptRunExpirationMinutes), Device.OrganizationID);
        var scriptRuns = await _dataService.GetPendingScriptRuns(Device.ID);

        foreach (var run in scriptRuns)
        {
            if (run.SavedScriptId is null)
            {
                continue;
            }
            await Clients.Caller.RunScript(
                run.SavedScriptId.Value,
                run.Id,
                run.Initiator ?? "Unknown Initiator",
                run.InputType,
                authToken);
        }
    }

    public async Task<bool> DeviceCameOnline(DeviceClientDto device)
    {
        try
        {
            if (await CheckForDeviceBan(device.ID, device.DeviceName))
            {
                return false;
            }

            var ip = Context.GetHttpContext()?.Connection?.RemoteIpAddress;
            if (ip != null && ip.IsIPv4MappedToIPv6)
            {
                ip = ip.MapToIPv4();
            }
            device.PublicIP = $"{ip}";

            if (await CheckForDeviceBan(device.PublicIP))
            {
                return false;
            }

            // Authenticate returning agents: once a device has a server verification
            // token, the connecting agent must present a matching one. New devices
            // (no token yet) enroll on first connect (trust-on-first-use).
            var existingDeviceResult = await _dataService.GetDevice(device.ID);
            if (existingDeviceResult.IsSuccess &&
                !string.IsNullOrWhiteSpace(existingDeviceResult.Value.ServerVerificationToken))
            {
                var presented = Encoding.UTF8.GetBytes(device.ServerVerificationToken ?? string.Empty);
                var stored = Encoding.UTF8.GetBytes(existingDeviceResult.Value.ServerVerificationToken!);
                if (!CryptographicOperations.FixedTimeEquals(presented, stored))
                {
                    _logger.LogWarning(
                        "Device {deviceId} failed the server verification token check from {ip}.  Aborting connection.",
                        device.ID,
                        device.PublicIP);
                    Context.Abort();
                    return false;
                }
            }

            var result = await _dataService.AddOrUpdateDevice(device);
            if (!result.IsSuccess)
            {
                // Organization wasn't found.
                return false;
            }

            Device = result.Value;

            _serviceSessionCache.AddOrUpdateByConnectionId(Context.ConnectionId, Device);

            var userIDs = _circuitManager.Connections.Select(x => x.User.Id);

            var filteredUserIDs = _dataService.FilterUsersByDevicePermission(userIDs, Device.ID);

            var connections = _circuitManager.Connections
                .Where(x => x.User.OrganizationID == Device.OrganizationID &&
                    filteredUserIDs.Contains(x.User.Id));

            foreach (var connection in connections)
            {
                var message = new DeviceStateChangedMessage(Device);
                await _messenger.Send(message, connection.ConnectionId);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while setting device to online status.");
        }

        Context.Abort();
        return false;
    }

    public async Task DeviceHeartbeat(DeviceClientDto device)
    {
        if (await CheckForDeviceBan(device.ID, device.DeviceName))
        {
            return;
        }

        var ip = Context.GetHttpContext()?.Connection?.RemoteIpAddress;
        if (ip != null && ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }
        device.PublicIP = $"{ip}";

        if (await CheckForDeviceBan(device.PublicIP))
        {
            return;
        }


        var result = await _dataService.AddOrUpdateDevice(device);

        if (!result.IsSuccess)
        {
            return;
        }

        Device = result.Value;

        _serviceSessionCache.AddOrUpdateByConnectionId(Context.ConnectionId, Device);

        var userIDs = _circuitManager.Connections.Select(x => x.User.Id);

        var filteredUserIDs = _dataService.FilterUsersByDevicePermission(userIDs, Device.ID);

        var connections = _circuitManager.Connections
            .Where(x => x.User.OrganizationID == Device.OrganizationID &&
                filteredUserIDs.Contains(x.User.Id));

        foreach (var connection in connections)
        {
            var message = new DeviceStateChangedMessage(Device);
            await _messenger.Send(message, connection.ConnectionId);
        }


        await CheckForPendingScriptRuns();
    }

    public Task DisplayMessage(string consoleMessage, string popupMessage, string className, string requesterId)
    {
        var message = new DisplayNotificationMessage(consoleMessage, popupMessage, className);
        return _messenger.Send(message, requesterId);
    }

    public Task DownloadFile(string fileID, string requesterId)
    {
        var message = new DownloadFileMessage(fileID);
        return _messenger.Send(message, requesterId);
    }

    public Task DownloadFileProgress(int progressPercent, string requesterId)
    {
        var message = new DownloadFileProgressMessage(progressPercent);
        return _messenger.Send(message, requesterId);
    }

    public async Task<string> GetServerUrl()
    {
        var settings = await _dataService.GetSettings();
        return settings.ServerUrl;
    }

    public string GetServerVerificationToken()
    {
        return $"{Device?.ServerVerificationToken}";
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            if (Device != null)
            {
                _dataService.DeviceDisconnected(Device.ID);

                Device.IsOnline = false;

                var userIDs = _circuitManager.Connections.Select(x => x.User.Id);

                var filteredUserIDs = _dataService.FilterUsersByDevicePermission(userIDs, Device.ID);

                var connections = _circuitManager.Connections
                    .Where(x => x.User.OrganizationID == Device.OrganizationID &&
                        filteredUserIDs.Contains(x.User.Id));

                foreach (var connection in connections)
                {
                    var message = new DeviceStateChangedMessage(Device);
                    await _messenger.Send(message, connection.ConnectionId);
                }
            }
            await base.OnDisconnectedAsync(exception);
        }
        finally
        {
            _serviceSessionCache.TryRemoveByConnectionId(Context.ConnectionId, out _);
        }
    }

    public Task ReturnPowerShellCompletions(PwshCommandCompletion completion, CompletionIntent intent, string senderConnectionId)
    {
        var message = new PowerShellCompletionsMessage(completion, intent);
        return _messenger.Send(message, senderConnectionId);
    }

    public async Task ScriptResult(string scriptResultId)
    {
        var result = await _dataService.GetScriptResult(scriptResultId);
        if (!result.IsSuccess)
        {
            return;
        }

        var message = new ScriptResultMessage(result.Value);
        await _messenger.Send(message, $"{result.Value.SenderConnectionID}");
    }

    public void ScriptResultViaApi(string commandID, string requestID)
    {
        ApiScriptResults.Set(requestID, commandID, DateTimeOffset.Now.AddHours(1));
    }

    public Task SendConnectionFailedToViewers(List<string> viewerIDs)
    {
        return _viewerHubContext.Clients.Clients(viewerIDs).SendAsync("ConnectionFailed");
    }

    public Task SendLogs(string logChunk, string requesterConnectionId)
    {
        var message = new ReceiveLogsMessage(logChunk);
        return _messenger.Send(message, requesterConnectionId);
    }

    public void SetServerVerificationToken(string verificationToken)
    {
        if (Device is null)
        {
            return;
        }
        // Only allow setting the token during initial enrollment. Once a device
        // has a verification token it must not be overwritable by a (re)connecting
        // client, otherwise anyone who can claim a device ID (DeviceCameOnline is
        // not authenticated) could reset/hijack its verification token.
        if (!string.IsNullOrWhiteSpace(Device.ServerVerificationToken))
        {
            return;
        }
        Device.ServerVerificationToken = verificationToken;
        _dataService.SetServerVerificationToken(Device.ID, verificationToken);
    }

    public Task TransferCompleted(string transferId, string requesterId)
    {
        var message = new TransferCompleteMessage(transferId);
        return _messenger.Send(message, requesterId);
    }

    private async Task<bool> CheckForDeviceBan(params string[] deviceIdNameOrIPs)
    {
        var settings = await _dataService.GetSettings();
        foreach (var device in deviceIdNameOrIPs)
        {
            if (string.IsNullOrWhiteSpace(device))
            {
                continue;
            }

            if (settings.BannedDevices.Any(x => !string.IsNullOrWhiteSpace(x) &&
                x.Equals(device, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning("Device ID/name/IP ({device}) is banned.  Sending uninstall command.", device);

                await Clients.Caller.UninstallAgent();
                return true;
            }
        }

        return false;
    }
}
