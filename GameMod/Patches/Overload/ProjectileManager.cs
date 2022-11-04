using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GameMod.Messages;
using GameMod.Metadata;
using GameMod.Objects;
using HarmonyLib;
using Overload;
using UnityEngine.Networking;

namespace GameMod.Patches.Overload {
    /// <summary>
    /// This tells the server to explode any devastators in flight.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    [HarmonyPatch(typeof(ProjectileManager), "ExplodePlayerDetonators")]
    public static class ProjectileManager_ExplodePlayerDetonators {
        public static bool Prefix(Player p) {
            if (!SniperPackets.enabled) return true;
            if (!GameplayManager.IsMultiplayerActive) return true;
            if (NetworkServer.active && !Tweaks.ClientHasMod(p.connectionToClient.connectionId)) return true;

            if (NetworkServer.active && !SniperPackets.serverCanDetonate) {
                return false;
            }

            if (!NetworkServer.active && p.isLocalPlayer) {
                if (SniperPackets.justFiredDev) {
                    return false;
                }

                Client.GetClient().Send(MessageTypes.MsgDetonate, new DetonateMessage {
                    m_player_id = p.netId
                });
            }

            return true;
        }
    }

    /// <summary>
    /// Reads the projdata if it exists.
    /// </summary>
    [Mod(Mods.PresetData)]
    [HarmonyPatch(typeof(ProjectileManager), "ReadProjPresetData")]
    public static class ProjectileManager_ReadProjPresetData {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            var dataReader_GetProjData_Method = typeof(PresetData).GetMethod("GetProjData");
            foreach (var code in instructions)
                if (code.opcode == OpCodes.Callvirt && ((MethodInfo)code.operand).Name == "get_text")
                    yield return new CodeInstruction(OpCodes.Call, dataReader_GetProjData_Method);
                else
                    yield return code;
        }
    }
}
