using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod {
    // disable receiving server position on observer mode
    [HarmonyPatch(typeof(Client), "ReconcileServerPlayerState")]
    class MPObserverReconcile
    {
        static bool Prefix()
        {
            return !MPObserver.Enabled;
        }
    }

    public static class MPObserver
    {
        public static bool Enabled;
        public static bool DamageNumbersEnabled;
        public static Player ObservedPlayer = null;
        public static bool ThirdPerson = false;
        public static Vector3 SavedPosition = Vector3.zero;
        public static Quaternion SavedRotation = Quaternion.identity;
        public static Quaternion LastRotation = Quaternion.identity;

        public static void Enable()
        {
            if (Enabled)
                return;
            Enabled = true;
            PlayerShip.EnablePlayerLevelCollision(false);
            ChunkManager.ForceActivateAll();
            RenderSettings.skybox = null;
            GameplayManager.m_use_segment_visibility = false;
            GameManager.m_player_ship.c_camera.useOcclusionCulling = false;

            if (GameplayManager.IsMultiplayer)
            {
                NetworkMatch.m_show_enemy_names = MatchShowEnemyNames.ALWAYS;
                GameplayManager.AddHUDMessage("Observer mode - Use switch weapon to select player, fire missle for third person view");
            }
            else
            {
                GameManager.m_local_player.SetCheaterFlag(true);
                foreach (var robot in RobotManager.m_master_robot_list)
                    if (robot != null && !robot.gameObject.activeSelf)
                        robot.gameObject.SetActive(true);
                GameplayManager.AddHUDMessage("Observer mode enabled");
            }
        }

        public static void SetPlayerVisibility(Player player, bool visible)
        {
            foreach (var child in player.c_player_ship.GetComponentsInChildren<MeshRenderer>())
            {
                child.enabled = visible;
            }
        }

        public static void SetObservedPlayer(Player player)
        {
            if (ObservedPlayer == null)
            {
                SavedPosition = GameManager.m_player_ship.c_transform.position;
                SavedRotation = GameManager.m_player_ship.c_transform.rotation;
            }
            else
            {
                SetPlayerVisibility(ObservedPlayer, true);
            }

            ObservedPlayer = player;

            if (ObservedPlayer == null)
            {
                GameManager.m_player_ship.c_transform.position = SavedPosition;
                GameManager.m_player_ship.c_transform.rotation = SavedRotation;
            }
            else
            {
                SetPlayerVisibility(ObservedPlayer, ThirdPerson);
            }

            GameManager.m_viewer.SetDamageEffects(-999);
        }

        public static void SwitchObservedPlayer(bool prev)
        {
            int playerNum = ObservedPlayer == null ? -1 : Overload.NetworkManager.m_Players.IndexOf(ObservedPlayer);
            while (true)
            {
                playerNum += prev ? -1 : 1;
                if (playerNum < -1)
                {
                    playerNum = Overload.NetworkManager.m_Players.Count - 1;
                }
                else if (playerNum >= Overload.NetworkManager.m_Players.Count)
                {
                    playerNum = -1;
                }

                if (playerNum == -1 || !Overload.NetworkManager.m_Players[playerNum].m_spectator)
                {
                    break;
                }
            }

            SetObservedPlayer(playerNum == -1 ? null : Overload.NetworkManager.m_Players[playerNum]);
        }
    }

    // detect "observer" cheat code
    [HarmonyPatch(typeof(PlayerShip))]
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
    [HarmonyPatch(typeof(GameplayManager), "CreateNewGame")]
    class MPObserverReset
    {
        static void Prefix()
        {
            GameplayManager.m_use_segment_visibility = true;
            MPObserver.Enabled = false;
            MPObserver.ObservedPlayer = null;
            MPObserver.ThirdPerson = false;
            MPObserver.SavedPosition = Vector3.zero;
            MPObserver.SavedRotation = Quaternion.identity;
            MPObserver.LastRotation = Quaternion.identity;
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

    // Modify level / settings for observer. Need to wait for OnMatchStart to be sure m_spectator is set
    [HarmonyPatch(typeof(Client), "OnMatchStart")]
    class MPObserverModifyLevel
    {
        static void Postfix()
        {
            //Debug.Log("OnMatchStart player " + GameManager.m_local_player.m_mp_name + " observer " + GameManager.m_local_player.m_spectator);
            if (GameplayManager.IsDedicatedServer() || !GameManager.m_local_player.m_spectator)
                return;
            MPObserver.Enable();
        }
    }

    [HarmonyPatch(typeof(Server), "AllConnectionsHavePlayerReadyForCountdown")]
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

    [HarmonyPatch(typeof(PlayerShip), "Update")]
    class MPObserverFollowPlayer
    {
        static void Postfix(PlayerShip __instance)
        {
            if (MPObserver.Enabled && MPObserver.ObservedPlayer != null && __instance.isLocalPlayer)
            {
                var player = MPObserver.ObservedPlayer;
                __instance.c_transform.position = (__instance.c_transform.position + player.transform.position) / 2;

                if (player.c_player_ship.m_dead || player.c_player_ship.m_dying)
                {
                    __instance.c_transform.position -= MPObserver.LastRotation * (Vector3.forward * 2);
                    __instance.c_transform.rotation = MPObserver.LastRotation;

                    MPObserver.SetPlayerVisibility(player, true);
                }
                else
                {
                    MPObserver.LastRotation = __instance.c_transform.rotation = Quaternion.Lerp(__instance.c_transform.rotation, MPObserver.ObservedPlayer.transform.rotation, 0.5f);

                    if (MPObserver.ThirdPerson)
                    {
                        __instance.c_transform.position -= __instance.c_transform.rotation * new Vector3(0, -0.5f, 2f);
                    }

                    MPObserver.SetPlayerVisibility(player, MPObserver.ThirdPerson);
                }
            }
        }
    }

    // Remove very slow turning with observer (spectator) mode
    [HarmonyPatch(typeof(PlayerShip), "FixedUpdateProcessControlsInternal")]
    class MPObserverFixedUpdateProcess
    {
        static bool Prefix(PlayerShip __instance)
        {
            return !(MPObserver.Enabled && MPObserver.ObservedPlayer != null);
        }

        static IEnumerable<CodeInstruction> Transpiler(ILGenerator ilGen, IEnumerable<CodeInstruction> instructions)
        {
            var playerShip_c_player_Field = AccessTools.Field(typeof(PlayerShip), "c_player");
            var player_m_spectator_Field = AccessTools.Field(typeof(Player), "m_spectator");

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
                        new CodeInstruction(OpCodes.Ldfld, playerShip_c_player_Field),
                        new CodeInstruction(OpCodes.Ldfld, player_m_spectator_Field),
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
        private static MethodInfo _UIElement_getAlphaEyeGaze_Method = typeof(UIElement).GetMethod("getAlphaEyeGaze", BindingFlags.NonPublic | BindingFlags.Instance);
        static float getAlphaEyeGaze(UIElement uie, string pos)
        {
            return (float)_UIElement_getAlphaEyeGaze_Method.Invoke(uie, new object[] { pos });
        }

        private static MethodInfo _UIElement_DrawMpScoreboardRaw_Method = typeof(UIElement).GetMethod("DrawMpScoreboardRaw", BindingFlags.NonPublic | BindingFlags.Instance);
        static void DrawMpScoreboardRaw(UIElement uie, Vector2 vector)
        {
            _UIElement_DrawMpScoreboardRaw_Method.Invoke(uie, new object[] { vector });
        }

        public static void DrawFullScreenEffects()
        {
            if (MPObserver.ObservedPlayer == null)
            {
                return;
            }
            PlayerShip player_ship = MPObserver.ObservedPlayer.c_player_ship;
            Vector2 position;
            position.x = 576f;
            position.y = 576f;
            if (UIManager.ui_bg_fade > 0f)
            {
                UIManager.DrawQuadUIInner(position, 15f, 15f, Color.black, UIManager.ui_bg_fade, 11, 0.8f);
                if (!UIManager.ui_blocker_active && UIManager.ui_bg_fade >= 1f)
                {
                    UIManager.ui_blocker_active = true;
                    GameManager.m_player_ship.c_bright_blocker_go.SetActive(true);
                    if (GameManager.m_mesh_container != null)
                    {
                        GameManager.m_mesh_container.SetActive(false);
                    }
                }
            }
            if (UIManager.ui_blocker_active && UIManager.ui_bg_fade < 1f)
            {
                UIManager.ui_blocker_active = false;
                GameManager.m_player_ship.c_bright_blocker_go.SetActive(false);
                if (GameManager.m_mesh_container != null)
                {
                    GameManager.m_mesh_container.SetActive(true);
                }
            }
            position.x = -576f;
            position.y = 576f;
            if (GameplayManager.GamePaused)
            {
                UIManager.menu_flash_fade = Mathf.Max(0f, UIManager.menu_flash_fade - RUtility.FRAMETIME_UI * 2f);
            }
            else
            {
                UIManager.menu_flash_fade = Mathf.Min(1f, UIManager.menu_flash_fade + RUtility.FRAMETIME_UI * 4f);
            }
            if (UIManager.menu_flash_fade > 0f && (!player_ship.m_dying || player_ship.m_player_died_due_to_timer))
            {
                if (player_ship.m_damage_flash_slow > 0f)
                {
                    float a = UIManager.menu_flash_fade * Mathf.Min(0.1f, player_ship.m_damage_flash_slow * 0.1f) + Mathf.Min(0.2f, player_ship.m_damage_flash_fast * 0.2f);
                    UIManager.DrawQuadUI(position, 15f, 15f, UIManager.m_col_damage, a, 11);
                }
                if (player_ship.m_pickup_flash > 0f)
                {
                    float a2 = UIManager.menu_flash_fade * Mathf.Clamp01(player_ship.m_pickup_flash * player_ship.m_pickup_flash) * 0.25f;
                    UIManager.DrawQuadUI(position, 15f, 15f, UIManager.m_col_white, a2, 11);
                }
                if (player_ship.m_energy_flash > 0f)
                {
                    float a3 = UIManager.menu_flash_fade * (player_ship.m_energy_flash - 0.2f) * (player_ship.m_energy_flash - 0.2f) * 0.15f;
                    UIManager.DrawQuadUI(position, 15f, 15f, UIManager.m_col_hi3, a3, 11);
                }
            }
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

            if (MPObserver.ObservedPlayer == null)
            {
                return;
            }

            var player = MPObserver.ObservedPlayer;
            var player_ship = player.c_player_ship;

            vector.x = UIManager.UI_RIGHT - 270f;
            vector.y = UIManager.UI_BOTTOM - 70f - 22f * 4;

            if (MPModPrivateData.AssistScoring) {
                vector.y += 22f;
            }
            uie.DrawStringSmall("NOW OBSERVING:", vector, 0.35f, StringOffset.LEFT, UIManager.m_col_hi3, uie.m_alpha, -1f);
            vector.y += 22f;
            uie.DrawStringSmall("KILLS:", vector, 0.35f, StringOffset.LEFT, UIManager.m_col_hi3, uie.m_alpha, -1f);
            if (MPModPrivateData.AssistScoring) {
                vector.y += 22f;
                uie.DrawStringSmall("ASSISTS:", vector, 0.35f, StringOffset.LEFT, UIManager.m_col_hi3, uie.m_alpha, -1f);
            }
            vector.y += 22f;
            uie.DrawStringSmall("DEATHS:", vector, 0.35f, StringOffset.LEFT, UIManager.m_col_hi3, uie.m_alpha, -1f);

            vector.x = UIManager.UI_RIGHT - 20f;
            vector.y = UIManager.UI_BOTTOM - 70f - 22f * 4;

            uie.DrawStringSmall(player.m_mp_name, vector, 0.35f, StringOffset.RIGHT, UIManager.m_col_hi3, uie.m_alpha, -1f);
            vector.y += 22f;
            uie.DrawDigitsVariable(vector, player.m_kills, 0.4f, StringOffset.RIGHT, UIManager.m_col_hi3, uie.m_alpha);
            vector.y += 22f;
            uie.DrawDigitsVariable(vector, player.m_assists, 0.4f, StringOffset.RIGHT, UIManager.m_col_hi3, uie.m_alpha);
            vector.y += 22f;
            uie.DrawDigitsVariable(vector, player.m_deaths, 0.4f, StringOffset.RIGHT, UIManager.m_col_hi3, uie.m_alpha);

            if (player_ship.c_player == MPObserver.ObservedPlayer && !MPObserver.ThirdPerson && !player_ship.m_dead && !player_ship.m_dying)
            {
                DrawFullScreenEffects();
            }
        }
    }

    // Handle input for observer mode. Note: Is there a better method to hook than this?
    [HarmonyPatch(typeof(Controls), "UpdateKey")]
    class MPObserverControlsUpdateKey
    {
        static bool Prefix(CCInput cc_type)
        {
            if (MPObserver.Enabled && GameplayManager.IsMultiplayerActive)
            {
                if (cc_type == CCInput.FIRE_FLARE)
                {
                    return false;
                }
            }
            return true;
        }

        static void Postfix(CCInput cc_type)
        {
            if (MPObserver.Enabled && GameplayManager.IsMultiplayerActive)
            {
                if (cc_type == CCInput.SWITCH_WEAPON && Controls.JustPressed(CCInput.SWITCH_WEAPON))
                {
                    MPObserver.SwitchObservedPlayer(false);
                }
                if (cc_type == CCInput.PREV_WEAPON && Controls.JustPressed(CCInput.PREV_WEAPON))
                {
                    MPObserver.SwitchObservedPlayer(true);
                }
                if (cc_type == CCInput.FIRE_WEAPON && Controls.JustPressed(CCInput.FIRE_WEAPON) && MPObserver.ObservedPlayer != null)
                {
                    MPObserver.SetObservedPlayer(null);
                }
                if (cc_type == CCInput.FIRE_MISSILE && Controls.JustPressed(CCInput.FIRE_MISSILE) && MPObserver.ObservedPlayer != null)
                {
                    MPObserver.ThirdPerson = !MPObserver.ThirdPerson;

                    MPObserver.SetPlayerVisibility(MPObserver.ObservedPlayer, MPObserver.ThirdPerson);
                    GameManager.m_viewer.SetDamageEffects(-999);
                }
                if (cc_type == CCInput.SWITCH_MISSILE && Controls.JustPressed(CCInput.SWITCH_MISSILE) && CTF.IsActive)
                {
                    var player = (from f in CTF.PlayerHasFlag
                                  join p in Overload.NetworkManager.m_Players on f.Key equals p.netId
                                  where p.m_mp_team == MpTeam.TEAM0
                                  select p).FirstOrDefault();

                    if (player == null)
                    {
                        GameplayManager.AddHUDMessage($"No {MPTeams.TeamName(MpTeam.TEAM0)} player is carrying a flag.");
                    }
                    else
                    {
                        MPObserver.SetObservedPlayer(player);
                    }
                }
                if (cc_type == CCInput.PREV_MISSILE && Controls.JustPressed(CCInput.PREV_MISSILE) && CTF.IsActive)
                {
                    var player = (from f in CTF.PlayerHasFlag
                                  join p in Overload.NetworkManager.m_Players on f.Key equals p.netId
                                  where p.m_mp_team == MpTeam.TEAM1
                                  select p).FirstOrDefault();

                    if (player == null)
                    {
                        GameplayManager.AddHUDMessage($"No {MPTeams.TeamName(MpTeam.TEAM1)} player is carrying a flag.");
                    }
                    else
                    {
                        MPObserver.SetObservedPlayer(player);
                    }
                }
                // MPSpawnExtensionVis binding
                if (cc_type == CCInput.SMASH_ATTACK && MPSpawnExtensionVis.visualizing && Controls.JustPressed(CCInput.SMASH_ATTACK))
                {
                    MPSpawnExtensionVis.TriggerSpawnToggle();
                }
            }
        }
    }

    // Handle what happens when a player leaves.
    [HarmonyPatch(typeof(Overload.NetworkManager), "RemovePlayer")]
    class MPObserverNetworkManagerRemovePlayer {
        static void Prefix(Player player)
        {
            if (player == MPObserver.ObservedPlayer)
            {
                MPObserver.SetObservedPlayer(null);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "DrawEffectMesh")]
    class MPObserverDisableEffectMeshForObservedPlayer
    {
        static bool Prefix(PlayerShip __instance)
        {
            return !(MPObserver.ObservedPlayer == __instance.c_player && !MPObserver.ThirdPerson && !__instance.m_dead && !__instance.m_dying);
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "RpcApplyDamage")]
    class MPObserverSetDamageEffects
    {
        static void Postfix(PlayerShip __instance, float hitpoints, float damage, float damage_scaled, float damage_min)
        {
            if (MPObserver.ObservedPlayer == __instance.c_player && !MPObserver.ThirdPerson && !__instance.m_dead && !__instance.m_dying)
            {
                // NOTE: This appears to be a bug with the base game... __instance.c_player.m_hitpoints is the value AFTER the damage taken, but shouldn't this be the value BEFORE the damage taken?
                float damageEffects = Mathf.Min(__instance.c_player.m_hitpoints, damage_scaled);
                GameManager.m_viewer.SetDamageEffects(damageEffects);
            }
        }
    }

    // Support for display health bars above players

    public class MPObserverDamageLog
    {
        public float dmg;
        public float timer;
    }

    public static class MPObserverDamage
    {
        public static Dictionary<Player, List<MPObserverDamageLog>> playerDamages = new Dictionary<Player, List<MPObserverDamageLog>>();
        public static void AddDamage(Player player, float dmg, float timer = 2f)
        {
            if (!MPObserver.Enabled)
            {
                return;
            }

            if (!playerDamages.ContainsKey(player))
            {
                playerDamages.Add(player, new List<MPObserverDamageLog> { new MPObserverDamageLog { dmg = dmg, timer = timer } });
            }
            else
            {
                playerDamages[player].Add(new MPObserverDamageLog { dmg = dmg, timer = timer });
            }
        }

        public static void OnSendDamage(NetworkMessage rawMsg)
        {
            var dmg = rawMsg.ReadMessage<SendDamageMessage>();
            var player = Overload.NetworkManager.m_Players.Find(p => p.netId == dmg.m_defender_id);
            if (!playerDamages.ContainsKey(player))
            {
                playerDamages.Add(player, new List<MPObserverDamageLog> { new MPObserverDamageLog { dmg = dmg.m_damage, timer = 2f } });
            }
            else
            {
                playerDamages[player].Add(new MPObserverDamageLog { dmg = dmg.m_damage, timer = 2f });
            }
        }
    }

    public class SendDamageMessage : MessageBase
    {
        public override void Serialize(NetworkWriter writer)
        {
            writer.Write((byte)0); // version
            writer.Write(m_attacker_id);
            writer.Write(m_defender_id);
            writer.Write(m_damage);
        }

        public override void Deserialize(NetworkReader reader)
        {
            var version = reader.ReadByte();
            m_attacker_id = reader.ReadNetworkId();
            m_defender_id = reader.ReadNetworkId();
            m_damage = reader.ReadSingle();
        }

        public NetworkInstanceId m_attacker_id;
        public NetworkInstanceId m_defender_id;
        public float m_damage;
    }

    // Process damage log dropoffs
    [HarmonyPatch(typeof(PlayerShip), "Update")]
    class MPObserver_PlayerShip_Update
    {
        static void Postfix(PlayerShip __instance)
        {
            if (!MPObserver.Enabled && !MPObserver.DamageNumbersEnabled)
                return;

            if (MPObserverDamage.playerDamages.ContainsKey(__instance.c_player))
            {
                foreach (var dmg in MPObserverDamage.playerDamages[__instance.c_player])
                {
                    dmg.timer -= RUtility.FRAMETIME_UI;
                }

                if (MPObserver.Enabled) {
                    MPObserverDamage.playerDamages[__instance.c_player].RemoveAll(x => x.timer < 0f);
                } else {
                    if (MPObserverDamage.playerDamages[__instance.c_player].Where(x => x.timer >= 0f).Count() == 0) {
                        MPObserverDamage.playerDamages[__instance.c_player].Clear();
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(UIManager), "DrawMultiplayerNames")]
    class MPObserver_UIManager_DrawMultiplayerName
    {

        // Preserve arrow display for teammates while they're in death roll/prior to respawn
        static void Postfix()
        {
            Vector2 zero = Vector2.zero;
            int count = Overload.NetworkManager.m_Players.Count;
            for (int i = 0; i < count; i++)
            {
                Player player = Overload.NetworkManager.m_Players[i];
                if (!player.m_spectator)
                {
                    if (player.m_mp_data.vis_fade == 0f && player.m_mp_team == GameManager.m_local_player.m_mp_team && player.m_mp_team != MpTeam.ANARCHY)
                    {
                        int quad_index2 = UIManager.m_quad_index;
                        zero.y = -80f / player.m_mp_data.dist;
                        UIManager.DrawMpPlayerName(player, zero);
                        UIManager.DrawMpPlayerArrow(player, zero);
                        UIManager.PreviousQuadsTransformPlayer(player, quad_index2);
                    }
                }
            }
        }

        static IEnumerable<CodeInstruction> Transpiler(ILGenerator ilGen, IEnumerable<CodeInstruction> instructions)
        {
            int state = 0;
            var codes = new List<CodeInstruction>(instructions);

            foreach (var code in codes)
            {
                if (state < 26)
                {
                    if (code.opcode == OpCodes.Ldloc_S && ((LocalBuilder)code.operand).LocalIndex == 5)
                    {
                        state++;
                    }
                }
                else if (state == 26)
                {
                    if (code.opcode == OpCodes.Ble_Un)
                    {
                        code.opcode = OpCodes.Blt_Un;
                        state++;
                    }
                }
                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(UIManager), "DrawMpPlayerArrow")]
    class MPObserver_UIManager_DrawMpPlayerArrow
    {
        static bool Prefix(Player player)
        {
            return player.m_mp_data.vis_fade != 0
                || (player.m_hitpoints <= 0f && player.m_mp_team == GameManager.m_local_player.m_mp_team && player.m_mp_team != MpTeam.ANARCHY);
        }
    }

    // Display health bar above players
    [HarmonyPatch(typeof(UIManager), "DrawMpPlayerName")]
    class MPObserver_UIManager_DrawMpPlayerName
    {
        static void Postfix(Player player, Vector2 offset)
        {
            float w = 3.5f;
            float h = MPObserver.Enabled ? 1f : 0.5f;
            if (MPObserver.Enabled || Menus.mms_team_health && player.m_mp_team == GameManager.m_local_player.m_mp_team && player.m_mp_team != MpTeam.ANARCHY)
            {
                offset.y -= 3f;
                Color c1 = Color.Lerp(HSBColor.ConvertToColor(0.4f, 0.85f, 0.1f), HSBColor.ConvertToColor(0.4f, 0.8f, 0.15f), UnityEngine.Random.value * UIElement.FLICKER);
                Color c3 = Color.Lerp(player.m_mp_data.color, UIManager.m_col_white2, player.c_player_ship.m_damage_flash_fast);
                if (MPObserver.Enabled)
                {
                    UIManager.DrawStringAlignCenter(player.m_hitpoints.ToString("n1"), offset + Vector2.up * -3f, 0.8f, c3);
                }
                UIManager.DrawQuadBarHorizontal(offset, w + 0.25f, h + 0.25f, 0f, HSBColor.ConvertToColor(0.4f, 0.1f, 0.1f), 7);
                float health = System.Math.Min(player.m_hitpoints, 100f) / 100f * w;
                offset.x = w - health;
                UIManager.DrawQuadUIInner(offset, health, h, c1, 1f, 11, 1f);
            }
            if (MPObserver.Enabled || (MPObserver.DamageNumbersEnabled && Menus.mms_client_damage_numbers))
            {
                if (MPObserverDamage.playerDamages.ContainsKey(player) && MPObserverDamage.playerDamages[player].Sum(x => x.dmg) > 0f)
                {
                    float dmg = MPObserverDamage.playerDamages[player].Sum(x => x.dmg);
                    Color c2 = Color.Lerp(HSBColor.ConvertToColor(0f, 1f, 0.90f), HSBColor.ConvertToColor(0f, 0.9f, 1f), UnityEngine.Random.value * UIElement.FLICKER);
                    if (MPObserver.Enabled)
                    {
                        float health = System.Math.Min(player.m_hitpoints, 100f) / 100f * w;
                        float dmgbar = System.Math.Max(0, System.Math.Min(100f - health, dmg / 100 * w));
                        offset.x -= health + dmgbar;
                        UIManager.DrawQuadUIInner(offset, dmgbar, h, c2, 1f, 11, 1f);
                    }
                    else
                    {
                        c2.a = player.m_mp_data.vis_fade;
                        UIManager.DrawStringAlignCenter(dmg.ToString("n0"), offset + Vector2.up * -3f, 1.5f, c2);
                    }
                }
            }
        }
    }

    // Add Observer damage record
    [HarmonyPatch(typeof(PlayerShip), "RpcApplyDamage")]
    class MPObserver_PlayerShip_RpcApplyDamage
    {
        static void Postfix(PlayerShip __instance, float hitpoints, float damage, float damage_scaled, float damage_min)
        {
            if (!MPObserver.Enabled)
                return;

            __instance.m_damage_flash_slow = 0f;
            MPObserverDamage.AddDamage(__instance.c_player, damage_scaled);
        }
    }

    // Skip if observer (MaybeFireWeapon does)
    [HarmonyPatch(typeof(PlayerShip), "MaybeFireMissile")]
    class MPObserver_PlayerShip_MaybeFireMissile
    {
        static bool Prefix(PlayerShip __instance)
        {
            if (__instance.c_player.m_spectator)
                return false;

            return true;
        }
    }

    // Observers shouldn't count against "head to head" calc - short enough method to not bother transpiling
    [HarmonyPatch(typeof(NetworkMatch), "SetMode")]
    class MPObserver_NetworkMatch_SetMode
    {
        static bool Prefix(MatchMode mode, ref MatchMode ___m_match_mode)
        {
            ___m_match_mode = mode;
            if (mode == MatchMode.ANARCHY)
            {
                NetworkMatch.m_head_to_head = (NetworkMatch.m_players.Count(x => !x.Value.m_name.StartsWith("OBSERVER")) < 3);
            }
            else
            {
                NetworkMatch.m_head_to_head = false;
            }
            return false;
        }
    }
}
