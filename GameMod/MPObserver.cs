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
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int n = 0;
            var codes = new List<CodeInstruction>(instructions);
            for (var i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldfld && (codes[i].operand as FieldInfo).Name == "m_spectator" &&
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
}
