using GameMod.Messages;
using GameMod.Metadata;
using HarmonyLib;
using Overload;

namespace GameMod.Patches.Overload {
    /// <summary>
    /// Registers all of olmod's client handlers.
    /// </summary>
    [Mod(Mods.MessageHandlers)]
    [HarmonyPatch(typeof(Client), "RegisterHandlers")]
    public class Client_RegisterHandlers {
        public static void Postfix() {
            RegisterHandlers.RegisterClientHandlers();
        }
    }
}
