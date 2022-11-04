using GameMod.Metadata;
using Overload;
using UnityEngine.Networking;

namespace GameMod.Messages {
    /// <summary>
    /// Functions to register all handlers for olmod.
    /// </summary>
    [Mod(Mods.MessageHandlers)]

    public static class RegisterHandlers {
        public static void RegisterClientHandlers() {
            if (Client.GetClient() == null)
                return;

            Client.GetClient().RegisterHandler(MessageTypes.MsgMPTweaksSet, TweaksMessage.ClientHandler);
            Client.GetClient().RegisterHandler(MessageTypes.MsgChangeTeam, ChangeTeamMessage.ClientHandler);
            Client.GetClient().RegisterHandler(MessageTypes.MsgPlayerWeaponSynchronization, PlayerWeaponSynchronizationMessage.ClientHandler);
            Client.GetClient().RegisterHandler(MessageTypes.MsgPlayerAddResource, PlayerAddResourceMessage.ClientHandler);
            Client.GetClient().RegisterHandler(MessageTypes.MsgPlayerSyncResource, PlayerSyncResourceMessage.ClientHandler);
            Client.GetClient().RegisterHandler(MessageTypes.MsgPlayerSyncAllMissiles, PlayerSyncAllMissilesMessage.ClientHandler);
            Client.GetClient().RegisterHandler(MessageTypes.MsgDetonate, DetonateMessage.ClientHandler);
        }

        public static void RegisterServerHandlers() {
            NetworkServer.RegisterHandler(MessageTypes.MsgClientCapabilities, TweaksMessage.ServerHandler);
            NetworkServer.RegisterHandler(MessageTypes.MsgChangeTeam, ChangeTeamMessage.ServerHandler);
            NetworkServer.RegisterHandler(MessageTypes.MsgSniperPacket, SniperPacketMessage.ServerHandler);
            NetworkServer.RegisterHandler(MessageTypes.MsgPlayerWeaponSynchronization, PlayerWeaponSynchronizationMessage.ServerHandler);
            NetworkServer.RegisterHandler(MessageTypes.MsgPlayerSyncResource, PlayerSyncResourceMessage.ServerHandler);
            NetworkServer.RegisterHandler(MessageTypes.MsgPlayerSyncAllMissiles, PlayerSyncAllMissilesMessage.ServerHandler);
            NetworkServer.RegisterHandler(MessageTypes.MsgDetonate, DetonateMessage.ServerHandler);
        }
    }
}
