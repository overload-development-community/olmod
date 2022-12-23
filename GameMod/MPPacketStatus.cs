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

        // calculated status for input and output packets (just lossiness for now)
        public static int InStatus = 0;
        public static int OutStatus = 0;

        // time to next status update in seconds
        public static float shortUpdate = 0.25f; // ping timeout period from NetworkManager
        public static float nextUpdate = 2f;

        public static int[] inStats = new int[5] { 100, 100, 100, 100, 100 };
        public static int[] outStats = new int[5] { 100, 100, 100, 100, 100 };

        private static int inCount = 0;

        private static int inErr = 0; 
        private static int outErr = 0;

        private static int idx = 0;

        private static NetworkClient client => Client.GetClient();
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
                    outErr = Math.Max(Math.Max(loss, drop), outErr);

                    shortUpdate = Time.unscaledTime + 0.25f;
                }
                if (Time.unscaledTime >= nextUpdate)
                {
                    inCount = NetworkTransport.GetIncomingPacketCount(connection.hostId, connection.connectionId, out _) - inCount;
                    //int loss2 = NetworkTransport.GetIncomingPacketDropCountForAllHosts();
                    //int drop2 = NetworkTransport.GetIncomingPacketLossCount(connection.hostId, connection.connectionId, out lastErr);
                    //inErr = loss2 + drop2 - inErr;
                    inErr = NetworkTransport.GetIncomingPacketLossCount(connection.hostId, connection.connectionId, out _) - inErr;

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

                    idx = (idx + 1) % 5;
                    nextUpdate = Time.unscaledTime + 2f;
                }
            }
            else if (running)
            {
                InColor = UIManager.m_col_good_ping;
                OutColor = UIManager.m_col_good_ping;
                inStats = new int[5];
                inStats = new int[5];
                inCount = 0;
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
            pos.x += 70f;
            __instance.DrawStringSmall(100 - MPPacketStatus.InStatus + "% in", pos, 0.4f, StringOffset.LEFT, MPPacketStatus.InColor, 1f);
            pos.x += 70f;
            __instance.DrawStringSmall(100 - MPPacketStatus.OutStatus + "% out", pos, 0.4f, StringOffset.LEFT, MPPacketStatus.OutColor, 1f);
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
                    yield return new CodeInstruction(OpCodes.Nop);
                    yield return new CodeInstruction(OpCodes.Br, jump2);
                }
                yield return code;
            }
        }
    }

    
}