using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace GameMod
{
    class FramerateLimiter
    {
        public static int target_framerate = 0;
        public const int maximum_framerate = 360;

        // Insert the framelimiter menu option
        [HarmonyPatch(typeof(UIElement), "DrawGraphicsMenu")]
        class FrameLimiter_DrawGraphicsMenu
        {
            public static void DrawFrameLimiterSlider(UIElement uie, ref Vector2 position)
            {
                position.y += 62f;
                Menus_UIElement_DrawMpOptions.SelectAndDrawSliderItem(uie, "FRAMERATE LIMIT", position, 282, target_framerate, 360, "SETS THE MAXIMUM FRAMERATE. THIS WILL BE OVERRIDDEN WHEN VSYNC IS ACTIVE");

            }


            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
            {
                int state = 0;
                foreach (var code in codes)
                {
                    // 1. adjust the spacing
                    if (state == 0 && code.opcode == OpCodes.Ldc_R4)
                    {
                        state = 1;
                        yield return new CodeInstruction(OpCodes.Ldc_R4, 19f);
                        continue;
                    }
                    if (state == 1 && code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(UIElement), "DrawHeaderMedium"))
                    {
                        state = 2;
                        yield return code;
                        yield return new CodeInstruction(OpCodes.Ldloca_S, 0);
                        yield return new CodeInstruction(OpCodes.Dup);
                        yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(UnityEngine.Vector2), "y"));
                        yield return new CodeInstruction(OpCodes.Ldc_R4, 55f);
                        yield return new CodeInstruction(OpCodes.Sub);
                        yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(UnityEngine.Vector2), "y"));
                        continue;
                    }

                    // 2. start after the description of the vsync option
                    if (state == 2 && code.opcode == OpCodes.Ldstr && (string)code.operand == "SYNC GAME DRAWING TO MONITOR REFRESH RATE")
                        state = 3;

                    // 3. insert the hook for drawing the new option
                    if (state == 3 && code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(UIElement), "SelectAndDrawStringOptionItem"))
                    {
                        state = 4;
                        yield return code;
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Ldloca_S, 0);
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FrameLimiter_DrawGraphicsMenu), "DrawFrameLimiterSlider"));
                        continue;
                    }

                    yield return code;
                }
            }
        }

        
        [HarmonyPatch(typeof(MenuManager), "GraphicsOptionsUpdate")]
        class FrameLimiter_GraphicsOptionsUpdate
        {
            public static void UpdateFrameLimiterSlider(int menu_selection)
            {
                switch (menu_selection)
                {
                    case 282:
                        if (MenuManager.option_dir && UIManager.PushedDir()){
                            target_framerate += UIManager.m_select_dir;
                            Application.targetFrameRate = FramerateLimiter.target_framerate;
                            MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                            uConsole.Log("Set the target framerate to " + FramerateLimiter.target_framerate);
                        }
                        else{
                            if (Input.GetMouseButtonUp(0)){
                                target_framerate = (int)(UIElement.SliderPos * maximum_framerate);
                                Application.targetFrameRate = FramerateLimiter.target_framerate;
                                MenuManager.PlaySelectSound(1f);
                                uConsole.Log("Set the target framerate to " + FramerateLimiter.target_framerate);
                            }
                            else if (Input.GetMouseButton(0))
                                target_framerate = (int)(UIElement.SliderPos * maximum_framerate);
                        }
                        break;
                }
            }

            public static void Postfix()
            {
                UpdateFrameLimiterSlider(UIManager.m_menu_selection);
            }

        }
        
    }
}
