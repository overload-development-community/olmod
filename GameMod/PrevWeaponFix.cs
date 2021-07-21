using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace GameMod
{

    /// <summary>
    /// Author: Tobias/luponix (adaptation of luponix' identified fix)
    /// Created: 2019-08-21
    /// </summary>
    [HarmonyPatch(typeof(PlayerShip), "UpdateReadImmediateControls")]
    class PrevWeaponFix
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                // Before if (Controls.IsPressed(CCInput.SWITCH_MISSILE))
                if (code.opcode == OpCodes.Ldc_I4_S && (sbyte)code.operand == 17)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PrevWeaponFix), "PrevWeaponUpdate"));
                }

                yield return code;
            }
        }

        static void PrevWeaponUpdate(PlayerShip player)
        {
            player.c_player.CallCmdSetCurrentWeapon(player.c_player.m_weapon_type);
        }
    }
}
