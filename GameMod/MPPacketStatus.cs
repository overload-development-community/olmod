using HarmonyLib;
using Overload;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod
{
    public static class MPPacketStatus
    {
        public static Color InColor = UIManager.m_col_good_ping;
        public static Color OutColor = UIManager.m_col_good_ping;

        public static Color InColorMin = UIManager.m_col_good_ping;
        public static Color OutColorMin = UIManager.m_col_good_ping;

        // calculated status for input and output packets (just lossiness for now)
        public static int InStatus = 0;
        public static int OutStatus = 0;

        // round minimums seen
        public static int InMin = 0;
        public static int OutMin = 0;

        // time to next status update in seconds
        public static float shortUpdate = 0.25f; // ping timeout period from NetworkManager
        public static float nextUpdate = 2f;

        public static int[] inStats = new int[5];
        public static int[] outStats = new int[5];

        private static int inTotal = 0; // Total number of inbound packets
        private static int inCount = 0; // Number of inbound packets in the last sample period

        private static int inErrTotal = 0; // Total number of inbound packets dropped
        private static int inErr = 0; // Number of inbound packets dropped in the last sample period
        private static int outErr = 0; // *Percentage* of outbound packets dropped or lost because Unity can provide it directly

        private static int idx = 0;

        //private static NetworkClient client => Client.GetClient();
        private static NetworkConnection connection => Client.GetConnection();

        //private static byte lastErr = 0;
        private static bool running = true;


        public static void UpdateStatus()
        {
            if (!GameplayManager.IsDedicatedServer() && GameplayManager.IsMultiplayerActive && Client.IsConnected())
            {
                running = true;
                if (Time.unscaledTime >= shortUpdate)
                {
                    int loss = NetworkTransport.GetOutgoingPacketNetworkLossPercent(connection.hostId, connection.connectionId, out _);
                    int drop = NetworkTransport.GetOutgoingPacketOverflowLossPercent(connection.hostId, connection.connectionId, out _);
                    outErr = Math.Max(loss + drop, outErr);

                    shortUpdate = Time.unscaledTime + 0.25f;
                }
                if (Time.unscaledTime >= nextUpdate)
                {
                    int inTotalNew = NetworkTransport.GetIncomingPacketCount(connection.hostId, connection.connectionId, out _);
                    inCount = inTotalNew - inTotal;
                    inTotal = inTotalNew;

                    int inErrNew = NetworkTransport.GetIncomingPacketLossCount(connection.hostId, connection.connectionId, out _);
                    inErr = inErrNew - inErrTotal;
                    inErrTotal = inErrNew;

                    inStats[idx] = 100 - (int)(100 - ((100f * inErr) / inCount)); // rounding, weeee
                    outStats[idx] = outErr;

                    outErr = 0;
                    InStatus = 0;
                    OutStatus = 0;

                    for (int i = 0; i < 5; i++)
                    {
                        InStatus = Math.Max(InStatus, inStats[i]);
                        OutStatus = Math.Max(OutStatus, outStats[i]);
                    }

                    InColor = Color.Lerp(UIManager.m_col_good_ping, UIManager.m_col_em5, InStatus / 10f); // 10f since anything over 0 is bad, if you're at 10% it's terrible
                    OutColor = Color.Lerp(UIManager.m_col_good_ping, UIManager.m_col_em5, OutStatus / 10f);

                    if (InStatus > InMin)
                    {
                        InMin = InStatus;
                        InColorMin = InColor;
                    }
                    if (OutStatus > OutMin)
                    {
                        OutMin = OutStatus;
                        OutColorMin = OutColor;
                    }

                    idx = (idx + 1) % 5;
                    nextUpdate = Time.unscaledTime + 2f;
                }
            }
            else if (running)
            {
                Debug.Log("Maximum packet loss seen during a sample window last round (2 seconds): " + InMin + "% in, " + OutMin + "% out");
                InColor = UIManager.m_col_good_ping;
                OutColor = UIManager.m_col_good_ping;
                InColorMin = UIManager.m_col_good_ping;
                OutColorMin = UIManager.m_col_good_ping;
                InMin = 0;
                OutMin = 0;
                inStats = new int[5];
                inStats = new int[5];
                inTotal = 0;
                inCount = 0;
                inErrTotal = 0;
                inErr = 0;
                outErr = 0;
                idx = 0;
                running = false;
            }
        }
    }

    [HarmonyPatch(typeof(Client), "Update")]
    class MPPacketStatus_Client_Update
    {
        static void Postfix()
        {
            MPPacketStatus.UpdateStatus();
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawPing")]
    class MPPacketStatus_UIElement_DrawPing
    {
        static void Postfix(Vector2 pos, UIElement __instance)
        {
            pos.x += 65f;
            __instance.DrawStringSmall(100 - MPPacketStatus.InStatus + "% in", pos, 0.4f, StringOffset.LEFT, MPPacketStatus.InColor, 1f);
            pos.x += 65f;
            __instance.DrawStringSmall(100 - MPPacketStatus.OutStatus + "% out", pos, 0.4f, StringOffset.LEFT, MPPacketStatus.OutColor, 1f);
            pos.y -= 16f;
            pos.x -= 155f;
            __instance.DrawStringSmall("Minimums:", pos, 0.4f, StringOffset.LEFT, UIManager.m_col_good_ping, 0.5f);
            pos.x += 90f;
            __instance.DrawStringSmall(100 - MPPacketStatus.InMin + "% in", pos, 0.4f, StringOffset.LEFT, MPPacketStatus.InColorMin, 0.5f);
            pos.x += 65f;
            __instance.DrawStringSmall(100 - MPPacketStatus.OutMin + "% out", pos, 0.4f, StringOffset.LEFT, MPPacketStatus.OutColorMin, 0.5f);
        }
    }

    // gets rid of the XP indicator in multiplayer to make room for the network health display
    [HarmonyPatch(typeof(UIElement), "DrawHUD")]
    public static class MPPacketStatus_UIElement_DrawHUD
    {
        static IEnumerable<CodeInstruction> Transpiler(ILGenerator ilGen, IEnumerable<CodeInstruction> codes)
        {
            Label jump1 = ilGen.DefineLabel();
            Label jump2 = ilGen.DefineLabel();
            int flagged = 0;

            foreach (var code in codes)
            {
                if (flagged == 1)
                {
                    flagged = 2;
                    code.labels.Add(jump2);
                }
                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(UIElement), "DrawXPTotalSmall"))
                {
                    code.labels.Add(jump1);
                    flagged = 1;

                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(GameplayManager), "IsMultiplayerActive"));
                    yield return new CodeInstruction(OpCodes.Brfalse, jump1);
                    yield return new CodeInstruction(OpCodes.Pop);
                    yield return new CodeInstruction(OpCodes.Pop);
                    //yield return new CodeInstruction(OpCodes.Nop);
                    yield return new CodeInstruction(OpCodes.Br, jump2);
                }
                yield return code;
            }
        }
    }
}