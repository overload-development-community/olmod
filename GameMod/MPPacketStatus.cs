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
        // do yourself a favour and make sure that holdTime is a multiple of sampleTime
        public const int sampleTime = 1; // sample window size in seconds
        public const int holdTime = 5; // length of time to hold a visible loss value on-screen

        public const int holdSize = holdTime / sampleTime; // array size for the stats arrays

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
        public static float nextUpdate = sampleTime;

        public static int[] inStats = new int[holdSize];
        public static int[] outStats = new int[holdSize];

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
                    //int loss = NetworkTransport.GetOutgoingPacketNetworkLossPercent(connection.hostId, connection.connectionId, out _);
                    //int drop = NetworkTransport.GetOutgoingPacketOverflowLossPercent(connection.hostId, connection.connectionId, out _);
                    //outErr = Math.Max(loss + drop, outErr);  // this one keeps the max 1/4-second recorded value
                    //outErr += loss + drop;  // this one is for the average over the sampleTime
                    outErr += NetworkTransport.GetOutgoingPacketNetworkLossPercent(connection.hostId, connection.connectionId, out _);

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

                    inStats[idx] = 100 - (int)(100 - (100f * inErr / inCount)); // rounding up, weeee
                    //outStats[idx] = outErr; // this one is for the max 1/4-second recorded value
                    outStats[idx] = 100 - (int)(100 - (outErr / (4f * sampleTime))); // this one is for the average over the sampleTime. Also rounding up.

                    outErr = 0;
                    InStatus = 0;
                    OutStatus = 0;

                    for (int i = 0; i < holdSize; i++)
                    {
                        InStatus = Math.Max(InStatus, inStats[i]);
                        OutStatus = Math.Max(OutStatus, outStats[i]);
                    }

                    InColor = Color.Lerp(UIManager.m_col_good_ping, UIManager.m_col_em5, InStatus / 20f); // 20f since anything over 0% is bad, if you're at 20% it's terrible
                    OutColor = Color.Lerp(UIManager.m_col_good_ping, UIManager.m_col_em5, OutStatus / 20f);

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

                    idx = (idx + 1) % holdSize;
                    nextUpdate = Time.unscaledTime + sampleTime;
                }
            }
            else if (running) // reset everything since we're not connected anymore but we *were* running
            {
                Debug.Log("Maximum packet loss seen during a sample window last round (" + sampleTime + "-second window): " + InMin + "% in, " + OutMin + "% out");
                InColor = UIManager.m_col_good_ping;
                OutColor = UIManager.m_col_good_ping;
                InColorMin = UIManager.m_col_good_ping;
                OutColorMin = UIManager.m_col_good_ping;
                InMin = 0;
                OutMin = 0;
                inStats = new int[holdSize];
                inStats = new int[holdSize];
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

    [HarmonyPatch(typeof(Client), "FixedUpdate")]
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
            //__instance.DrawStringSmall(100 - MPPacketStatus.InStatus + "% in", pos, 0.4f, StringOffset.LEFT, MPPacketStatus.InColor, 1f);  // uncomment this to show packet consistency % instead of loss %
            __instance.DrawStringSmall(MPPacketStatus.InStatus + "% in", pos, 0.4f, StringOffset.LEFT, MPPacketStatus.InColor, 1f);
            pos.x += 65f;
            //__instance.DrawStringSmall(100 - MPPacketStatus.OutStatus + "% out", pos, 0.4f, StringOffset.LEFT, MPPacketStatus.OutColor, 1f);  // uncomment this to show packet consistency % instead of loss %
            __instance.DrawStringSmall(MPPacketStatus.OutStatus + "% out", pos, 0.4f, StringOffset.LEFT, MPPacketStatus.OutColor, 1f);
            pos.y -= 15f;
            pos.x -= 65f;
            __instance.DrawStringSmall("Packet loss (" + MPPacketStatus.holdTime + "s max):", pos, 0.3f, StringOffset.LEFT, UIManager.m_col_good_ping, 0.5f);
            /* // This displays the worst values recorded so far in the round above the current values.
            pos.x -= 155f;
            __instance.DrawStringSmall("Minimums:", pos, 0.4f, StringOffset.LEFT, UIManager.m_col_good_ping, 0.5f);
            pos.x += 90f;
            __instance.DrawStringSmall(100 - MPPacketStatus.InMin + "% in", pos, 0.4f, StringOffset.LEFT, MPPacketStatus.InColorMin, 0.5f);
            pos.x += 65f;
            __instance.DrawStringSmall(100 - MPPacketStatus.OutMin + "% out", pos, 0.4f, StringOffset.LEFT, MPPacketStatus.OutColorMin, 0.5f);
            */
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

    // Adds ping and packet loss indication onto the death page
    [HarmonyPatch(typeof(UIElement), "DrawMpDeathOverlay")]
    public static class MPPacketStatus_UIElement_DrawMpDeathOverlay
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                yield return code;

                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(UIElement), "DrawRecentKillsMP"))
                {
                    // pos.x = 430f;
                    yield return new CodeInstruction(OpCodes.Ldloca_S, 1);
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 430f);
                    yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(Vector2), "x"));

                    // pos.y = -270f;
                    yield return new CodeInstruction(OpCodes.Ldloca_S, 1);
                    yield return new CodeInstruction(OpCodes.Ldc_R4, -270f);
                    yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(Vector2), "y"));

                    // DrawPing();
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldloc_1);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UIElement), "DrawPing"));
                }
            }
        }
    }
}