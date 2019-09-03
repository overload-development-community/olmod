using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Harmony;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod
{
    /*
    [HarmonyPatch(typeof(Overload.Player), "NeedToSendFixedUpdateMessages")]
    class MPObserverFixedUpdate
    {
        static bool Prefix(ref bool __result)
        {
            if (MPObserver.Enabled)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
    */

    // disable receiving server position on observer mode
    [HarmonyPatch(typeof(Overload.Client), "ReconcileServerPlayerState")]
    class MPObserverReconcile
    {
        static bool Prefix()
        {
            return !MPObserver.Enabled;
        }
    }

    static class MPObserver
    {
        public static bool Enabled;
        public static void Enable()
        {
            if (Enabled)
                return;
            Enabled = true;
            GameplayManager.AddHUDMessage("Observer mode enabled");
            PlayerShip.EnablePlayerLevelCollision(false);
            ChunkManager.ForceActivateAll();
            GameplayManager.m_use_segment_visibility = false;

            /*
            GameManager.m_player_ship.m_player_died_due_to_timer = true;
            GameManager.m_player_ship.m_dying = true;
            GameManager.m_player_ship.m_dying_timer = 0f;
            GameManager.m_player_ship.m_dying_explode_timer = 0f;
            */

            //NetworkManager.RemovePlayer(GameManager.m_player_ship.c_player);
            if (GameplayManager.IsMultiplayer)
            {
                GameManager.m_player_ship.c_player.Networkm_spectator = true;
                GameManager.m_local_player.Networkm_spectator = true;
                GameManager.m_player_ship.c_player.m_spectator = true;
                GameManager.m_local_player.m_spectator = true;
            }
            else
            {
                GameManager.m_local_player.SetCheaterFlag(true);
                foreach (var robot in RobotManager.m_master_robot_list)
                    if (robot != null && !robot.gameObject.activeSelf)
                        robot.gameObject.SetActive(true);
            }
        }
    }

    // detect "observer" cheat code
    [HarmonyPatch(typeof(Overload.PlayerShip))]
    [HarmonyPatch("FrameUpdateReadKeysFromInput")]
    class MPObserverReadKeys
    {
        private static string code = "observer";
        private static int codeIdx = 0;

        static void Prefix()
        {
            foreach (char c in Input.inputString)
            {
                if (code[codeIdx] == c)
                    if (++codeIdx < code.Length)
                        continue;
                    else if (!GameplayManager.IsMultiplayer)
                        MPObserver.Enable();
                codeIdx = 0;
            }
        }
    }

    // disable observer mode on new game
    [HarmonyPatch(typeof(Overload.GameplayManager), "CreateNewGame")]
    class MPObserverReset
    {
        static void Prefix()
        {
            GameplayManager.m_use_segment_visibility = true;
            MPObserver.Enabled = false;
            if (GameManager.m_player_ship != null && GameManager.m_player_ship.c_camera != null)
                GameManager.m_player_ship.c_camera.useOcclusionCulling = true;
        }
    }

    // force robots active for (sp) observer mode
    [HarmonyPatch(typeof(RobotManager), "ActivateRobot")]
    class RobotActivatePatch
    {
        static void Prefix(ref bool force_active)
        {
            if (MPObserver.Enabled)
                force_active = true;
        }
    }

    // Don't do chunk/probe/light (de)activation for observer
    [HarmonyPatch(typeof(RobotManager), "Update")]
    class MPObserverChunks
    {
        static bool Prefix()
        {
            return !GameManager.m_local_player.m_spectator;
        }
    }

    // Don't do light (de)activation for observer
    [HarmonyPatch(typeof(ChunkManager), "UpdateLights")]
    class MPObserverLights
    {
        static bool Prefix()
        {
            return !GameManager.m_local_player.m_spectator;
        }
    }

    // Don't do light fade for observer
    [HarmonyPatch(typeof(ChunkManager), "FadeLights")]
    class MPObserverLightsFade
    {
        static bool Prefix()
        {
            return !GameManager.m_local_player.m_spectator;
        }
    }

    /*
    [HarmonyPatch(typeof(Overload.Client))]
    [HarmonyPatch("SendReadyForCountdownMessage")]
    class MPObserverDeadUpdatePatch
    {
        static void Postfix()
        {
            if (PilotManager.PilotName == "OBSERVER")
                MPObserver.Enable();
        }
    }
    */

    // enable observer mode in server for player with name starting with "OBSERVER"
    /*
    [HarmonyPatch(typeof(Overload.Server), "OnAddPlayerMessage")]
    class MPObserverSpawnPatch
    {
        static void Postfix(NetworkMessage msg)
        {
            Debug.LogFormat("OnAddPlayerMessage postfix");
            Player player = Server.FindPlayerByConnectionId(msg.conn.connectionId);
            if (player.m_mp_name.StartsWith("OBSERVER"))
            {
                Debug.LogFormat("Enabling spectator for {0}", player.m_mp_name);
                player.Networkm_spectator = true;
                Debug.LogFormat("Enabled spectator for {0}", player.m_mp_name);
            }
        }
    }
    */

    // Modify level / settings for observer. Need to wait for OnMatchStart to be sure m_spectator is set
    [HarmonyPatch(typeof(Client), "OnMatchStart")]
    class MPObserverModifyLevel
    {
        static void Postfix()
        {
            //Debug.Log("OnMatchStart player " + GameManager.m_local_player.m_mp_name + " observer " + GameManager.m_local_player.m_spectator);
            if (GameplayManager.IsDedicatedServer() || !GameManager.m_local_player.m_spectator)
                return;
            RenderSettings.skybox = null;
            GameplayManager.m_use_segment_visibility = false;
            NetworkMatch.m_show_enemy_names = MatchShowEnemyNames.ALWAYS;
            GameManager.m_player_ship.c_camera.useOcclusionCulling = false;
            ChunkManager.ForceActivateAll();
        }
    }

    [HarmonyPatch(typeof(Overload.Server), "AllConnectionsHavePlayerReadyForCountdown")]
    class MPObserverSpawnPatch
    {
        static void Postfix(bool __result)
        {
            if (!__result)
                return;
            foreach (KeyValuePair<int, PlayerLobbyData> keyValuePair in NetworkMatch.m_players)
                if (keyValuePair.Value.m_name.StartsWith("OBSERVER"))
                {
                    Player player = Server.FindPlayerByConnectionId(keyValuePair.Value.m_id);
                    if (!player || player.m_spectator)
                        continue;
                    Debug.LogFormat("Enabling spectator for {0}", player.m_mp_name);
                    player.Networkm_spectator = true;
                    Debug.LogFormat("Enabled spectator for {0}", player.m_mp_name);
                }
        }
    }

    // remove very slow turning with observer (spectator) mode
    [HarmonyPatch(typeof(Overload.PlayerShip), "FixedUpdateProcessControlsInternal")]
    class MPObserverFixedUpdateProcess
    {
        static IEnumerable<CodeInstruction> Transpiler(ILGenerator ilGen, IEnumerable<CodeInstruction> instructions)
        {
            int n = 0;
            var codes = new List<CodeInstruction>(instructions);
            for (var i = 0; i < codes.Count; i++)
            {
                if (n == 0 && codes[i].opcode == OpCodes.Callvirt && (codes[i].operand as MemberInfo).Name == "get_mass" &&
                    codes[i + 1].opcode == OpCodes.Mul)
                {
                    Label l = ilGen.DefineLabel();
                    codes[i + 2].labels.Add(l);
                    var newCodes = new[] {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PlayerShip), "c_player")),
                        new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Player), "m_spectator")),
                        new CodeInstruction(OpCodes.Brfalse, l),
                        new CodeInstruction(OpCodes.Ldc_R4, 2f),
                        new CodeInstruction(OpCodes.Mul) };
                    codes.InsertRange(i + 2, newCodes);
                    i += 2 + newCodes.Length - 1;
                    n++;
                }
                else if (codes[i].opcode == OpCodes.Ldfld && (codes[i].operand as FieldInfo).Name == "m_spectator" &&
                    codes[i + 1].opcode == OpCodes.Brfalse &&
                    i > 2 && codes[i - 1].opcode == OpCodes.Ldfld && codes[i - 2].opcode == OpCodes.Ldarg_0)
                {
                    // codes[i].opcodes = OpCodes.Ldc_I4_0 doesn't work? (class still on stack?)
                    //codes[i] = new CodeInstruction(OpCodes.Ldc_I4_0);
                    codes[i - 2] = new CodeInstruction(OpCodes.Br, codes[i + 1].operand);
                    n++;
                    break;
                }
            }
            Debug.Log("Patched FixedUpdateProcessControlsInternal n=" + n);
            return codes;
        }
    }

    // remove m_spectator check so it also receives fire messages
    [HarmonyPatch(typeof(Server), "SendProjectileFiredToClients")]
    class MPObserverFired
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            CodeInstruction last = null;
            int state = 0; // 0 = before m_spectator, 1 = wait for brtrue, 2 = after brtrue
            foreach (var code in codes)
            {
                if (state == 0 && code.opcode == OpCodes.Ldfld && (code.operand as FieldInfo).Name == "m_spectator")
                {
                    last = null; // also remove previous Ldloc0
                    state = 1;
                    continue;
                }
                else if (state == 1)
                {
                    if (code.opcode == OpCodes.Brtrue)
                        state = 2;
                    continue;
                }
                if (last != null)
                    yield return last;
                last = code;
            }
            if (last != null)
                yield return last;
        }
    }

    [HarmonyPatch(typeof(Player), "CmdSendFullChat")]
    class MPObserverChat
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            CodeInstruction last = null;
            foreach (var c in codes)
            {
                // replace xxx.m_spectator with false
                if (c.opcode == OpCodes.Ldfld && ((FieldInfo)c.operand).Name == "m_spectator")
                {
                    last = new CodeInstruction(OpCodes.Ldc_I4_0);
                    continue;
                }
                if (last != null)
                    yield return last;
                last = c;
            }
            yield return last;
        }
    }

    // show parts of the hud in observer mode
    [HarmonyPatch(typeof(UIElement), "DrawHUD")]
    class MPObserverHUD
    {
        static float getAlphaEyeGaze(UIElement uie, string pos)
        {
            return (float)typeof(UIElement).GetMethod("getAlphaEyeGaze", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(
                                uie, new object[] { pos });
        }

        static void DrawMpScoreboardRaw(UIElement uie, Vector2 vector)
        {
            typeof(UIElement).GetMethod("DrawMpScoreboardRaw", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(
                    uie, new object[] { vector });
        }

        static void Prefix(UIElement __instance)
        {
            if (!GameManager.m_local_player.m_spectator || !GameplayManager.ShowHud || Overload.NetworkManager.IsHeadless() ||
                GameManager.m_local_player.m_pregame)
                return;

            var uie = __instance;
            float alpha = uie.m_alpha;
            uie.m_alpha *= UIElement.HUD_ALPHA;
            uie.DrawMessages();

            Vector2 vector;
            if (!GameplayManager.ShowMpScoreboard)
            {
                vector.y = UIManager.UI_TOP + 70f;
                vector.x = UIManager.UI_RIGHT - 20f;
                uie.m_alpha = Mathf.Min(getAlphaEyeGaze(uie, "upperright"), uie.m_alpha);
                //uie.DrawXPTotalSmall(vector);
                vector.y += 26f;
                vector.x -= 89f;
                uie.DrawHUDScoreInfo(vector);
            }
            else
            {
                vector.y = -240f;
                vector.x = 0f;
                uie.DrawStringSmall(NetworkMatch.GetModeString(MatchMode.NUM), vector, 0.75f, StringOffset.CENTER, UIManager.m_col_ui5, 1f, -1f);
                vector.y += 25f;
                uie.DrawStringSmall(GameplayManager.Level.DisplayName, vector, 0.5f, StringOffset.CENTER, UIManager.m_col_ui1, 1f, -1f);
                vector.y += 35f;
                DrawMpScoreboardRaw(uie, vector);
            }
            uie.m_alpha = alpha;
        }
    }
}
