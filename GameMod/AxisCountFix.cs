using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace GameMod
{
    // fixes that when swapping to a different controller overload's menu code doesnt check,
    // wether the currently selected axis is out of bounds for the new controller which would otherwise result in a freeze
    class AxisCountFix
    {
        [HarmonyPatch(typeof(MenuManager), "ControlsOptionsUpdate")]
        internal class AxisCountFix_MenuManager_ControlsOptionsUpdate
        {
            public static void MaybeAdjustAxisSelection(int controller)
            {
                if (Controls.m_controllers[controller].m_joystick.axisCount <= MenuManager.m_calibration_current_axis)
                    MenuManager.m_calibration_current_axis = 0;
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
            {
                int state = 0;
                foreach (var code in codes)
                {
                    yield return code;
                    if (state == 0 && code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "GetPrevControllerWithAxes")
                        state = 1;
                    if (state == 5)
                    {
                        yield return new CodeInstruction(OpCodes.Ldloc_S, 9);
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(AxisCountFix_MenuManager_ControlsOptionsUpdate), "MaybeAdjustAxisSelection"));
                        state++;
                        continue;
                    }
                    if (state > 0)
                        state++;

                }
            }
        }
    }
}