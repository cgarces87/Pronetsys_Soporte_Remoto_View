using Pronetsys.Shared.Enums;
using Pronetsys.Shared.Models;

namespace Pronetsys.Server.Models.Messages;

public record PowerShellCompletionsMessage(PwshCommandCompletion Completion, CompletionIntent Intent);