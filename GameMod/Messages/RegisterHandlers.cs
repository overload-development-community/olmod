using GameMod.Metadata;
using Overload;
using UnityEngine.Networking;

namespace GameMod.Messages {
    /// <summary>
    /// Functions to register all handlers for olmod.
    /// </summary>
    [Mod(Mods.MessageHandlers)]

    public class RegisterHandlers {
        public static void RegisterClientHandlers() {
            if (Client.GetClient() == null)
                return;

            Client.GetClient().RegisterHandler(MessageTypes.MsgMPTweaksSet, TweaksMessage.ClientHandler);
        }

        public static void RegisterServerHandlers() {
            NetworkServer.RegisterHandler(MessageTypes.MsgClientCapabilities, TweaksMessage.ServerHandler);
        }
    }
}
