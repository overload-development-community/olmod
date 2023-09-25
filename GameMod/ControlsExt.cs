using HarmonyLib;
using Overload;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace GameMod
{
    internal class ControlsExt
    {
        public static int MAX_ARRAY_SIZE = Enum.GetValues(typeof(CCInputExt)).Cast<int>().Max() + 1;

        public static string GetInputName(CCInputExt cc)
        {
            switch (cc)
            {
                case CCInputExt.TOGGLE_LOADOUT_PRIMARY:
                    return Loc.LS("TOGGLE LOADOUT PRIMARY");
                default:
                    return Loc.LS("UNKNOWN");
            }
        }

        public static int GetExclusionMask(CCInputExt input)
        {
            switch (input)
            {
                case CCInputExt.WHEEL_LEFT:
                case CCInputExt.WHEEL_RIGHT:
                case CCInputExt.WHEEL_UP:
                case CCInputExt.WHEEL_DOWN:
                    return 2;
                default:
                    switch (input)
                    {
                        case CCInputExt.HOLOGUIDE:
                        case CCInputExt.PREV_WEAPON:
                        case CCInputExt.PREV_MISSILE:
                            break;
                        default:
                            if (input != CCInputExt.SWITCH_WEAPON && input != CCInputExt.SWITCH_MISSILE)
                            {
                                return 1;
                            }
                            break;
                    }
                    return 3;
            }
        }
    }

    public enum CCInputExt
    {
        TURN_LEFT,
        TURN_RIGHT,
        PITCH_UP,
        PITCH_DOWN,
        ROLL_LEFT,
        ROLL_RIGHT,
        MOVE_FORE,
        MOVE_BACK,
        SLIDE_LEFT,
        SLIDE_RIGHT,
        SLIDE_UP,
        SLIDE_DOWN,
        ROLL_LEFT_90,
        ROLL_RIGHT_90,
        FIRE_WEAPON,
        FIRE_MISSILE,
        SWITCH_WEAPON,
        SWITCH_MISSILE,
        FIRE_FLARE,
        USE_BOOST,
        TOGGLE_HEADLIGHT,
        VIEW_MAP,
        HOLOGUIDE,
        SMASH_ATTACK,
        REAR_VIEW,
        PREV_WEAPON,
        PREV_MISSILE,
        SLIDE_MODIFIER,
        QUICKSAVE,
        TOGGLE_COCKPIT,
        FULL_CHAT,
        TOGGLE_HUD,
        RECENTER_VR,
        WEAPON_1x2,
        WEAPON_3x4,
        WEAPON_5x6,
        WEAPON_7x8,
        MISSILE_1x2,
        MISSILE_3x4,
        MISSILE_5x6,
        MISSILE_7x8,
        WHEEL_LEFT,
        WHEEL_RIGHT,
        WHEEL_UP,
        WHEEL_DOWN,
        MENU_UP,
        MENU_DOWN,
        MENU_LEFT,
        MENU_RIGHT,
        MENU_SELECT,
        MENU_BACK,
        MENU_DELETE,
        MENU_SECONDARY,
        MENU_RECENTER,
        MENU_PGUP,
        MENU_PGDN,
        MENU_HOME,
        MENU_END,
        PAUSE,
        NUM,
        NUM_CONFIGURABLE = 45,
        // Start of new entries
        TOGGLE_LOADOUT_PRIMARY = 60,
        TAUNT_1 = 61,
        TAUNT_2 = 62,
        TAUNT_3 = 63,
        TAUNT_4 = 64,
        TAUNT_5 = 65,
        TAUNT_6 = 66,

    };

    [HarmonyPatch(typeof(PlayerShip), "UpdateReadImmediateControls")]
    internal class ControlsExt_PlayerShip_UpdateReadImmediateControls
    {
        static void Postfix(PlayerShip __instance)
        {
            if (MPObserver.Enabled)
                return;

            if (Controls.JustPressed((CCInput)CCInputExt.TOGGLE_LOADOUT_PRIMARY) && GameplayManager.IsMultiplayerActive)
            {
                MPLoadouts.ToggleLoadoutPrimary(__instance.c_player);
            }
        }
    }

    [HarmonyPatch(typeof(Player), MethodType.Constructor)]
    internal class ControlsExt_Player_Constructor
    {
        static void Postfix(Player __instance)
        {
            Array.Resize<int>(ref __instance.m_input_count, ControlsExt.MAX_ARRAY_SIZE);
        }
    }

    [HarmonyPatch(typeof(Controls), "InitControl")]
    internal class ControlsExt_Controls_InitControl
    {
        static void Prefix()
        {
            Controls.m_input_joy = new RWInput[2, ControlsExt.MAX_ARRAY_SIZE];
            Controls.m_input_kc = new KeyCode[2, ControlsExt.MAX_ARRAY_SIZE];
            Controls.m_input_count = new int[ControlsExt.MAX_ARRAY_SIZE];
        }
    }

    [HarmonyPatch]
    internal class ControlsExt_PatchArraySizes
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Controls), "UpdateDevice");
            yield return AccessTools.Method(typeof(Controls), "ClearKBMouse");
            yield return AccessTools.Method(typeof(Controls), "ClearControlsForController");
            yield return AccessTools.Method(typeof(PlayerShip), "UpdateReadImmediateControls");
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldc_I4 && (int)code.operand == 59)
                    code.operand = ControlsExt.MAX_ARRAY_SIZE;

                if (code.opcode == OpCodes.Ldc_I4_S && (sbyte)code.operand == 59)
                    code.operand = (sbyte)ControlsExt.MAX_ARRAY_SIZE;

                yield return code;
            }
        }
    }

    /// <summary>
    /// The control pairing for a given CCInput is based on passed CCInput value and CCInput value + 50.
    /// We need to bump this out further to differentiate CCInput values > 50 from their alt representation.
    /// </summary>
    [HarmonyPatch(typeof(UIElement), "SelectAndDrawControlOption")]
    internal class ControlsExt_UIElement_SelectAndDrawControlOption
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldc_I4_S && (sbyte)code.operand == 50)
                {
                    yield return new CodeInstruction(OpCodes.Ldc_I4, 1000);
                    continue;
                }
                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(Controls), "ClearMatchingControls")]
    internal class ControlsExt_Controls_ClearMatchingControls
    {
        static bool Prefix(int mask, RWInput rwi)
        {
            for (int i = 0; i < 45; i++)
            {
                if ((mask & ControlsExt.GetExclusionMask((CCInputExt)i)) != 0)
                {
                    if (Controls.m_input_joy[0, i].Match(rwi))
                    {
                        Controls.m_input_joy[0, i].Clear();
                    }
                    if (Controls.m_input_joy[1, i].Match(rwi))
                    {
                        Controls.m_input_joy[1, i].Clear();
                    }
                }
            }
            for (int i = (int)CCInputExt.TOGGLE_LOADOUT_PRIMARY; i < ControlsExt.MAX_ARRAY_SIZE; i++)
            {
                if ((mask & ControlsExt.GetExclusionMask((CCInputExt)i)) != 0)
                {
                    if (Controls.m_input_joy[0, i].Match(rwi))
                    {
                        Controls.m_input_joy[0, i].Clear();
                    }
                    if (Controls.m_input_joy[1, i].Match(rwi))
                    {
                        Controls.m_input_joy[1, i].Clear();
                    }
                }
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(Controls), "SetInputJoystick")]
    internal class ControlsExt_Controls_SetInputJoystick
    {
        private static MethodInfo _ClearMatchingControls_Method = AccessTools.Method(typeof(Controls), "ClearMatchingControls");

        static bool Prefix(int idx, bool alt, RWInput rwi)
        {
            int exclusionMask = ControlsExt.GetExclusionMask((CCInputExt)idx);
            _ClearMatchingControls_Method.Invoke(null, new object[] { exclusionMask, rwi });
            int num = (!alt) ? 0 : 1;
            Controls.m_input_joy[num, idx].Copy(rwi);

            return false;
        }
    }
}