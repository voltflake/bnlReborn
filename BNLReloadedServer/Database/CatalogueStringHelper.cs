namespace BNLReloadedServer.Database;

public static class CatalogueStringHelper
{
    public const string OnEnterChat = "hud_chat_player_joined";
    // Spectators are absent from the clients' player caches, so the server supplies their name.
    // This is TeamColorContainer.Gui.ChatPlayerMessage from the stock client.
    public const string OnSpectatorEnterChat =
        "<color=#5BBC4DFF>{spectator}</color> joined as a spectator.";
    public const string OnSpectatorLeaveChat =
        "<color=#5BBC4DFF>{spectator}</color> stopped spectating.";
    public const string OnLeaveMatch = "result_screen_chat_player_left";
    public const string OnDisconnected = "hud_chat_player_disconnected";
    public const string OnKicked = "match_kicked_antihack_chat";
    public const string OnQuit = "match_kicked_quit_chat";
    public const string ReturnToCustomHost = "customgame_returning_to_menu";
    public const string OnInactivity = "match_kicked_inactivity_chat";
}
