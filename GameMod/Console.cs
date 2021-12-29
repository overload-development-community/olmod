using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod {
    static class Console
    {
        public static bool KeyEnabled;
        public static int CustomUIColor;

        private static MethodInfo _GameManager_InitializeMissionList_Method = typeof(GameManager).GetMethod("InitializeMissionList", AccessTools.all);
        public static void CmdReloadMissions()
        {
            MBLevelPatch.SLInit = false;
            _GameManager_InitializeMissionList_Method.Invoke(GameManager.m_gm, null);
            uConsole.Log("Missions reloaded (" + GameManager.GetAvailableMissions().Length + " sp, " +
                GameManager.ChallengeMission.NumLevels + " cm, " + GameManager.MultiplayerMission.NumLevels + " mp)");
        }

        static void CmdXP()
        {
            int xp = uConsole.GetInt();
            if (xp == -1)
            {
                xp = GameManager.m_local_player.m_xp;
                if (xp >= 20000)
                {
                    uConsole.Log("XP is " + xp);
                    return;
                }
                xp = 20000;
            }
            GameManager.m_local_player.m_xp = xp;
            MenuManager.LocalSetInt("PS_XP2", xp);
            uConsole.Log("XP set to " + xp);
        }

        static void CmdMipmapBias()
        {
            if (!uConsole.NextParameterIsFloat())
            {
                uConsole.Log("Missing float argument");
                return;
            }
            float bias = uConsole.GetFloat();
            var texIds = new[] { Shader.PropertyToID("_MainTex"), Shader.PropertyToID("_EmissionMap"),
                Shader.PropertyToID("_EmissionMap"), Shader.PropertyToID("_MetallicGlossMap"),
                Shader.PropertyToID("_BumpMap") };
            int n = 0;
            foreach (Renderer renderer in UnityEngine.Object.FindObjectsOfType<Renderer>())
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader.name != "Standard")
                        continue;
                    foreach (var texId in texIds)
                    {
                        var texture = material.GetTexture(texId);
                        if (texture != null && texture.mipMapBias != bias)
                        {
                            texture.mipMapBias = bias;
                            n++;
                        }
                    }
                }
            uConsole.Log("Changed " + n + " textures");
        }

        public static void ApplyCustomUIColor()
        {
            int n = CustomUIColor;
            if (n == 0) {
                UIManager.UpdateUIColors(MenuManager.opt_hud_color);
                return;
            }
            float h0 = UIManager.UI_MAIN_HUE[0], s0 = UIManager.UI_MAIN_SAT[0], b0 = UIManager.UI_MAIN_BRI[0];
            var cc = new Color((n >> 16) / 255f, ((n >> 8) & 0xff) / 255f, (n & 0xff) / 255f);
            HSBColor c = HSBColor.FromColor(cc);
            UIManager.UI_MAIN_HUE[0] = c.h;
            UIManager.UI_MAIN_SAT[0] = c.s;
            UIManager.UI_MAIN_BRI[0] = c.b;
            UIManager.UpdateUIColors(0);
            UIManager.UI_MAIN_HUE[0] = h0;
            UIManager.UI_MAIN_SAT[0] = s0;
            UIManager.UI_MAIN_BRI[0] = b0;
        }

        static void CmdUIColor()
        {
            string s = uConsole.GetString();
            int n = 0;
            if (s != null)
            {
                if (s.StartsWith("#"))
                    s = s.Substring(1);
                if ((s.Length != 3 && s.Length != 6) ||
                    !int.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out n))
                {
                    uConsole.Log("Invalid color: " + s);
                    return;
                }
                if (s.Length == 3)
                    n = ((((n >> 8) & 0xf) * 0x11) << 16) | ((((n >> 4) & 0xf) * 0x11) << 8) | ((n & 0xf) * 0x11);
            }
            if (CustomUIColor == n) {
                uConsole.Log("UI Color unchanged");
                return;
            }
            CustomUIColor = n;
            ApplyCustomUIColor();
            uConsole.Log("UI Color changed");
        }

        static void CmdToggleDebugging() {
            Debugging.Enabled = !Debugging.Enabled;
        }

        static void CmdDumpSegments() {
            for (int segmentIndex = 0; segmentIndex < GameManager.m_level_data.Segments.Length; segmentIndex++) {
                Debug.Log($"Segment Index: {segmentIndex}");
                var segment = GameManager.m_level_data.Segments[segmentIndex];
                Debug.Log($"  Center: x: {segment.Center.x:N4}, y: {segment.Center.y:N4}, z: {segment.Center.z:N4}");

                for (int portalIndex = 0; portalIndex < segment.Portals.Length; portalIndex++) {
                    Debug.Log($"    Portal Index: {portalIndex} Value: {segment.Portals[portalIndex]}");
                    if (segment.Portals[portalIndex] == -1) {
                        continue;
                    }
                    var portal = GameManager.m_level_data.Portals[segment.Portals[portalIndex]];
                    Debug.Log($"      {(portal.MasterSegmentIndex == segmentIndex ? $"Master, Side: {portal.MasterSideIndex}, Other Side: {portal.SlaveSideIndex}" : $"Slave, Side: {portal.SlaveSideIndex}, Other Side: {portal.MasterSideIndex}")}");
                    Debug.Log($"      Other Segment Index: {(portal.MasterSegmentIndex == segmentIndex ? portal.SlaveSegmentIndex : portal.MasterSegmentIndex)}");
                }
            }

            uConsole.Log("Segments dumped to debug log.");
        }

        // Not working.  See VRScale.cs.
        //static void CmdVRScale() {
        //    if (!GameplayManager.VRActive) {
        //        uConsole.Log("You must be in VR to use this command.");
        //        return;
        //    }

        //    string s = uConsole.GetString();

        //    if (float.TryParse(s, out float scale)) {
        //        scale = Mathf.Clamp(scale, 0.1f, 10f);

        //        VRScale.VR_Scale = scale;
        //    } else {
        //        uConsole.Log("Invalid scale, must be a number between 0.1 and 10.");
        //    }
        //}

        public static void RegisterCommands()
        {
            uConsole.RegisterCommand("dump_segments", "Dump segment data", new uConsole.DebugCommand(CmdDumpSegments));
            uConsole.RegisterCommand("mipmap_bias", "Set Mipmap bias (-16 ... 15.99)", new uConsole.DebugCommand(CmdMipmapBias));
            uConsole.RegisterCommand("reload_missions", "Reload missions", new uConsole.DebugCommand(CmdReloadMissions));
            uConsole.RegisterCommand("toggle_debugging", "Toggle the display of debugging info", new uConsole.DebugCommand(CmdToggleDebugging));
            uConsole.RegisterCommand("ui_color", "Set UI color #aabbcc", new uConsole.DebugCommand(CmdUIColor));
            // Not working.  See VRScale.cs.
            // uConsole.RegisterCommand("vr_scale", "Set VR scale (0.1 to 10)", new uConsole.DebugCommand(CmdVRScale));
            uConsole.RegisterCommand("xp", "Set XP", new uConsole.DebugCommand(CmdXP));
        }
    }


    [HarmonyPatch(typeof(GameManager), "Start")]
    class ConsolePatch
    {
        static void Postfix(GameManager __instance)
        {
            GameObject go = UnityEngine.Object.Instantiate((GameObject)Resources.Load("uConsole"));
            go.transform.parent = __instance.transform;
            Console.RegisterCommands();
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawControlsMenu")]
    class ConsoleOptionPatch
    {
        public static void DrawConsoleOption(UIElement uie, ref Vector2 position)
        {
            position.y += 62f;
            uie.SelectAndDrawStringOptionItem(Loc.LS("ENABLE CONSOLE KEY"), position, 9, MenuManager.GetToggleSetting(Console.KeyEnabled ? 1 : 0), Loc.LS("ACTIVATE CONSOLE WITH ` KEY"), 1.5f, false);
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var consoleOptionPatch_DrawConsoleOption_Method = AccessTools.Method(typeof(ConsoleOptionPatch), "DrawConsoleOption");

            int state = 0; // 0 = before adv.ctrl, 1 = before 248f, 2 = before stloc (last opt), 3 = before last SelectAndDrawStringOptionItem, 4 = rest
            foreach (var code in codes) {
                if (state == 0 && code.opcode == OpCodes.Ldstr && (string)code.operand == "CONTROL OPTIONS - ADVANCED") {
                    state = 1;
                } else if (state == 1 && code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 248f) {
                    code.operand = 248f + 48f;
                    state = 2;
                } else if (state == 2 && (code.opcode == OpCodes.Stloc || code.opcode == OpCodes.Stloc_S)) {
                    state = 3;
                } else if (state == 3 && code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "SelectAndDrawStringOptionItem") {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldloca, 0);
                    yield return new CodeInstruction(OpCodes.Call, consoleOptionPatch_DrawConsoleOption_Method);
                    state = 4;
                    continue;
                }
                yield return code;
            }
        }
    }

    // Changed from Postfix to Transpile to fix left arrow, insert processing directly after MaybeReverseOption
    [HarmonyPatch(typeof(MenuManager), "ControlsOptionsUpdate")]
    class ConsoleOptionTogglePatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var consoleOptionTogglePatch_HandleConsoleToggle_Method = AccessTools.Method(typeof(ConsoleOptionTogglePatch), "HandleConsoleToggle");

            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "MaybeReverseOption")
                {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Call, consoleOptionTogglePatch_HandleConsoleToggle_Method);
                    continue;
                }

                yield return code;
            }
        }

        private static void HandleConsoleToggle()
        {
            if (MenuManager.m_menu_sub_state == MenuSubState.ACTIVE &&
                (UIManager.PushedSelect(100) || UIManager.PushedDir()) && 
                MenuManager.m_menu_micro_state == 2 &&
                UIManager.m_menu_selection == 9)
            {
                Console.KeyEnabled = !Console.KeyEnabled;
                MenuManager.PlayCycleSound(1f);
            }
        }
    }

    [HarmonyPatch(typeof(uConsoleInput), "ProcessActivationInput")]
    class ConsoleEnablePatch
    {
        private static bool Prefix()
        {
            return Console.KeyEnabled || uConsole.IsOn();
        }
    }

    [HarmonyPatch(typeof(MenuManager), "ApplyPreferences")]
    class CustomColorPatch
    {
        private static void Postfix()
        {
            Console.ApplyCustomUIColor();
        }
    }
}
