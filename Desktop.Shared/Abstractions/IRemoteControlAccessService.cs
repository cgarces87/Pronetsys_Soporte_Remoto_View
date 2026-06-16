using Pronetsys.Shared.Enums;

namespace Pronetsys.Desktop.Shared.Abstractions;

public interface IRemoteControlAccessService
{
    bool IsPromptOpen { get; }

    Task<PromptForAccessResult> PromptForAccess(string requesterName, string organizationName);
}
