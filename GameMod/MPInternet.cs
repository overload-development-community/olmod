using Harmony;
using Overload;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace GameMod
{
    class MPInternet
    {
        public static bool Enabled;
        public static IPAddress ServerAddress;
        public static IPAddress FindPasswordAddress(string password, out string msg)
        {
            msg = null;
            var i = password.IndexOf('_'); // allow password suffix with '_'
            string name = i == -1 ? password : password.Substring(0, i);
            if (!name.Contains('.'))
            {
                msg = "Invalid IP/server name";
                return null;
            }
            if (new Regex(@"\d{1,3}([.]\d{1,3}){3}").IsMatch(name) &&
                IPAddress.TryParse(name, out IPAddress adr))
                return adr;
            try
            {
                var adrs = Dns.GetHostAddresses(name); // async is better but would need moving bottom of NetworkMatch.SwitchToLobbyMenu to NetSystemDoTick
                return adrs == null || adrs.Length == 0 ? null : adrs[0];
            }
            catch (SocketException ex)
            {
                msg = "Cannot find " + name + ": " + ex.Message;
            }
            catch (Exception ex)
            {
                msg = "Error looking up " + name + ": " + ex.Message;
            }
            return null;
        }
        public static string ClientModeName()
        {
            return Enabled ? "INTERNET MATCH" : "LAN MATCH";
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawMpMenu")]
    class MPInternetMainDraw
    {
        private static void DrawItem(UIElement uie, ref Vector2 position)
        {
            uie.SelectAndDrawItem(Loc.LS("INTERNET MATCH"), position, 4, false, 1f, 0.75f);
            position.y += 62f;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldstr && (string)code.operand == "CUSTOMIZE")
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldloca, 0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPInternetMainDraw), "DrawItem"));
                }
                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(MenuManager), "MpMenuUpdate")]
    class MPInternetMainUpdate
    {
        private static void Prefix()
        {
            MPInternet.Enabled = false;
        }

        private static void Postfix()
        {
            if (MenuManager.m_menu_sub_state == MenuSubState.ACTIVE && UIManager.PushedSelect(100))
            {
                if (UIManager.m_menu_selection == 4)
                {
                    Debug.Log("MpMenuUpdate - pushed internet");
                    MPInternet.Enabled = true;
                    MenuManager.m_mp_lan_match = true;
                    MenuManager.m_mp_private_match = true;
                    UIManager.DestroyAll(false);
                    MenuManager.ChangeMenuState(MenuState.MP_LOCAL_MATCH, false);
                    NetworkMatch.SetNetworkGameClientMode(NetworkMatch.NetworkGameClientMode.LocalLAN);
                    MenuManager.PlaySelectSound(1f);
                }
                else
                {
                    Debug.Log("MpMenuUpdate - pushed non internet");
                    MPInternet.Enabled = false;
                }
            }
        }
    }

    [HarmonyPatch(typeof(MenuManager), "MpMatchSetup")]
    class MPInternetMatchSetup
    {
        private static IEnumerable<CodeInstruction> Transpiler(ILGenerator ilGen, IEnumerable<CodeInstruction> codes)
        {
            int state = 0; // 0 = before Start/JoinPrivLob, 1 = before call DestroyAll, 2 = just after call DestroyAll
            string name;
            Label l = ilGen.DefineLabel();
            foreach (var code in codes)
            {
                if (state == 2)
                {
                    code.labels.Add(l);
                    l = ilGen.DefineLabel();
                    state = 0;
                }
                else if (code.opcode == OpCodes.Call)
                {
                    if (state == 0 && (((name = ((MemberInfo)code.operand).Name) == "StartPrivateLobby") || name == "JoinPrivateLobby"))
                    {
                        yield return code;
                        yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MenuManager), "m_menu_state"));
                        yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MenuManager), "m_next_menu_state"));
                        yield return new CodeInstruction(OpCodes.Beq, l);
                        state = 1;
                        continue;
                    }
                    else if (state == 1)
                    {
                        state = 2;
                    }
                }
                if (code.opcode == OpCodes.Ldstr && (string)code.operand == "LAN MATCH")
                {
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPInternet), "ClientModeName"));
                    continue;
                }
                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "SwitchToLobbyMenu")]
    class MPInternetStart
    {
        private static bool Prefix()
        {
            if (!MPInternet.Enabled)
                return true;
            string pwd = NetworkMatch.m_match_req_password;
            if (pwd == "")
            {
                var pmd = (PrivateMatchDataMessage)typeof(NetworkMatch).GetField("m_private_data", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                pwd = pmd != null ? pmd.m_password : "";
            }
            MPInternet.ServerAddress = MPInternet.FindPasswordAddress(pwd.Trim(), out string msg);
            if (MPInternet.ServerAddress == null)
            {
                Debug.Log("SwitchToLobbyMenu failed " + msg);
                NetworkMatch.CreateGeneralUIPopup("INTERNET MATCH", msg, 1);
                //MenuManager.ChangeMenuState(MenuState.MP_LOCAL_MATCH, true);
                /*
                MenuManager.m_menu_state = MenuState.MP_LOCAL_MATCH;
                MenuManager.m_next_menu_state = MenuManager.m_menu_state;
                UIManager.CreateUIElement(UIManager.SCREEN_CENTER, 7000, UIElementType.MP_MATCH_SETUP, Loc.LS("INTERNET MATCH"));
                MenuManager.m_menu_sub_state = MenuSubState.ACTIVE;
                MenuManager.m_menu_micro_state = NetworkMatch.m_match_req_password == "" ? 4 : 1;
                */
                return false;
            }
            MenuManager.m_mp_status = NetworkMatch.m_match_req_password == "" ? Loc.LS("CREATING INTERNET MATCH") : Loc.LS("JOINING INTERNET MATCH");
            return true;
        }
    }

    [HarmonyPatch(typeof(BroadcastState), "EnumerateNetworkInterfaces")]
    class MPInternetEnum
    {
        private static bool Prefix(Action<object> callback)
        {
            if (!MPInternet.Enabled)
                return true;
            /*
            var IntfType = typeof(BroadcastState).Assembly.GetType("Overload.EnumeratedNetworkInterface");
            object intf = Activator.CreateInstance(IntfType);
            IntfType.GetField("ipAddress").SetValue(intf, IPAddress.Any);
            IntfType.GetField("netMaskAddress").SetValue(intf, new IPAddress(~MPInternet.ServerAddress.Address));
            IntfType.GetField("isUp").SetValue(intf, true);
            IntfType.GetField("hasBroadcast").SetValue(intf, true);
            IntfType.GetField("isLoopback").SetValue(intf, false);
            callback(intf);
            */
            return false;
        }
    }
    
    [HarmonyPatch(typeof(BroadcastState), MethodType.Constructor, new [] { typeof(int), typeof(int), typeof(IBroadcastStateReceiver) })]
    class MPInternetState
    {
        /*
        public static int ListenPortMod(int value)
        {
            if (!MPInternet.Enabled)
                return value;
            return 8001; // Exception if olproxy is running...
        }
        */
        
        public static bool InitReceiveClient(BroadcastState bs)
        {
            if (!MPInternet.Enabled)
                return false;
            var ep = new IPEndPoint(MPInternet.ServerAddress, 8001);
            var client = new UdpClient();
            client.Connect(ep);
            typeof(BroadcastState).GetField("m_receiveClient", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(bs, client);

            var IntfType = typeof(BroadcastState).Assembly.GetType("Overload.BroadcastState").GetNestedType("InterfaceState", BindingFlags.NonPublic);
            object intf = Activator.CreateInstance(IntfType, new object[] { client, ep.Address, null, new IPAddress(-1) });
            var intfs = typeof(BroadcastState).GetField("m_interfaces", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(bs);
            intfs.GetType().GetMethod("Add").Invoke(intfs, new object[] { intf });
            /*
            ICollection intfs = typeof(BroadcastState).GetField("m_interfaces", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(bs) as ICollection;
            foreach (var intf in intfs) {
                var client = intf.GetType().GetField("m_client").GetValue(intf);
                typeof(BroadcastState).GetField("m_receiveClient", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(bs, client);
                break;
            }
            */
            return true;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0; // 0 = before 1st LogFormat, 1 = before 2nd LogFormat, 2 = before rc Bind, 3 = after rc Bind, 4 = after Bind label
            Label initRCLabel = new Label();
            foreach (var code in codes) //Trans.FieldReadModifier("listenPort", AccessTools.Method(typeof(MPInternetState), "ListenPortMod"), codes))
            {
                //Debug.Log("state " + state + " op " + code.opcode + " " + code.operand);
                if (state <= 1 && code.opcode == OpCodes.Call && ((MemberInfo)code.operand).Name == "LogFormat" && ++state == 2)
                {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPInternetState), "InitReceiveClient"));
                    yield return new CodeInstruction(OpCodes.Brtrue, initRCLabel);
                    continue;
                }
                else if (state == 2 && code.opcode == OpCodes.Callvirt && ((MemberInfo)code.operand).Name == "Bind")
                {
                    state = 3;
                }
                else if (state == 3)
                {
                    code.labels.Add(initRCLabel);
                    state = 4;
                }
                yield return code;
            }
        }
    }

    /*
    [HarmonyPatch]
    class MPInternetEnumCB
    {
        static MethodBase TargetMethod()
        {
            foreach (var x in typeof(BroadcastState).GetNestedTypes(BindingFlags.NonPublic))
                if (x.Name.Contains("c__AnonStorey0"))
                {
                    var m = AccessTools.Method(x, "<>m__0");
                    if (m != null)
                    {
                        Debug.Log("MPInternetEnumCB TargetMethod found");
                        return m;
                    }
                }
            Debug.Log("MPInternetEnumCB TargetMethod not found");
            return null;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            return Trans.FieldReadModifier("listenPort", AccessTools.Method(typeof(MPInternetState), "ListenPortMod"), codes);
        }
    }
    */

    /*
    [HarmonyPatch]
    class MPInternetRecvInfo
    {
        static MethodBase TargetMethod()
        {
            return typeof(BroadcastState).GetNestedType("ReceivedPacketInfo", BindingFlags.NonPublic).GetConstructor(new [] { typeof(bool), typeof(IPEndPoint), typeof(byte[]) });
        }
        
        private static void Prefix()
        {
            Debug.Log("Received packet");
        }
    }
    */

    [HarmonyPatch(typeof(LocalLANClient), "DoTick")]
    class MPInternetConnInfo
    {
        public static MatchmakerConnectionInfo ConnInfoMod(MatchmakerConnectionInfo connInfo)
        {
            if (MPInternet.Enabled)
                connInfo.IPAddress = MPInternet.ServerAddress.ToString();
            return connInfo;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in Trans.FieldReadModifier("listenPort", AccessTools.Method(typeof(MPInternetState), "ListenPortMod"), codes))
            {
                if (code.opcode == OpCodes.Stfld && ((FieldInfo)code.operand).Name == "GameSessionConnectionInfo")
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPInternetConnInfo), "ConnInfoMod"));
                yield return code;
            }
        }
    }
}
