using Pronetsys.Server.Enums;

namespace Pronetsys.Server.Models.Messages;

public record DeviceCardStateChangedMessage(string DeviceId, DeviceCardState State);