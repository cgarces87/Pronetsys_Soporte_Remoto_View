using Pronetsys.Shared.Enums;
using Pronetsys.Shared.Models;

namespace Pronetsys.Shared.Interfaces;

public interface IDesktopHubClient
{
    Task Disconnect(string reason);

    Task GetScreenCast(
        string viewerId,
        string requesterName,
        bool notifyUser,
        Guid streamId);

    Task<PromptForAccessResult> PromptForAccess(RemoteControlAccessRequest accessRequest);

    Task RequestScreenCast(
        string viewerId,
        string requesterName,
        bool notifyUser,
        Guid streamId);

    Task SendDtoToClient(byte[] dtoWrapper, string viewerConnectionId);

    Task ViewerDisconnected(string viewerId);
}
