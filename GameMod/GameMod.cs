using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Harmony;
using Overload;

namespace GameMod.Core
{
    public class GameMod
    {
        internal static void Initialize()
        {
            var harmony = HarmonyInstance.Create("ol.gamemod");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        [HarmonyPatch(typeof(Overload.MenuManager))]
        [HarmonyPatch("MpMatchSetup")]
        class MBModeSelPatch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                for (var i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Ldsfld && (codes[i].operand as FieldInfo).Name == "mms_mode")
                    {
                        i++;
                        if (codes[i].opcode == OpCodes.Ldc_I4_2)
                            codes[i].opcode = OpCodes.Ldc_I4_3;
                        i++;
                        while (codes[i].opcode == OpCodes.Add || codes[i].opcode == OpCodes.Ldsfld)
                            i++;
                        if (codes[i].opcode == OpCodes.Ldc_I4_2)
                            codes[i].opcode = OpCodes.Ldc_I4_3;
                    }
                }
                return codes;
            }
        }

        [HarmonyPatch(typeof(Overload.UIElement))]
        [HarmonyPatch("DrawMainMenu")]
        class VersionPatch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                for (var i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Ldstr && codes[i].operand as string == "VERSION {0}.{1} BUILD {2}")
                    {
                        codes[i].operand = "VERSION {0}.{1} BUILD {2} MOD";
                    }
                }
                return codes;
            }

            static void Postfix(UIElement __instance)
            {
                Vector2 pos = new Vector2(UIManager.UI_RIGHT - 10f, -155f - 60f + 50f + 40f);
                __instance.DrawStringSmall("UNOFFICIAL MODIFIED VERSION!", pos,
                    0.35f, StringOffset.RIGHT, UIManager.m_col_ui1, 0.5f, -1f);
            }
        }

        [HarmonyPatch(typeof(Overload.GameManager))]
        [HarmonyPatch("ScanForLevels")]
        class MBLevelPatch
        {
            static bool SLInit = false;
            static void Prefix()
            {
                if (SLInit)
                    return;
                SLInit = true;
                Overload.GameManager.MultiplayerMission.AddLevel("mb_arena1", "ARENA", "TITAN_06", new int[]
                            {
                                1,
                                4,
                                2,
                                8
                            });
            }
        }
    }
}
