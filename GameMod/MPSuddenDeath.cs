using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using Harmony;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod
{
    class MPSuddenDeath
    {
        public static bool SuddenDeathEnabled = true; // TODO: Make this an option.
        public static bool InOvertime = false;

        public static int GetTimer()
        {
            if (NetworkMatch.m_match_time_limit_seconds == int.MaxValue || !SuddenDeathEnabled)
            {
                return NetworkMatch.m_match_time_remaining;
            }

            return Math.Abs(NetworkMatch.m_match_time_limit_seconds - (int)NetworkMatch.m_match_elapsed_seconds);
        }
    }

    public class SuddenDeathCustomMsg
    {
        public const short MsgSuddenDeath = 126;
    }

    public class SuddenDeathMessage : MessageBase
    {
        public override void Serialize(NetworkWriter writer)
        {
            writer.Write(m_message);
        }
        public override void Deserialize(NetworkReader reader)
        {
            m_message = reader.ReadString();
        }

        public string m_message;
    }

    [HarmonyPatch(typeof(NetworkMatch), "InitBeforeEachMatch")]
    class SuddenDeathInitBeforeEachMatch
    {
        private static void Postfix()
        {
            MPSuddenDeath.InOvertime = false;
        }
    }
    
    [HarmonyPatch(typeof(NetworkMatch), "MaybeEndTimer")]
    class MaybeEndTimer
    {
        public static bool Prefix()
        {
            if (
                NetworkMatch.m_match_elapsed_seconds > NetworkMatch.m_match_time_limit_seconds
                && (NetworkMatch.GetMode() == MatchMode.MONSTERBALL || NetworkMatch.GetMode() == CTF.MatchModeCTF)
                && MPSuddenDeath.SuddenDeathEnabled
                && NetworkMatch.m_team_scores[(int)MpTeam.TEAM0] == NetworkMatch.m_team_scores[(int)MpTeam.TEAM1]
            )
            {
                if (!MPSuddenDeath.InOvertime)
                {
                    Debug.Log("Sudden Death!");
                    MPSuddenDeath.InOvertime = true;
                    NetworkServer.SendToAll(SuddenDeathCustomMsg.MsgSuddenDeath, new SuddenDeathMessage
                    {
                        m_message = "Sudden Death!"
                    });

                }
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Client), "RegisterHandlers")]
    class SuddenDeathClientHandlers
    {
        private static void OnSuddenDeathNotify(NetworkMessage rawMsg)
        {
            var msg = rawMsg.ReadMessage<SuddenDeathMessage>();

            if (msg.m_message != null)
                GameplayManager.AddHUDMessage(msg.m_message, -1, true);
        }

        static void Postfix()
        {
            if (Client.GetClient() != null)
            {
                Client.GetClient().RegisterHandler(SuddenDeathCustomMsg.MsgSuddenDeath, OnSuddenDeathNotify);
            }
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawHUDScoreInfo")]
    class DrawHUDScoreInfo
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool found = false;
            foreach (var code in instructions)
            {
                if (!found && code.opcode == OpCodes.Ldloc_S && ((LocalBuilder)code.operand).LocalIndex == 5)
                {
                    found = true;

                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPSuddenDeath), "GetTimer"));

                    continue;
                }

                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawMpMiniScoreboard")]
    class DrawMpMiniScoreboard
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool found = false;
            foreach (var code in instructions)
            {
                if (!found && code.opcode == OpCodes.Ldloc_1)
                {
                    found = true;

                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPSuddenDeath), "GetTimer"));

                    continue;
                }

                yield return code;
            }
        }
    }
}
