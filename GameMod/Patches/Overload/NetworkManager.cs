using System.Collections.Generic;
using System.Linq;
using GameMod.Metadata;
using GameMod.Objects;
using HarmonyLib;
using Overload;

namespace GameMod.Patches.Overload {
    /// <summary>
    /// Sends map-specific tweaks to the client.
    /// </summary>
    /// <remarks>
    /// This should only be used for map-specific tweaks.  Use client capabilities in NetworkMatch.OnAcceptedToLobby for general tweaks.
    /// </remarks>
    [Mod(Mods.Tweaks)]
    [HarmonyPatch(typeof(NetworkManager), "LoadScene")]
    public static class NetworkManager_LoadScene {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        public static void Postfix() {
            RobotManager.ReadMultiplayerModeFile();

            var tweaks = new Dictionary<string, string>() { };

            if (!MPCustomModeFile.PickupCheck)
                tweaks.Add("item.pickupcheck", bool.FalseString);

            if (tweaks.Any()) {
                Settings.Set(tweaks);
                Settings.Send();
            }
        }
    }
}
