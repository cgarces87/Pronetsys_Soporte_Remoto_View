using Pronetsys.Shared.Models;

namespace Pronetsys.Desktop.Shared.Abstractions;

public interface IChatUiService
{
    event EventHandler ChatWindowClosed;

    void ShowChatWindow(string organizationName, StreamWriter writer);
    Task ReceiveChat(ChatMessage chatMessage);
}
