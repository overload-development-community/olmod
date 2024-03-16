using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace GameMod
{

    // Skip the portion in Player.UpdateInvul() where ship movement reduces your invuln time
    [HarmonyPatch(typeof(Player), "UpdateInvul")]
    internal class MPSpawnInvuln_Player_UpdateInvul
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Stloc_1)
                {
                    state++;
                    if (state == 2)
                        code.opcode = OpCodes.Pop;
                }

                yield return code;
            }
        }
    }

    // Cancels spawn invuln if a player starts charging Thunderbolt
    [HarmonyPatch(typeof(PlayerShip), "ThunderCharge")]
    internal class MPSpawnInvuln_PlayerShip_ThunderCharge
    {
        static void Postfix(Player ___c_player)
        {
            if (GameplayManager.IsMultiplayerActive && ___c_player.m_spawn_invul_active)
            {
                ___c_player.m_timer_invuln = (float)___c_player.m_timer_invuln - (float)NetworkMatch.m_respawn_shield_seconds;
            }
        }
    }
}