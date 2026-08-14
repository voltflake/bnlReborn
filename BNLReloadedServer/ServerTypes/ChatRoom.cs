using BNLReloadedServer.BaseTypes;
using BNLReloadedServer.Servers;
using BNLReloadedServer.Service;

namespace BNLReloadedServer.ServerTypes;

public class ChatRoom(RoomId roomId, ISender sender)
{
    private readonly ServiceChat _chatService = new(sender);
    public readonly RoomId RoomId = roomId;

    // The notify service is optional: a session that has already gone away still has to be
    // unsubscribed, but there is nobody left to tell about it.
    public void AddToRoom(Guid sessionId, IServiceChat? notifyPlayer)
    {
        sender.Subscribe(sessionId);
        notifyPlayer?.SendRoomAdd(RoomId);
    }

    public void RemoveFromRoom(Guid sessionId, IServiceChat? notifyPlayer)
    {
        sender.Unsubscribe(sessionId);
        notifyPlayer?.SendRoomRemove(RoomId);
    }

    // Re-announces the room to a session that may already have it. The client keys its rooms in a
    // dictionary it adds to blindly, so a room it still remembers has to be dropped first.
    public void ResendRoom(Guid sessionId, IServiceChat? notifyPlayer)
    {
        sender.Subscribe(sessionId);
        notifyPlayer?.SendRoomRemove(RoomId);
        notifyPlayer?.SendRoomAdd(RoomId);
    }

    public void SendMessage(ChatPlayer player, string message, List<Guid>? excluded = null) =>
        _chatService.SendRoomMessage(RoomId, player, message, excluded);
    
    public void SendServiceMessage(string message, bool isLocalized, Dictionary<string, string> args) => 
        _chatService.SendServiceMessage(RoomId, message, isLocalized, args);
    
    public void SendServiceMessage(string message, bool isLocalized = false) => 
        _chatService.SendServiceMessage(RoomId, message, isLocalized, new Dictionary<string, string>());

    public void ClearRoom()
    {
        sender.UnsubscribeAll();
    }
}