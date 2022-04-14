using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using Overload;
using UnityEngine;

namespace GameMod {
    class MPInternet
    {
        public static bool OldEnabled; // this is only used on the old non-internet-match builds!
        public static bool ServerEnabled;
        public static IPAddress ServerAddress;
        public static IPAddress FindPasswordAddress(string password, out string msg)
        {
            msg = null;
            var i = password.IndexOf('_'); // allow password suffix with '_'
            string name = i == -1 ? password : password.Substring(0, i);
            /* Allow a host name without any dots...
            if (!name.Contains('.'))
            {
                msg = "Invalid IP/server name";
                return null;
            }
            */
            if (new Regex(@"\d{1,3}([.]\d{1,3}){3}").IsMatch(name) &&
                IPAddress.TryParse(name, out IPAddress adr))
                return adr;
            try
            {
                var adrs = ResolveIPAddress(name);
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
        public static IPAddress[] ResolveIPAddress(string name)
        {
            bool foundV4 = false;
            IPAddress[] addrs = Dns.GetHostAddresses(name); // async is better but would need moving bottom of NetworkMatch.SwitchToLobbyMenu to NetSystemDoTick
            // the game doesn't like IPv6,
            // so for now, try to find a V4 address if there is any
            // The caller of this function simply takes the first element of the array,
            // so we can sort the V4 to the first place if there is one
            if (addrs != null && addrs.Length > 0) {
                for(int i = 0; i< addrs.Length; i++) {
                    if (addrs[i].AddressFamily == AddressFamily.InterNetwork) {
                        // this is a IPv4
                        if (i > 0) {
                            // swap it to first place
                            IPAddress tmp = addrs[0];
                            addrs[0] = addrs[i];
                            addrs[i] = tmp;
                        }
                        foundV4=true;
                        Debug.LogFormat("DNS: host {0} is IPv4 {1}",name,addrs[0].ToString());
                        break;
                    }
                }
                if (!foundV4) {
                    // TODO: anlayze what breaks in the V6 case, maybe we can make it work?
                    Debug.LogFormat("Did not find an IPv4 for {0}, game might crash on V6 addresses", name);
                }
            } else {
                Debug.LogFormat("Failed to resolve host name {0}", name);
            }
            return addrs;
        }
        public static string ClientModeName()
        {
            return Enabled ? "INTERNET MATCH" : "LAN MATCH";
        }
        public static string PasswordFieldName()
        {
            return MenuManager.m_mp_lan_match && Enabled ? "SERVER IP (PASSWORD)" : "PASSWORD";
        }
        public static void CheckInternetServer()
        {
            if (Core.GameMod.FindArg("-internet") && GameplayManager.IsDedicatedServer())
            {
                Enabled = ServerEnabled = true;
                ServerAddress = IPAddress.Any;
                Debug.Log("Internet server enabled");
            }
        }

        private static FieldInfo _InternetMatch_Enabled_Field = typeof(GameManager).Assembly.GetType("InternetMatch").GetField("Enabled", BindingFlags.Static | BindingFlags.Public);
        public static bool Enabled
        {
            get
            {
                if (Core.GameMod.HasInternetMatch())
                    return (bool)_InternetMatch_Enabled_Field.GetValue(null);
                else
                    return OldEnabled;
            }
            set
            {
                if (Core.GameMod.HasInternetMatch())
                    _InternetMatch_Enabled_Field.SetValue(null, value);
                else
                    OldEnabled = value;
            }
        }

        private static PropertyInfo _MenuManager_mms_match_password_Property = typeof(MenuManager).GetProperty("mms_match_password", BindingFlags.Static | BindingFlags.Public);
        private static FieldInfo _MenuManager_mms_match_password_Field = typeof(MenuManager).GetField("mms_match_password", BindingFlags.Static | BindingFlags.Public);
        public static string MenuPassword
        {
            get
            {
                if (Core.GameMod.HasInternetMatch())
                    return (string)_MenuManager_mms_match_password_Property.GetValue(null, null);
                else
                    return (string)_MenuManager_mms_match_password_Field.GetValue(null);
            }
            set
            {
                if (Core.GameMod.HasInternetMatch())
                    _MenuManager_mms_match_password_Property.SetValue(null, value, null);
                else
                    _MenuManager_mms_match_password_Field.SetValue(null, value);
            }
        }

        private static FieldInfo _MenuManager__mms_ip_address_Field = typeof(MenuManager).GetField("_mms_ip_address", BindingFlags.Static | BindingFlags.Public);
        public static string MenuIPAddress
        {
            get
            {
                if (Core.GameMod.HasInternetMatch())
                    return (string)_MenuManager__mms_ip_address_Field.GetValue(null);
                else
                    return (string)_MenuManager_mms_match_password_Field.GetValue(null);
            }
            set
            {
                if (Core.GameMod.HasInternetMatch())
                    _MenuManager__mms_ip_address_Field.SetValue(null, value);
                else
                    _MenuManager_mms_match_password_Field.SetValue(null, value);
            }
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawMpMenu")]
    class MPInternetMainDraw
    {
        private static bool Prepare() {
            return !Core.GameMod.HasInternetMatch();
        }

        private static void DrawItem(UIElement uie, ref Vector2 position)
        {
            uie.SelectAndDrawItem(Loc.LS("INTERNET MATCH"), position, 4, false, 1f, 0.75f);
            position.y += 62f;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var mpInternetMainDraw_DrawItem_Method = AccessTools.Method(typeof(MPInternetMainDraw), "DrawItem");

            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldstr && (string)code.operand == "CUSTOMIZE")
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldloca, 0);
                    yield return new CodeInstruction(OpCodes.Call, mpInternetMainDraw_DrawItem_Method);
                }
                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(MenuManager), "MpMenuUpdate")]
    class MPInternetMainUpdate
    {
        private static bool Prepare() {
            return !Core.GameMod.HasInternetMatch();
        }

