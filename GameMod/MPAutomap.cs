using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace GameMod
{
    class MPAutomap
    {
        public static CCInput binding = CCInput.QUICKSAVE;
    }

    /// <summary>
    /// Remove check for IsMultiplayer before initializing GameplayManager.m_automap
    /// </summary>
    [HarmonyPatch(typeof(GameplayManager), "StartLevel")]
    class MPAutomap_GameplayManager_StartLevel
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;

            foreach (var code in codes)
            {
                if (state == 0 && code.opcode == OpCodes.Call && code.operand == AccessTools.Property(typeof(GameplayManager), "IsMultiplayer").GetGetMethod())
                {
                    state = 1;
                    code.opcode = OpCodes.Ldc_I4_0;
                    code.operand = null;
                }
                yield return code;
            }
        }
    }

    /// <summary>
    /// Add handler for MP Automap binding
    /// </summary>
    [HarmonyPatch(typeof(PlayerShip), "UpdateReadImmediateControls")]
    class MPAutomap_PlayerShip_UpdateReadImmediateControls
    {
        static void MaybeOpenAutomap(PlayerShip playerShip)
        {
            if (playerShip.m_wheel_select_state == WheelSelectState.NONE && GameplayManager.IsMultiplayerActive && Controls.JustPressed(MPAutomap.binding) && GameplayManager.m_gameplay_state != GameplayState.AUTOMAP)
            {
                GameplayManager.OpenAutomap();
            }
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(PlayerShip), "IssueGuidebotCommandFromWheel"))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPAutomap_PlayerShip_UpdateReadImmediateControls), "MaybeOpenAutomap"));
                }
                yield return code;
            }
        }
    }

    /// <summary>
    /// Add handler for closing MP Automap
    /// </summary>
    [HarmonyPatch(typeof(Automap), "Update")]
    class MPAutomap_Automap_Update
    {
        static void MaybeDestroyAutomap(Automap automap)
        {
            if (GameplayManager.IsMultiplayerActive && Controls.JustPressed(MPAutomap.binding))
            {
                AccessTools.Field(typeof(Automap), "m_state_timer").SetValue(automap, 0.5f);
                automap.m_automap_state = Automap.AutomapState.EXIT;
                UIManager.DestroyType(UIElementType.MAP_HUD, false);
            }
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Callvirt && code.operand == AccessTools.Property(typeof(UnityEngine.Transform), "position").GetSetMethod())
                {
                    state++;
                    if (state == 2)
                    {
                        yield return code;
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPAutomap_Automap_Update), "MaybeDestroyAutomap"));
                        continue;
                    }
                }
                yield return code;
            }
        }
    }
}
