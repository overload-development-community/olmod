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

        static void MutePlayer()
        {
            string player_name = uConsole.GetString();

            if (string.IsNullOrEmpty(player_name))
            {
                Debug.Log("You didn't add the name of the player you want to mute!\nSyntax: mute <player_name>");
                return;
            }

            if(!GameplayManager.IsMultiplayerActive)
            {
                Debug.Log("You need to be in the same game as the person that you want to mute!");
                return;
            }

            player_name = player_name.ToUpper();
            foreach (PlayerLobbyData pld in NetworkMatch.m_players.Values)
            {
                if (pld.m_name.ToUpper().Equals(player_name))
                {
                    if (ExtendedConfig.Section_AudiotauntMutedPlayers.ids.Contains(pld.m_player_id))
                    {
                        Debug.Log("This player is already muted! If you want to unmute that player use the unmute command.");
                        return;
                    }
                    else
                    {
                        ExtendedConfig.Section_AudiotauntMutedPlayers.ids.Add(pld.m_player_id);
                        Debug.Log("Muted "+pld.m_name);
                        return;
                    }
                        
                }
            }
        }

        static void UnmutePlayer()
        {
            string player_name = uConsole.GetString();

            if (string.IsNullOrEmpty(player_name))
            {
                Debug.Log("You didn't add the name of the player you want to unmute!\nSyntax: unmute <player_name>");
                return;
            }

            if (!GameplayManager.IsMultiplayerActive)
            {
                Debug.Log("You need to be in the same game as the person that you want to unmute!");
                return;
            }

            player_name = player_name.ToUpper();
            foreach (PlayerLobbyData pld in NetworkMatch.m_players.Values)
            {
                if (pld.m_name.ToUpper().Equals(player_name))
                {
                    if (!ExtendedConfig.Section_AudiotauntMutedPlayers.ids.Contains(pld.m_player_id))
                    {
                        Debug.Log("This player is not muted! If you want to mute that player use the mute command.");
                        return;
                    }
                    else
                    {
                        ExtendedConfig.Section_AudiotauntMutedPlayers.ids.Remove(pld.m_player_id);
                        Debug.Log("Unmuted " + pld.m_name);
                        return;
                    }

                }
            }
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

        static void CmdVRScale() {
            if (!GameplayManager.VRActive) {
                uConsole.Log("You must be in VR to use this command.");
                return;
            }

            if (GameplayManager.m_gameplay_state != GameplayState.MENUS) {
                uConsole.Log("You must set this in the menus first, this has no effect while playing.");
                return;
            }

            string s = uConsole.GetString();

            if (float.TryParse(s, out float scale)) {
                scale = Mathf.Clamp(scale, 0.1f, 10f);

                VRScale.VR_Scale = scale;
            } else {
                uConsole.Log("Invalid scale, must be a number between 0.1 and 10.");
            }
        }

        static void CmdLoadoutMask()
        {
            if (uConsole.GetNumParameters() > 0) {
                MPLoadouts.LoadoutFilterBitmask = uConsole.GetInt();
            }
            Debug.LogFormat("loadout mask: {0} = {0:X8}", MPLoadouts.LoadoutFilterBitmask);
        }

        static bool RequireInt(string usage)
        {
            if (uConsole.NextParameterIsInt()) return true;
            uConsole.Log("qs_set " + usage + " <int>");
            return false;
        }

        static bool RequireFloat(string usage)
        {
            if (uConsole.NextParameterIsFloat()) return true;
            uConsole.Log("qs_set " + usage + " <float>");
            return false;
        }

        static void CmdSetQualitySetting()
        {
            string name = uConsole.GetString();
            if (string.IsNullOrEmpty(name))
            {
                uConsole.Log("usage: qs_set <name> <value> - run qs_show for names and current values");
                return;
            }
            switch (name.ToLowerInvariant())
            {
                case "pixellightcount":
                    if (!RequireInt("pixellightcount")) return;
                    QualitySettings.pixelLightCount = uConsole.GetInt();
                    break;
                case "texturequality":
                    if (!RequireInt("texturequality (masterTextureLimit, 0=full res)")) return;
                    QualitySettings.masterTextureLimit = uConsole.GetInt();
                    break;
                case "anisotropicfiltering":
                    if (!RequireInt("anisotropicfiltering (0=disable,1=enable,2=forceenable)")) return;
                    QualitySettings.anisotropicFiltering = (AnisotropicFiltering)uConsole.GetInt();
                    break;
                case "antialiasing":
                    if (!RequireInt("antialiasing (0,2,4,8)")) return;
                    QualitySettings.antiAliasing = uConsole.GetInt();
                    break;
                case "softparticles":
                    if (!RequireInt("softparticles (0|1)")) return;
                    QualitySettings.softParticles = uConsole.GetInt() != 0;
                    break;
                case "realtimereflectionprobes":
                    if (!RequireInt("realtimereflectionprobes (0|1)")) return;
                    QualitySettings.realtimeReflectionProbes = uConsole.GetInt() != 0;
                    break;
                case "particleraycastbudget":
                    if (!RequireInt("particleraycastbudget")) return;
                    QualitySettings.particleRaycastBudget = uConsole.GetInt();
                    break;
                case "maxqueuedframes":
                    if (!RequireInt("maxqueuedframes")) return;
                    QualitySettings.maxQueuedFrames = uConsole.GetInt();
                    break;
                case "vsynccount":
                    if (!RequireInt("vsynccount (0,1,2,3,4)")) return;
                    QualitySettings.vSyncCount = uConsole.GetInt();
                    break;
                case "blendweights":
                    if (!RequireInt("blendweights (0=0bones,1=1bone,2=2bones,4=4bones)")) return;
                    QualitySettings.blendWeights = (BlendWeights)uConsole.GetInt();
                    break;
                case "lodbias":
                    if (!RequireFloat("lodbias")) return;
                    QualitySettings.lodBias = uConsole.GetFloat();
                    break;
                case "maximumlodlevel":
                    if (!RequireInt("maximumlodlevel")) return;
                    QualitySettings.maximumLODLevel = uConsole.GetInt();
                    break;
                case "shadows":
                    if (!RequireInt("shadows (0=off,1=hard,2=all)")) return;
                    QualitySettings.shadows = (ShadowQuality)uConsole.GetInt();
                    break;
                case "shadowresolution":
                    if (!RequireInt("shadowresolution (0=low,1=medium,2=high,3=veryhigh)")) return;
                    QualitySettings.shadowResolution = (ShadowResolution)uConsole.GetInt();
                    break;
                case "shadowprojection":
                    if (!RequireInt("shadowprojection (0=closefit,1=stablefit)")) return;
                    QualitySettings.shadowProjection = (ShadowProjection)uConsole.GetInt();
                    break;
                case "shadowcascades":
                    if (!RequireInt("shadowcascades (0,1,2,4)")) return;
                    QualitySettings.shadowCascades = uConsole.GetInt();
                    break;
                case "shadowdistance":
                    if (!RequireFloat("shadowdistance")) return;
                    QualitySettings.shadowDistance = uConsole.GetFloat();
                    break;
                case "shadownearplaneoffset":
                    if (!RequireFloat("shadownearplaneoffset")) return;
                    QualitySettings.shadowNearPlaneOffset = uConsole.GetFloat();
                    break;
                default:
                     uConsole.Log("qs_set: unknown field '" + name + "'");
                    return;
            }
            uConsole.Log("qs_set: " + name.ToLowerInvariant() + " applied");
        }

        static void CmdShowQualitySettings()
        {
            uConsole.Log("--- QualitySettings (live values) ---");
            uConsole.Log("pixelLightCount = " + QualitySettings.pixelLightCount);
            uConsole.Log("textureQuality (masterTextureLimit) = " + QualitySettings.masterTextureLimit);
            uConsole.Log("anisotropicFiltering = " + QualitySettings.anisotropicFiltering);
            uConsole.Log("antiAliasing = " + QualitySettings.antiAliasing);
            uConsole.Log("softParticles = " + QualitySettings.softParticles);
            uConsole.Log("realtimeReflectionProbes = " + QualitySettings.realtimeReflectionProbes);
            uConsole.Log("particleRaycastBudget = " + QualitySettings.particleRaycastBudget);
            uConsole.Log("maxQueuedFrames = " + QualitySettings.maxQueuedFrames);
            uConsole.Log("vSyncCount = " + QualitySettings.vSyncCount);
            uConsole.Log("blendWeights = " + QualitySettings.blendWeights);
            uConsole.Log("lodBias = " + QualitySettings.lodBias);
            uConsole.Log("maximumLODLevel = " + QualitySettings.maximumLODLevel);
            uConsole.Log("shadows = " + QualitySettings.shadows);
            uConsole.Log("shadowResolution = " + QualitySettings.shadowResolution);
            uConsole.Log("shadowProjection = " + QualitySettings.shadowProjection);
            uConsole.Log("shadowCascades = " + QualitySettings.shadowCascades);
            uConsole.Log("shadowDistance = " + QualitySettings.shadowDistance);
            uConsole.Log("shadowNearPlaneOffset = " + QualitySettings.shadowNearPlaneOffset);
        }

        public static void RegisterCommands()
        {
            uConsole.RegisterCommand("mute", "Mute a specific player", new uConsole.DebugCommand(MutePlayer));
            uConsole.RegisterCommand("unmute", "Unmute a specific player", new uConsole.DebugCommand(UnmutePlayer));
            uConsole.RegisterCommand("dump_segments", "Dump segment data", new uConsole.DebugCommand(CmdDumpSegments));
            uConsole.RegisterCommand("mipmap_bias", "Set Mipmap bias (-16 ... 15.99)", new uConsole.DebugCommand(CmdMipmapBias));
            uConsole.RegisterCommand("reload_missions", "Reload missions", new uConsole.DebugCommand(CmdReloadMissions));
            uConsole.RegisterCommand("toggle_debugging", "Toggle the display of debugging info", new uConsole.DebugCommand(CmdToggleDebugging));
            uConsole.RegisterCommand("ui_color", "Set UI color #aabbcc", new uConsole.DebugCommand(CmdUIColor));
            uConsole.RegisterCommand("vr_scale", "Set VR scale (0.1 to 10)", new uConsole.DebugCommand(CmdVRScale));
            uConsole.RegisterCommand("xp", "Set XP", new uConsole.DebugCommand(CmdXP));
            uConsole.RegisterCommand("loadout_mask", "Manually set the loadout mask", CmdLoadoutMask);
            uConsole.RegisterCommand("qs_set", "sets a raw Unity QualitySettings field by name (qs_set <name> <value>); run qs_show for names/current values", new uConsole.DebugCommand(CmdSetQualitySetting));
            uConsole.RegisterCommand("qs_show", "prints the current value of every Unity QualitySettings field", new uConsole.DebugCommand(CmdShowQualitySettings));
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

    // Consolidated into Menus_UIElement_DrawControlsMenu in Menus.cs
    /*
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
    */

    // Consolidated into Menus_MenuManager_ControlsOptionsUpdate in Menus.cs
    /*
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
                MenuManager.m_menu_micro_state == 2 &&
                UIManager.m_menu_selection == 9 &&
                (UIManager.PushedSelect(100) || UIManager.PushedDir())
                )
            {
                Console.KeyEnabled = !Console.KeyEnabled;
                MenuManager.PlayCycleSound(1f);
            }
        }
    }
    */

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