        private static void Prefix()
        {
            MPInternet.OldEnabled = false;
        }

        private static void Postfix()
        {
            if (MenuManager.m_menu_sub_state == MenuSubState.ACTIVE && UIManager.PushedSelect(100))
            {
                if (UIManager.m_menu_selection == 4)
                {
                    Debug.Log("MpMenuUpdate - pushed internet");
                    MPInternet.OldEnabled = true;
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
                    MPInternet.OldEnabled = false;
                }
            }
        }
    }

    [HarmonyPatch(typeof(MenuManager), "MpMatchSetup")]
    class MPInternetMatchSetup
    {
        private static bool Prepare() {
            return !Core.GameMod.HasInternetMatch();
        }

        private static IEnumerable<CodeInstruction> Transpiler(ILGenerator ilGen, IEnumerable<CodeInstruction> codes)
        {
            var menuManager_m_menu_state_Field = AccessTools.Field(typeof(MenuManager), "m_menu_state");
            var menuManager_m_next_menu_state_Field = AccessTools.Field(typeof(MenuManager), "m_next_menu_state");
            var mpInternet_ClientModeName_Method = AccessTools.Method(typeof(MPInternet), "ClientModeName");
            var mpInternet_PasswordFieldName_Method = AccessTools.Method(typeof(MPInternet), "PasswordFieldName");

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
                        yield return new CodeInstruction(OpCodes.Ldsfld, menuManager_m_menu_state_Field);
                        yield return new CodeInstruction(OpCodes.Ldsfld, menuManager_m_next_menu_state_Field);
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
                    yield return new CodeInstruction(OpCodes.Call, mpInternet_ClientModeName_Method) { labels = code.labels };
                    continue;
                }
                if (code.opcode == OpCodes.Ldstr && (string)code.operand == "PASSWORD")
                {
                    yield return new CodeInstruction(OpCodes.Call, mpInternet_PasswordFieldName_Method) { labels = code.labels };
                    continue;
                }
                yield return code;
            }
        }
    }

    // rename password field if internet match selected
    [HarmonyPatch(typeof(UIElement), "DrawMpMatchSetup")]
    class MPInternetMatchSetupDraw
    {
        private static bool Prepare() {
            return !Core.GameMod.HasInternetMatch();
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var mpInternet_PasswordFieldName_Method = AccessTools.Method(typeof(MPInternet), "PasswordFieldName");

            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldstr && (string)code.operand == "PASSWORD")
                {
                    yield return new CodeInstruction(OpCodes.Call, mpInternet_PasswordFieldName_Method) { labels = code.labels };
                    continue;
                }
                yield return code;
            }
        }
    }

    // remove olmod link
    [HarmonyPatch(typeof(UIElement), "DrawMpMatchSetup")]
    class MPInternetMatchSetupDrawOlMod
    {
        private static bool Prepare() {
            return Core.GameMod.HasInternetMatch();
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0; // 0 before olmod text, 1 waiting for position reset, 2 after position reset
            foreach (var code in codes)
            {
                if (state == 0 && code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 200f)
                {
                    state = 1;
                }
                if (state == 1)
                    if (code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 0f)
                        state = 2;
                    else
                        continue;
                yield return code;
            }
        }
    }

    // after password entered: translate password to ip, adjust mp status
    [HarmonyPatch(typeof(NetworkMatch), "SwitchToLobbyMenu")]
    class MPInternetStart
    {
        private static bool Prepare() {
            return !Core.GameMod.HasInternetMatch();
        }

        private static FieldInfo _NetworkMatch_m_private_data_Field = typeof(NetworkMatch).GetField("m_private_data", BindingFlags.NonPublic | BindingFlags.Static);
        private static bool Prefix()
        {
            if (!MPInternet.OldEnabled || MPInternet.ServerEnabled)
                return true;
            string pwd = NetworkMatch.m_match_req_password;
            if (pwd == "")
            {
                var pmd = (PrivateMatchDataMessage)_NetworkMatch_m_private_data_Field.GetValue(null);
                pwd = pmd != null ? pmd.m_password : "";
            }
            MPInternet.ServerAddress = MPInternet.FindPasswordAddress(pwd.Trim(), out string msg);
            if (MPInternet.ServerAddress == null)
            {
                Debug.Log("SwitchToLobbyMenu FindPasswordAddress failed " + msg);
                NetworkMatch.CreateGeneralUIPopup("INTERNET MATCH", msg, 1);
                //MenuManager.ChangeMenuState(MenuState.MP_LOCAL_MATCH, true);
                /*
                MenuManager.m_menu_state = MenuState.MP_LOCAL_MATCH;
                MenuManager.m_next_menu_state = MenuManager.m_menu_state;
                UIManager.CreateUIElement(UIManager.SCREEN_CENTER, 7000, UIElementType.MP_MATCH_SETUP, Loc.LS("INTERNET MATCH"));
                MenuManager.m_menu_sub_state = MenuSubState.ACTIVE;
                MenuManager.m_menu_micro_state = NetworkMatch.m_match_req_password == "" ? 4 : 1;
                */
                MenuManager.ClearMpStatus();
                return false;
            }
            MenuManager.m_mp_status = NetworkMatch.m_match_req_password == "" ? Loc.LS("CREATING INTERNET MATCH") : Loc.LS("JOINING INTERNET MATCH");
            return true;
        }
    }

    // do not use interfaces for sending, instead send to listening socket (see below)
    [HarmonyPatch(typeof(BroadcastState), "EnumerateNetworkInterfaces")]
    class MPInternetEnum
    {
        private static bool Prepare() {
            return !Core.GameMod.HasInternetMatch();
        }

        private static bool Prefix(Action<object> callback)
        {
            if (!MPInternet.OldEnabled)
                return true;
            return false;
        }
    }

    //  create connection to server and use it for sending and receiving
    [HarmonyPatch(typeof(BroadcastState), MethodType.Constructor, new [] { typeof(int), typeof(int), typeof(IBroadcastStateReceiver) })]
    class MPInternetState
    {
        private static bool Prepare() {
            return !Core.GameMod.HasInternetMatch();
        }

        /*
        public static int ListenPortMod(int value)
        {
            if (!MPInternet.Enabled)
                return value;
            return 8001; // Exception if olproxy is running...
        }
        */

        private static Type _InterfaceState_Type = typeof(BroadcastState).Assembly.GetType("Overload.BroadcastState").GetNestedType("InterfaceState", BindingFlags.NonPublic);
        private static FieldInfo _BroadcastState_m_interfaces_Field = typeof(BroadcastState).GetField("m_interfaces", BindingFlags.NonPublic | BindingFlags.Instance);
        private static MethodInfo _List_BroadcastState_InterfaceState_Add_Method = _BroadcastState_m_interfaces_Field.FieldType.GetMethod("Add");
        private static FieldInfo _BroadcastState_m_receiveClient_Field = typeof(BroadcastState).GetField("m_receiveClient", BindingFlags.NonPublic | BindingFlags.Instance);
        // create receiving socket and also use it for sending by adding it to m_interfaces
        public static bool InitReceiveClient(BroadcastState bs)
        {
            if (!MPInternet.OldEnabled)
                return false;

            var client = new UdpClient();

            var ep = new IPEndPoint(MPInternet.ServerAddress, 8001);
            if (MPInternet.ServerEnabled)
            {
                // allowing multiple servers doesn't work, only one of the servers receives the packet, and it might be busy etc.
                //client.Client.ExclusiveAddressUse = false;
                //client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, 0);
                //client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
                try
                {
                    client.Client.Bind(ep);
                }
                catch (Exception ex)
                {
                    Debug.Log("Internet server setup failed " + ex.ToString());
                    Application.Quit();
                }
            }
            else
            {
                client.Connect(ep);

                // add socket to sending sockets (m_interfaces)
                object intf = Activator.CreateInstance(_InterfaceState_Type, new object[] { client, ep.Address, null, new IPAddress(-1) });
                var intfs = _BroadcastState_m_interfaces_Field.GetValue(bs);
                _List_BroadcastState_InterfaceState_Add_Method.Invoke(intfs, new object[] { intf });
            }

            _BroadcastState_m_receiveClient_Field.SetValue(bs, client);

            return true;
        }

        private static IEnumerable<CodeInstruction> Transpiler(ILGenerator ilGen, IEnumerable<CodeInstruction> codes)
        {
            var mpInternetState_InitReceiveClient_Method = AccessTools.Method(typeof(MPInternetState), "InitReceiveClient");

            int state = 0; // 0 = before 1st LogFormat, 1 = before 2nd LogFormat, 2 = before rc Bind, 3 = after rc Bind, 4 = after Bind label
            Label initRCLabel = ilGen.DefineLabel();
            foreach (var code in codes) //Trans.FieldReadModifier("listenPort", AccessTools.Method(typeof(MPInternetState), "ListenPortMod"), codes))
            {
                //Debug.Log("state " + state + " op " + code.opcode + " " + code.operand);
                if (state <= 1 && code.opcode == OpCodes.Call && ((MemberInfo)code.operand).Name == "LogFormat" && ++state == 2)
                {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, mpInternetState_InitReceiveClient_Method);
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

    [HarmonyPatch(typeof(BroadcastState), "InitInternetMatch")]
    class MPInternetServer
    {
        private static FieldInfo _BroadcastState_m_receiveClient_Field = typeof(BroadcastState).GetField("m_receiveClient", BindingFlags.NonPublic | BindingFlags.Instance);
        private static bool Prepare() {
            return Core.GameMod.HasInternetMatch();
        }

        private static bool Prefix(BroadcastState __instance) {
            if (!MPInternet.ServerEnabled)
                return true;

            var client = new UdpClient();
            var ep = new IPEndPoint(IPAddress.Any, 8001);

            try
            {
                client.Client.Bind(ep);
            }
            catch (Exception ex)
            {
                Debug.Log("Internet server setup failed " + ex.ToString());
                Application.Quit();
            }

            _BroadcastState_m_receiveClient_Field.SetValue(__instance, client);

            return false;
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

    // replace received server ip with the outgoing ip we used (server probably transmits internal ip)
    [HarmonyPatch(typeof(LocalLANClient), "DoTick")]
    class MPInternetConnInfo
    {
        private static bool Prepare() {
            return !Core.GameMod.HasInternetMatch();
        }

        public static MatchmakerConnectionInfo ConnInfoMod(MatchmakerConnectionInfo connInfo)
        {
            if (MPInternet.OldEnabled)
                connInfo.IPAddress = MPInternet.ServerAddress.ToString();
            return connInfo;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var mpInternetState_ListenPortMod_Method = AccessTools.Method(typeof(MPInternetState), "ListenPortMod");
            var mpInternetConnInfo_ConnInfoMod_Method = AccessTools.Method(typeof(MPInternetConnInfo), "ConnInfoMod");

            foreach (var code in Trans.FieldReadModifier("listenPort", mpInternetState_ListenPortMod_Method, codes))
            {
                if (code.opcode == OpCodes.Stfld && ((FieldInfo)code.operand).Name == "GameSessionConnectionInfo")
                    yield return new CodeInstruction(OpCodes.Call, mpInternetConnInfo_ConnInfoMod_Method);
                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(BroadcastState), "InternalSendPacket")]
    class MPInternetServerSend
    {
        private static bool Prepare()
        {
            return MPInternet.ServerEnabled;
        }

        private static bool Prefix(byte[] buffer, int bufferSize, IDictionary ___m_clientStates, UdpClient ___m_receiveClient)
        {
            if (!MPInternet.ServerEnabled)
                return true;
            foreach (var key in ___m_clientStates.Keys)
            {
                var addr = key as IPEndPointProcessId;
                ___m_receiveClient.Send(buffer, bufferSize, addr.endPoint);
            }
            return false;
        }
    }

    // -host support, allows multiple servers with same ip but different hostnames
    [HarmonyPatch(typeof(NetworkMatch), "TryLocalMatchmaking")]
    class MPServerHostFilter
    {
        static string HostArg;

        private static bool Prepare()
        {
            return Core.GameMod.FindArgVal("-host", out HostArg);
        }

        private static void Prefix(List<DistributedMatchUp.Match> candidates, DistributedMatchUp.Match backfillSeedMatch)
        {
            if (HostArg == null || backfillSeedMatch != null)
                return;
            List<DistributedMatchUp.Match> delReqs = new List<DistributedMatchUp.Match>();
            foreach (var req in candidates)
                foreach (var player in JArray.Parse(req.matchData["mm_players"].stringValue))
                {
                    JObject attrs = (player as JObject)["PlayerAttributes"] as JObject;
                    if (!attrs.TryGetValue("password", out JToken passwordAttrToken) || passwordAttrToken == null)
                        continue;
                    var passwordAttr = passwordAttrToken as JObject;
                    var passwordAttrType = (string)passwordAttr["attributeType"];
                    if (passwordAttrType != "STRING" && passwordAttrType != "STRING_LIST")
                        continue;
                    string password = (string)(passwordAttrType == "STRING_LIST" ? ((JArray)passwordAttr["valueAttribute"])[0] : passwordAttr["valueAttribute"]);
                    if (password == "")
                        continue;
                    var i = password.IndexOf('_'); // allow password suffix with '_'
                    string name = i == -1 ? password : password.Substring(0, i);
                    if (!name.Equals(HostArg, StringComparison.InvariantCultureIgnoreCase))
                    {
                        Debug.LogFormat("{0}: {1}: Match with wrong host ignored: {2}", DateTime.Now.ToString(), HostArg, name);
                        delReqs.Add(req);
                        break;
                    }
                }
            foreach (var req in delReqs)
                candidates.Remove(req);
        }
    }

    // InternalSendPacket does not handle potential socket exceptions
    // these Exceptions occur if the peer sends an ICMP failure when the port is not open,
    // In case of such a socket error, we abort the connection attempt.
    // Note that in theory, since UDP is connection-less, we could just ignore
    // the exception and go on, and a later attempt may succeed. However, this
    // is useless, as the async receive callback will also get the exception
    // and will bail out, so we're never able to receive an answer
    [HarmonyPatch(typeof(BroadcastState), "InternalSendPacket")]
    class MPInternetFixUnreachableServerException
    {
        private static Exception Finalizer(Exception __exception) {
            if (__exception != null) {
                if (__exception.GetType() == typeof(SocketException)) {
                    if (MPInternet.Enabled) {
                        NetworkMatch.CreateGeneralUIPopup("ERROR connecting to server", __exception.Message, 5.0f);
                        NetworkMatch.ExitMatchToMainMenu();
                    }
                    return null;
                }
            }
            // forward all other exceptions
            return __exception;
        }
    }

    // The receiveCallback delegate does not handle exceptions
    // These exceptions are catched _somewhere_ and do not end up
    // in the logfile, but the recieve callback dies when an
    // exception occurs. However, the main thread waits for
    // BrodcastState.m_readLoopExit == true to be set by
    // the receiveCallback, which won't happen in that case.
    // This patch ensures that this is done in the exception case.
    // The code is ultra-ugly due to all the reflection as it
    // involves two compiler-generated classes.
    [HarmonyPatch]
    class MPInternetFixHangOnUnreachableServer
    {
        private static FieldInfo _BroadcastState_m_readLoopExit = typeof(BroadcastState).GetField("m_readLoopExit", BindingFlags.NonPublic | BindingFlags.Instance);

        // helper function to find the types for the anon classes
        private static Type FindTypeHelper(string name)
        {
            foreach (var x in typeof(BroadcastState).GetNestedTypes(BindingFlags.NonPublic)) {
                if (x.Name.Contains(name)) {
                    return x;
                }
            }
            return null;
        }

        // the traget method for the harmony patch
        public static MethodBase TargetMethod()
        {
            var x = FindTypeHelper("c__AnonStorey1");
            if (x != null) {
                var m = AccessTools.Method(x, "<>m__0");
                if (m != null) {
                    Debug.Log("BrodcastState receiveCallback TargetMethod found");
                    return m;
                }
            }
            Debug.Log("BroadcastState receiveCallback TargetMethod not found");
            return null;
        }

        // the actual patch
        private static Exception Finalizer(object __instance, Exception __exception) {
            if (__exception != null) {
                Debug.LogFormat("BroadcastState receiveCallback died, telling main thread!");
                try {
                    Type anon0 = FindTypeHelper("c__AnonStorey0");
                    Type anon1 = FindTypeHelper("c__AnonStorey1");
                    FieldInfo _f_ref_field = AccessTools.Field(anon1, "<>f__ref$0");
                    FieldInfo _this_field = AccessTools.Field(anon0, "$this");
                    object f_ref = _f_ref_field.GetValue(__instance);
                    BroadcastState bs = (BroadcastState)_this_field.GetValue(f_ref);
                   _BroadcastState_m_readLoopExit.SetValue(bs, true);
                } catch(Exception ex) {
                    Debug.LogFormat("failed to inform main thread: {0}", ex.ToString());
                }
            }
            return null;
        }
    }

    [HarmonyPatch]
    class MPInternetIgnoreIPHostnameForMatchmaking
    {
        // helper function to find the types for the anon classes
        private static Type FindTypeHelper(string name)
        {
            foreach (var x in typeof(NetworkMatch).GetNestedTypes(BindingFlags.NonPublic)) {
                if (x.Name.Contains(name)) {
                    return x;
                }
            }
            return null;
        }

        // the traget method for the harmony patch
        public static MethodBase TargetMethod()
        {
            var x = FindTypeHelper("c__AnonStoreyF");
            if (x != null) {
                var m = AccessTools.Method(x, "<>m__1");
                if (m != null) {
                    //Debug.Log("TryLocalMatchmaking TargetMethod found");
                    return m;
                }
            }
            Debug.Log("TryLocalMatchmaking TargetMethod not found");
            return null;
        }

        public static string TransformPassword(string pw)
        {
            if (pw != null && MPInternet.Enabled) {
                // ignore IP/hostname for password comparison
                int pos = pw.IndexOf('_');
                if (pos >= 0) {
                    // there is a password, use it (including the underscore)
                    pw = pw.Substring(pos);
                    //Debug.LogFormat("TryLocalMatchmaking: using \"{0}\" as passowrd", pw);
                } else {
                    pw = "EMPTY"; // without underscore, so no client could have sent it
                    //Debug.LogFormat("TryLocalMatchmaking: game with no password, using \"{0}\" internally", pw);
                }
            }
            return pw;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var our_Method = AccessTools.Method(typeof(MPInternetIgnoreIPHostnameForMatchmaking), "TransformPassword");
            foreach (var code in codes)
            {
                // Loc 5 is the password, we modify it before it gets stored
                if (code.opcode == OpCodes.Stloc_S && ((LocalBuilder)code.operand).LocalIndex == 5) {
                    yield return new CodeInstruction(OpCodes.Call, our_Method);
                }
                yield return code;
            }
        }
    }

    [HarmonyPatch]
    class MPInternetAllowSimpleHostname {
        // the traget method for the harmony patch
        public static MethodBase TargetMethod()
        {
            var x = typeof(GameManager).Assembly.GetType("InternetMatch");
            if (x != null) {
                var m = AccessTools.Method(x, "FindPasswordAddress");
                if (m != null) {
                    return m;
                }
            }
            Debug.Log("InternetMatch FindPasswordAddress TargetMethod not found");
            return null;
        }
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;
            var our_Method = AccessTools.Method(typeof(MPInternet), "ResolveIPAddress");
            foreach (var code in codes)
            {
                if (state == 0 && code.opcode == OpCodes.Ldstr && ((string)code.operand) == "Invalid IP address or server name") {
                    state = 1;
                    yield return code;
                } else if (state == 1 && code.opcode == OpCodes.Ret) {
                    state = 2;
                    // omit this return, pop the return value from the stack again instead
                    yield return new CodeInstruction(OpCodes.Pop);
                } else if (state == 2 && code.opcode == OpCodes.Call && ((MemberInfo)code.operand).Name == "GetHostAddresses") {
                    state = 3;
                    // call our ResolveIPAddress instead of Dns.GetHostAddresses
                    yield return new CodeInstruction(OpCodes.Call, our_Method);
                } else {
                    yield return code;
                }
            }
        }
    }
}
