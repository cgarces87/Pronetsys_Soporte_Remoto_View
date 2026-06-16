using Microsoft.AspNetCore.Components;
using Pronetsys.Server.Hubs;
using Pronetsys.Server.Models.Messages;
using Pronetsys.Server.Services.Stores;
using Pronetsys.Shared.Enums;
using Pronetsys.Shared.ViewModels;

namespace Pronetsys.Server.Components.Devices;

public partial class ChatFrame : AuthComponentBase
{
    private ICollection<ChatSession> _chatSessions = Array.Empty<ChatSession>();

    [Inject]
    private ISelectedCardsStore CardStore { get; init; } = null!;

    [Inject]
    private IChatSessionStore ChatCache { get; init; } = null!;

    [Inject]
    private ICircuitConnection CircuitConnection { get; init; } = null!;

    protected override async Task OnInitializedAsync()
    {
        _chatSessions = ChatCache.GetAllSessions();
        await Register<ChatSessionsChangedMessage, string>(
            CircuitConnection.ConnectionId,
            HandleChatSessionsChanged);
        await Register<ChatReceivedMessage, string>(
            CircuitConnection.ConnectionId,
            HandleChatMessageReceived);

        await base.OnInitializedAsync();
    }

    private async Task HandleChatMessageReceived(object subscriber, ChatReceivedMessage message)
    {
        if (message.DidDisconnect ||
            ChatCache.ContainsKey(message.DeviceId))
        {
            return;
        }

        var newChat = new ChatSession()
        {
            DeviceId = message.DeviceId,
            DeviceName = message.DeviceName,
            IsExpanded = true
        };

        newChat.ChatHistory.Enqueue(new ChatHistoryItem()
        {
            Message = message.MessageText,
            Origin = ChatHistoryItemOrigin.Device
        });

        ChatCache.AddOrUpdate(message.DeviceId, newChat, (k, v) => newChat);

        await Messenger.Send(new ChatSessionsChangedMessage(), CircuitConnection.ConnectionId);
    }

    private async Task HandleChatSessionsChanged(object subscriber, ChatSessionsChangedMessage message)
    {
        _chatSessions = ChatCache.GetAllSessions();
        await InvokeAsync(StateHasChanged);
    }
}
