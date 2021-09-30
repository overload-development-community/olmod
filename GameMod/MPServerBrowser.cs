using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Newtonsoft.Json;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod
{

    static class MPServerBrowser
    {
        static readonly string browserUrl = "https://tracker.otl.gg/api/browser";
        static readonly int refreshTime = 10000; // 10s
        public static readonly MenuState msServerBrowser = (MenuState)75;
        public static readonly UIElementType uiServerBrowser = (UIElementType)89;
        public static DateTime lastUpdated = new DateTime(1970, 1, 1);
        static bool isRefreshing = false;
        public static string mms_match_notes = "";
        public static List<MPServerBrowserItem> Items = new List<MPServerBrowserItem>();
        public static MPServerBrowserItem selectedItem { get; set; }
        public static bool browserCoroutineActive = false;
        public static bool menuActive
        {
            get
            {
                return MenuManager.m_menu_state == msServerBrowser;
            }
        }

        static IEnumerator GetTrackerData(string url)
        {
            UnityWebRequest www = UnityWebRequest.Get(url);
            yield return www.SendWebRequest();

            lastUpdated = DateTime.UtcNow;
            isRefreshing = false;

            if (www.isNetworkError || www.isHttpError)
            {
                Debug.Log(www.error);
                yield break;
            }

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc
            };
            List<MPTrackerEntry> results = JsonConvert.DeserializeObject<List<MPTrackerEntry>>(www.downloadHandler.text, settings);
            List<MPServerBrowserItem> newItems = new List<MPServerBrowserItem>();
            foreach (var result in results.Where(x => x.server.online))
            {
                var existing = Items.FirstOrDefault(x => x.ip == result.server.ip && x.port == result.server.port);

                if (existing == null)
                {
                    // Add
                    MPServerBrowserItem.BrowserItemStatus status;
                    if (result.game == null)
                    {
                        // No game/lobby
                        if (result.server.online)
                        {
                            status = MPServerBrowserItem.BrowserItemStatus.READY;
                        }
                        else
                        {
                            status = MPServerBrowserItem.BrowserItemStatus.OFFLINE;
                        }
                    }
                    else
                    {
                        // Game or lobby going
                        if (result.game.inLobby)
                        {
                            status = MPServerBrowserItem.BrowserItemStatus.INLOBBY;
                        }
                        else
                        {
                            status = MPServerBrowserItem.BrowserItemStatus.PLAYING;
                        }
                    }

                    IPAddress addr = IPAddress.Any;
                    try
                    {
                        if (!IPAddress.TryParse(result.server.ip, out addr))
                        {
                            var addrs = Dns.GetHostAddresses(result.server.ip);
                            addr = addrs == null || addrs.Length == 0 ? null : addrs[0];
                        }
                    }
                    catch (Exception)
                    {
                        //Debug.LogException(ex);
                    }

                    newItems.Add(new MPServerBrowserItem
                    {
                        ip = result.server.ip,
                        port = result.server.port,
                        name = result.server.name,
                        serverNotes = result.server.serverNotes,
                        online = result.server.online,
                        gameStarted = result.game == null ? (DateTime?)null : result.game.gameStarted,
                        currentPlayers = result.game == null ? 0 : result.game.currentPlayers,
                        maxPlayers = result.game == null ? 0 : result.game.maxPlayers,
                        matchLength = result.game == null ? 0 : result.game.matchLength,
                        mapName = result.game == null ? "" : result.game.mapName,
                        mode = result.game == null ? "" : result.game.mode,
                        jip = result.game == null ? false : result.game.jip,
                        hasPassword = result.game == null ? false : result.game.hasPassword,
                        lastPingRequest = DateTime.UtcNow.AddSeconds(-5),
                        matchNotes = result.game == null ? "" : result.game.matchNotes,
                        inLobby = result.game == null ? false : result.game.inLobby,
                        status = status,
                        addr = addr
                    });
                }
                else
                {
                    // Update
                    existing.serverNotes = result.server.serverNotes;
                    existing.online = result.server.online;
                    existing.gameStarted = result.game == null ? (DateTime?)null : result.game.gameStarted;
                    existing.currentPlayers = result.game == null ? 0 : result.game.currentPlayers;
                    existing.maxPlayers = result.game == null ? 0 : result.game.maxPlayers;
                    existing.matchLength = result.game == null ? 0 : result.game.matchLength;
                    existing.mapName = result.game == null ? "" : result.game.mapName;
                    existing.mode = result.game == null ? "" : result.game.mode;
                    existing.jip = result.game == null ? false : result.game.jip;
                    existing.hasPassword = result.game == null ? false : result.game.hasPassword;
                    existing.matchNotes = result.game == null ? "" : result.game.matchNotes;
                    existing.inLobby = result.game == null ? false : result.game.inLobby;
                    if (result.game == null)
                    {
                        // No game/lobby
                        if (result.server.online)
                        {
                            existing.status = MPServerBrowserItem.BrowserItemStatus.READY;
                        }
                        else
                        {
                            existing.status = MPServerBrowserItem.BrowserItemStatus.OFFLINE;
                        }
                    }
                    else
                    {
                        // Game or lobby going
                        if (result.game.inLobby)
                        {
                            existing.status = MPServerBrowserItem.BrowserItemStatus.INLOBBY;
                        }
                        else
                        {
                            existing.status = MPServerBrowserItem.BrowserItemStatus.PLAYING;
                        }
                    }
                }
            }

            foreach (var item in Items)
            {
                if (!item.online)
                    item._lastPing = 0;
            }
            Items.AddRange(newItems);
            Items = Items.OrderByDescending(x => x.currentPlayers).ThenByDescending(x => x.status).ThenByDescending(x => x.name.StartsWith("roncli")).ThenBy(x => x.name).ToList();
            UpdateList();
        }

        public static byte[] CreatePingPacket(long time, int idx)
        {
            var packet = new byte[19 + 4 + 4 + 8 + 4];
            Array.Copy(BitConverter.GetBytes(-1), 0, packet, 0, 4);
            Array.Copy(BitConverter.GetBytes(0), 0, packet, 8, 4);
            Array.Copy(BitConverter.GetBytes(0), 0, packet, 19, 4);
            Array.Copy(BitConverter.GetBytes(0), 0, packet, 19 + 4, 4);
            Array.Copy(BitConverter.GetBytes(time), 0, packet, 19 + 4 + 4, 8);
            Array.Copy(BitConverter.GetBytes(idx), 0, packet, 19 + 4 + 4 + 8, 4);
            uint hash = xxHashSharp.xxHash.CalculateHash(packet);
            Array.Copy(BitConverter.GetBytes(hash), 0, packet, 8, 4);
            return packet;
        }

        public static bool CheckPingHash(byte[] packet)
        {
            var hash = BitConverter.ToUInt32(packet, 8);
            Array.Copy(BitConverter.GetBytes(0), 0, packet, 8, 4);
            var srcHash = xxHashSharp.xxHash.CalculateHash(packet);
            return srcHash == hash;
        }

        public static long PingGetTime(byte[] packet)
        {
            return BitConverter.ToInt64(packet, 19 + 4 + 4);
        }

        struct ReceivedPacketInfo
        {
            public byte[] packet;
            public IPEndPoint endPoint;
            public long time;
        }

        public static void UpdateList()
        {
            MenuManager.m_list_items_total_count = Items.Count();
            MenuManager.m_list_items_max_per_page = Math.Min(MenuManager.m_list_items_total_count, 12);
            while (MenuManager.m_list_items_first > MenuManager.m_list_items_total_count) {
                MenuManager.m_list_items_first -= 12;
            }
            MenuManager.m_list_items_last = Mathf.Min(MenuManager.m_list_items_first + MenuManager.m_list_items_max_per_page - 1, MenuManager.m_list_items_total_count - 1);
            MenuManager.m_list_item_paging = (MenuManager.m_list_items_total_count > 12);
        }

        public static IEnumerator Update()
        {
            browserCoroutineActive = true;
            UdpClient socket = new UdpClient();
            socket.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
            var receivedPackets = new List<ReceivedPacketInfo>();
            AsyncCallback receiver = null;
            receiver = new AsyncCallback((ar) => {
                if (!browserCoroutineActive)
                    return;
                var arSocket = (UdpClient)ar.AsyncState;
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
                var inPacket = arSocket.EndReceive(ar, ref endPoint);
                if (!CheckPingHash(inPacket))
                    return;
                var receivedPacket = new ReceivedPacketInfo
                {
                    packet = inPacket,
                    endPoint = endPoint,
                    time = DateTime.UtcNow.Ticks
                };
                lock (receivedPackets)
                {
                    receivedPackets.Add(receivedPacket);
                }
                socket.BeginReceive(receiver, arSocket);
            });
            socket.BeginReceive(receiver, socket);

            while (menuActive)
            {
                List<ReceivedPacketInfo> newPackets = null;
                lock (receivedPackets)
                {
                    if (receivedPackets.Any())
                    {
                        newPackets = new List<ReceivedPacketInfo>();
                        newPackets.AddRange(receivedPackets);
                        receivedPackets.Clear();
                    }
                }
                if (newPackets != null)
                {
                    foreach (var packetInfo in newPackets)
                    {
                        var endPoint = packetInfo.endPoint;
                        var entry = Items.FirstOrDefault(x => x.addr.Equals(endPoint.Address)); // && x.port == endPoint.Port);
                        if (entry == null)
                            continue;
                        var orgTime = PingGetTime(packetInfo.packet);
                        entry._lastPing = (int)((packetInfo.time - orgTime) / 10000);
                    }
                }

                var now = DateTime.UtcNow;
                if (now.Subtract(lastUpdated).TotalMilliseconds > refreshTime && !isRefreshing)
                {
                    // Update servers from json
                    isRefreshing = true;
                    GameManager.m_gm.StartCoroutine(GetTrackerData(browserUrl));
                }

                foreach (var entry in Items.Where(x => x.online && now.Subtract(x.lastPingRequest).TotalMilliseconds > 5000))
                {
                    try
                    {
                        entry.lastPingRequest = DateTime.UtcNow;

                        var packet = CreatePingPacket(DateTime.UtcNow.Ticks, 1);
                        socket.BeginSend(packet, packet.Length, new IPEndPoint(entry.addr, 8001),
                            (ar) => ((UdpClient)ar.AsyncState).EndSend(ar), socket);
                    }
                    catch (Exception)
                    {
                        //Debug.LogException(ex);
                        //uConsole.Log("Unable to ping " + entry.name);
                    }

                }
                yield return new WaitForSecondsRealtime(1f);
            }
            browserCoroutineActive = false;
        }

        private class MPTrackerEntry
        {
            public MPTrackerServer server { get; set; }
            public MPTrackerGame game { get; set; }

            public class MPTrackerServer
            {
                public string ip { get; set; }
                public int port { get; set; }
                public string name { get; set; }
                public string serverNotes { get; set; }
                public bool online { get; set; }
            }

            public class MPTrackerGame
            {
                public DateTime gameStarted { get; set; }
                public int currentPlayers { get; set; }
                public int maxPlayers { get; set; }
                public int matchLength { get; set; }
                public string mapName { get; set; }
                public string mode { get; set; }
                public bool jip { get; set; }
                public bool hasPassword { get; set; }
                public string matchNotes { get; set; }
                public bool inLobby { get; set; }
            }
        }
    }

    public class MPServerBrowserItem
    {
        public string ip { get; set; }
        public int port { get; set; }
        public string name { get; set; }
        public string serverNotes { get; set; }
        public bool online { get; set; }
        public DateTime? gameStarted { get; set; }
        public int currentPlayers { get; set; }
        public int maxPlayers { get; set; }
        public int matchLength { get; set; }
        public string mapName { get; set; }
        public string mode { get; set; }
        public bool jip { get; set; }
        public bool hasPassword { get; set; }
        public int _lastPing;
        internal IPAddress addr;

        public DateTime lastPingRequest { get; set; }
        public string matchNotes { get; set; }
        public bool inLobby { get; set; }
        public BrowserItemStatus status { get; set; }
        public int ping
        {
            get
            {
                return _lastPing;
            }
        }
        public string statusText
        {
            get
            {
                if (!online)
                    return "OFFLINE";

                switch (status)
                {
                    case BrowserItemStatus.INLOBBY:
                        return String.Format("IN LOBBY ({0}/{1})", currentPlayers, maxPlayers);
                    case BrowserItemStatus.PLAYING:
                        return String.Format("IN MATCH ({0}/{1})", currentPlayers, maxPlayers);
                    case BrowserItemStatus.READY:
                        return "READY";
                    default:
                        return "ERROR";
                }
            }
        }
        public string actionText
        {
            get
            {
                if (online && status == MPServerBrowserItem.BrowserItemStatus.READY)
                {
                    return "CREATE";
                }
                else if (online && status == MPServerBrowserItem.BrowserItemStatus.PLAYING && !jip)
                {
                    return "CLOSED";
                }
                else if (online && currentPlayers == maxPlayers)
                {
                    return "FULL";
                }
                else if (online && (status == MPServerBrowserItem.BrowserItemStatus.INLOBBY || status == MPServerBrowserItem.BrowserItemStatus.PLAYING) && currentPlayers < maxPlayers)
                {
                    if (hasPassword)
                    {
                        return "JOIN (PW)";
                    }
                    else
                    {
                        return "JOIN";
                    }
                }

                return "ERROR";
            }
        }

        public enum BrowserItemStatus
        {
            OFFLINE,
            READY,
            INLOBBY,
            PLAYING
        }

    }

    [HarmonyPatch(typeof(UIElement), "DrawMpMenu")]
    class MPServerBrowser_UIElement_DrawMpMenu
    {
        static int loadCount = 0;

        private static void DrawBrowserButton(UIElement __instance, ref Vector2 position)
        {
            __instance.SelectAndDrawItem("SERVER BROWSER", position, 5, false, 1f, 0.75f);
            position.y += 62f;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var mpServerBrowser_UIElement_DrawMpMenu_Method = AccessTools.Method(typeof(MPServerBrowser_UIElement_DrawMpMenu), "DrawBrowserButton");

            int state = 0;
            foreach (var code in codes)
            {
                if (loadCount < 2 && state == 0 && code.opcode == OpCodes.Ldstr && (string)code.operand == "LAN MATCH")
                {
                    loadCount++;
                    state = 1;
                    yield return new CodeInstruction(OpCodes.Ldloca, 0);
                    yield return new CodeInstruction(OpCodes.Call, mpServerBrowser_UIElement_DrawMpMenu_Method);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                }
                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(MenuManager), "MpMenuUpdate")]
    class MPServerBrowser_MenuManager_MpMenuUpdate
    {

        private static void Postfix()
        {
            if (MenuManager.m_menu_sub_state == MenuSubState.INIT)
                MPServerBrowser.selectedItem = null;
            if (MenuManager.m_menu_sub_state == MenuSubState.ACTIVE && UIManager.PushedSelect(100))
            {
                if (UIManager.m_menu_selection == 5)
                {
                    UIManager.DestroyAll();
                    MenuManager.ChangeMenuState(MPServerBrowser.msServerBrowser);
                    MenuManager.PlaySelectSound();
                }
            }
        }
    }

    [HarmonyPatch(typeof(UIElement), "Draw")]
    class MPServerBrowser_UIElement_Draw
    {

        private static void Postfix(UIElement __instance)
        {
            if (__instance.m_type == MPServerBrowser.uiServerBrowser && __instance.m_alpha > 0f)
                DrawBrowserWindow(__instance);
        }

        static void DrawBrowserWindow(UIElement uie)
        {
            UIManager.ui_bg_dark = true;
            uie.DrawMenuBG();
            Vector2 position = uie.m_position;
            position.y = UIManager.UI_TOP + 64f;
            int menu_micro_state = MenuManager.m_menu_micro_state;

            switch (menu_micro_state)
            {
                default:
                    uie.DrawHeaderMedium(Vector2.up * (UIManager.UI_TOP + 30f), Loc.LS("SERVER BROWSER"), 265f);
                    position.y += 20f;
                    float d = -600f; // Title
                    float d2 = -310f; // Level
                    float d3 = -110f; // Players
                    //float d4 = 20f; // Status
                    float d5 = 100f; // Mode
                    float d6 = 260f; // Ping
                    float d7 = 330f; // JIP
                                     //float d8 = 490f; // PW
                    float d9 = 550f + 50f;
                    uie.DrawStringSmall(Loc.LS("SERVER"), position + Vector2.right * d, 0.5f, StringOffset.LEFT, UIManager.m_col_hi2, 1f);
                    uie.DrawStringSmall(Loc.LS("LEVEL"), position + Vector2.right * d2, 0.5f, StringOffset.LEFT, UIManager.m_col_hi2, 1f);
                    uie.DrawStringSmall(Loc.LS("STATUS"), position + Vector2.right * d3, 0.5f, StringOffset.LEFT, UIManager.m_col_hi2, 1f);
                    uie.DrawStringSmall(Loc.LS("MODE"), position + Vector2.right * d5, 0.5f, StringOffset.LEFT, UIManager.m_col_hi2, 1f);
                    uie.DrawStringSmall(Loc.LS("PING"), position + Vector2.right * d6, 0.5f, StringOffset.LEFT, UIManager.m_col_hi2, 1f);
                    uie.DrawStringSmall(Loc.LS("NOTES"), position + Vector2.right * d7, 0.5f, StringOffset.LEFT, UIManager.m_col_hi2, 1f);
                    position.y += 30f;
                    uie.DrawMenuSeparator(position);
                    position.y += 30f;
                    for (int i = MenuManager.m_list_items_first; i <= MenuManager.m_list_items_last; i++)
                    {
                        if (i >= MPServerBrowser.Items.Count)
                            continue;

                        var item = MPServerBrowser.Items[i];

                        Color c = item.online ? UIManager.m_col_ui3 : new Color(0.2f, 0.2f, 0.2f);
                        position.y += 35f;
                        uie.DrawStringSmall(item.name /*.Substring(0, Math.Min(item.name.Length, 28))*/, position + Vector2.right * d, 0.5f, StringOffset.LEFT, c, 1f, d2 - d - 4f);
                        uie.DrawStringSmall(item.mapName /*.Substring(0, Math.Min(item.mapName.Length, 18))*/, position + Vector2.right * d2, 0.5f, StringOffset.LEFT, c, 1f, d3 - d2 - 4f);
                        if (item.status == MPServerBrowserItem.BrowserItemStatus.INLOBBY || item.status == MPServerBrowserItem.BrowserItemStatus.PLAYING)
                        {
                            uie.SelectAndDrawTextOnlyItem(item.statusText, position + Vector2.right * d3, 20000 + i, 0.5f, StringOffset.LEFT, false);
                        }
                        else
                        {
                            uie.DrawStringSmall(item.statusText, position + Vector2.right * d3, 0.5f, StringOffset.LEFT, c, 1f);
                        }
                        uie.DrawStringSmall(item.mode, position + Vector2.right * d5, 0.5f, StringOffset.LEFT, c, 1f);
                        UIManager.m_fixed_width_digits = true;
                        Vector2 v = position + Vector2.right * d6;
                        v.x += 11f;
                        Color startColor = uie.GetPingColor(item.ping);
                        Color endColor = startColor;
                        endColor.r *= 0.5f;
                        endColor.g *= 0.5f;
                        endColor.b *= 0.5f;
                        if (item.ping > 0)
                            uie.DrawDigitsVariable(v, item.ping, 0.5f, StringOffset.LEFT, item.online ? Color.Lerp(startColor, endColor, UnityEngine.Random.Range(0f, 0.5f * UIElement.FLICKER)) : startColor, 1f);
                        uie.DrawStringSmall(item.matchNotes, position + Vector2.right * d7, 0.5f, StringOffset.LEFT, c, 1f, d9 - d7 - 25f - 4f);
                        Color c2 = item.online ? Color.Lerp(UIManager.m_col_hi5, UIManager.m_col_hi6, UnityEngine.Random.Range(0f, 0.5f * UIElement.FLICKER)) : c;
                        if (item.actionText == "CREATE" || item.actionText == "JOIN" || item.actionText == "JOIN (PW)")
                        {
                            SelectAndDrawSmallerItem(uie, item.actionText, position + Vector2.right * d9, 10000 + i, false, 0.1f, 0.5f);
                        }
                        else
                        {
                            uie.DrawStringSmall(item.actionText, position + Vector2.right * d9, 0.5f, StringOffset.CENTER, c2, 1f, 50f);
                        }
                        UIManager.m_fixed_width_digits = false;
                        if (i % 2 == 0)
                        {
                            UIManager.DrawQuadUI(position, 600f, 16f, UIManager.m_col_ub0, uie.m_alpha * 0.3f, 13);
                        }
                    }

                    position.y = UIManager.UI_BOTTOM - 100f;

                    uie.DrawPageControls(position,
                        string.Format(Loc.LS("SERVERS {0}-{1} OF {2}"), MenuManager.m_list_items_first + 1, MenuManager.m_list_items_last + 1, MPServerBrowser.Items.Count),
                        true, true, false, false, 500, 40, false);

                    position.y += 40f;
                    uie.DrawMenuSeparator(position);
                    position.y += 30f;
                    uie.SelectAndDrawItem(Loc.LS("RETURN TO MULTIPLAYER"), position, 100, fade: false);
                    break;
            }
        }

        public static void SelectAndDrawTextOnlyItemColor(UIElement uie, string s, Vector2 pos, int selection, float scale, StringOffset so, Color c, bool fade)
        {
            float stringWidth = UIManager.GetStringWidth(s, scale * 20f, 0, -1);
            if (!fade && !uie.m_fade_die)
            {
                Vector2 pos2 = pos;
                if (so == StringOffset.LEFT)
                {
                    pos2.x += stringWidth / 2f;
                }
                else if (so == StringOffset.RIGHT)
                {
                    pos2.x -= stringWidth / 2f;
                }
                uie.TestMouseInRect(pos2, stringWidth / 2f, 20f * scale, selection, true);
            }
            bool flag = UIManager.m_menu_selection == selection;
            uie.DrawStringSmall(s, pos, scale, so, c, (!fade) ? 1f : 0.2f, -1f);
        }

        public static void SelectAndDrawSmallerItem(UIElement uie, string s, Vector2 pos, int selection, bool fade, float width = 1f, float text_size = 0.75f)
        {
            float drawWidth = 500f * width;

            var so = StringOffset.CENTER;
            if (!fade && !uie.m_fade_die)
            {
                Vector2 pos2 = pos;
                if (so == StringOffset.LEFT)
                {
                    pos2.x += drawWidth / 2f;
                }
                else if (so == StringOffset.RIGHT)
                {
                    pos2.x -= drawWidth / 2f;
                }
                uie.TestMouseInRect(pos2, drawWidth / 2f, 20f * text_size, selection, true);
            }

            float num2 = 12.36f;
            float d = drawWidth * 0.5f + num2 * 0.9f - 1f;
            Color color;
            if (UIManager.m_menu_selection == selection)
            {
                color = Color.Lerp(UIManager.m_col_ui5, UIManager.m_col_ui6, UnityEngine.Random.Range(0f, 0.15f * UIElement.FLICKER) + ((!uie.m_fade_die) ? 0f : 0.5f));
                float a = color.a = 1f - Mathf.Pow(1f - uie.m_alpha, 8f);
                UIManager.DrawQuadBarHorizontal(pos, 16f, 16f, drawWidth, color, 12);
                color = UIManager.m_col_ub3;
                color.a = a;
                UIManager.DrawQuadUI(pos - d * Vector2.right, num2 * 0.1f, num2, color, color.a, 13);
                UIManager.DrawQuadUI(pos + d * Vector2.right, num2 * 0.1f, num2, color, color.a, 13);
            }
            else
            {
                float alpha = uie.m_alpha;
                //color = Color.Lerp(UIManager.m_col_ui5, UIManager.m_col_ui6, UnityEngine.Random.Range(0f, 0.6f * UIElement.FLICKER));
                //color.a = alpha * ((!fade) ? 1f : 0.3f);
                //UIManager.DrawQuadBarHorizontal(pos, 22f, 22f, drawWidth, color, 7);
                color = UIManager.m_col_ui2;
                color.a = alpha * ((!fade) ? 1f : 0.5f);
            }
            if (fade)
            {
                color.a *= 0.5f;
                uie.DrawStringSmallOverrideAlpha(s, pos, text_size, so, color, 500f * width);
            }
            else
            {
                uie.DrawStringSmallOverrideAlpha(s, pos, text_size, so, color, 500f * width);
            }
        }

    }

    [HarmonyPatch(typeof(MenuManager), "Update")]
    class MPServerBrowser_MenuManager_Update
    {

        private static void Postfix(ref float ___m_menu_state_timer)
        {
            if (MenuManager.m_menu_state == MPServerBrowser.msServerBrowser)
                MPServerBrowserUpdate(ref ___m_menu_state_timer);
        }

        private static MethodInfo _MenuManager_CheckPaging_Method = typeof(MenuManager).GetMethod("CheckPaging", BindingFlags.NonPublic | BindingFlags.Static);
        private static FieldInfo _InternetMatch_ServerAddress_Field = typeof(GameManager).Assembly.GetType("InternetMatch").GetField("ServerAddress", BindingFlags.Static | BindingFlags.Public);
        private static void MPServerBrowserUpdate(ref float m_menu_state_timer)
        {
            UIManager.MouseSelectUpdate();
            switch (MenuManager.m_menu_sub_state)
            {
                case MenuSubState.INIT:
                    if (!MPServerBrowser.browserCoroutineActive)
                        GameManager.m_gm.StartCoroutine(MPServerBrowser.Update());
                    if (m_menu_state_timer > 0.25f)
                    {
                        //Debug.Log("MPServerBrowser Init " + DateTime.Now);
                        GameplayManager.SetGameType(GameType.MULTIPLAYER);
                        MPInternet.Enabled = true;
                        MenuManager.m_game_paused = false;
                        GameplayManager.DifficultyLevel = 3;
                        PlayerShip.DeathPaused = false;
                        if (!Overload.NetworkManager.IsHeadless())
                        {
                            Action<string, string> callback = delegate (string error, string player_id)
                            {
                                if (error != null)
                                {
                                    NetworkMatch.SetPlayerId("00000000-0000-0000-0000-000000000000");
                                }
                                else
                                {
                                    //Debug.Log("MPServerBrowser: Set player id to " + player_id);
                                    NetworkMatch.SetPlayerId(player_id);
                                }
                            };
                            NetworkMatch.GetMyPlayerId(PilotManager.PilotName, callback);
                        }
                        MenuManager.m_mp_lan_match = true;
                        MenuManager.m_mp_private_match = true;
                        NetworkMatch.SetNetworkGameClientMode(NetworkMatch.NetworkGameClientMode.Invalid);
                        MenuManager.ClearMpStatus();

                        MPServerBrowser.selectedItem = null;
                        UIManager.CreateUIElement(UIManager.SCREEN_CENTER, 7000, MPServerBrowser.uiServerBrowser);
                        MPServerBrowser.UpdateList();
                        MenuManager.m_menu_sub_state = MenuSubState.ACTIVE;
                        m_menu_state_timer = 0f;
                        MenuManager.SetDefaultSelection(0);
                    }
                    break;
                case MenuSubState.ACTIVE:
                    UIManager.ControllerMenu();
                    Controls.m_disable_menu_letter_keys = false;
                    int menu_micro_state = MenuManager.m_menu_micro_state;

                    if (m_menu_state_timer > 0.25f) //
                    {
                        bool flag = false;
                        var args = new object[] {  MenuManager.m_list_items_first, MenuManager.m_list_items_last,
                                MenuManager.m_list_items_total_count, MenuManager.m_list_items_max_per_page };
                        flag = (bool)_MenuManager_CheckPaging_Method.Invoke(null, args);

                        if (MenuManager.m_list_items_first != (int)args[0])
                            m_menu_state_timer = 0f;

                        MenuManager.m_list_items_first = (int)args[0];
                        MenuManager.m_list_items_last = (int)args[1];


                        if (!flag)
                        {
                            if (UIManager.PushedSelect(100) || (MenuManager.option_dir && UIManager.PushedDir()))
                            {
                                if (UIManager.m_menu_selection == 100)
                                {
                                    MenuManager.PlaySelectSound(1f);
                                    m_menu_state_timer = 0f;
                                    UIManager.DestroyAll(false);
                                    MenuManager.m_menu_state = 0;
                                    MenuManager.m_menu_micro_state = 0;
                                    MenuManager.m_menu_sub_state = MenuSubState.BACK;
                                }
                                else if (UIManager.m_menu_selection >= 20000)
                                {
                                    // Status action
                                    MPServerBrowser.selectedItem = MPServerBrowser.Items[UIManager.m_menu_selection - 20000];
                                    MenuManager.PlaySelectSound(1f);
                                    Application.OpenURL("https://tracker.otl.gg/game/" + MPServerBrowser.selectedItem.ip);
                                }
                                else if (UIManager.m_menu_selection >= 10000)
                                {
                                    // Far right action
                                    MPServerBrowser.selectedItem = MPServerBrowser.Items[UIManager.m_menu_selection - 10000];
                                    MenuManager.PlaySelectSound(1f);
                                    m_menu_state_timer = 0f;
                                    UIManager.DestroyAll(false);

                                    if (MPServerBrowser.selectedItem.online)
                                    {
                                        if (MPServerBrowser.selectedItem.status == MPServerBrowserItem.BrowserItemStatus.INLOBBY || (MPServerBrowser.selectedItem.status == MPServerBrowserItem.BrowserItemStatus.PLAYING && MPServerBrowser.selectedItem.jip))
                                        {
                                            // Join
                                            UIManager.DestroyAll(false);
                                            MPInternet.MenuPassword = MPServerBrowser.selectedItem.ip;

                                            // temporary show password field for non-jip matches because old server won't send hasPassword
                                            if (MPServerBrowser.selectedItem.hasPassword || !MPServerBrowser.selectedItem.jip)
                                            {
                                                NetworkMatch.SetNetworkGameClientMode(NetworkMatch.NetworkGameClientMode.LocalLAN);
                                                MenuManager.ChangeMenuState(MenuState.MP_LOCAL_MATCH, false);
                                                MenuManager.m_menu_micro_state = 1;
                                                UIManager.m_menu_selection = 0;
                                            }
                                            else
                                            {
                                                NetworkMatch.SetNetworkGameClientMode(NetworkMatch.NetworkGameClientMode.LocalLAN);
                                                NetworkMatch.m_match_req_password = MPServerBrowser.selectedItem.ip;
                                                MPInternet.ServerAddress = MPInternet.FindPasswordAddress(MPServerBrowser.selectedItem.ip, out string msg);
                                                if (Core.GameMod.HasInternetMatch())
                                                {
                                                    _InternetMatch_ServerAddress_Field.SetValue(null, MPInternet.ServerAddress);
                                                }
                                                MenuManager.m_mp_status = Loc.LS("JOINING " + MPInternet.ClientModeName());
                                                NetworkMatch.JoinPrivateLobby(MPInternet.MenuPassword);
                                            }

                                        }
                                        else if (MPServerBrowser.selectedItem.status == MPServerBrowserItem.BrowserItemStatus.READY)
                                        {
                                            // Create
                                            UIManager.DestroyAll(false);
                                            NetworkMatch.SetNetworkGameClientMode(NetworkMatch.NetworkGameClientMode.LocalLAN);
                                            MPInternet.MenuPassword = MPServerBrowser.selectedItem.ip;
                                            MenuManager.ChangeMenuState(MenuState.MP_LOCAL_MATCH, false);
                                            UIManager.m_menu_selection = 1;
                                            MenuManager.m_menu_state = MenuState.MP_LOCAL_MATCH;
                                            MenuManager.PlaySelectSound(1f);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    break;
                case MenuSubState.BACK:
                    if (m_menu_state_timer > 0.25f)
                    {
                        UIManager.DestroyAll(false);
                        m_menu_state_timer = 0f;
                        MenuManager.m_menu_state = 0;
                        MenuManager.m_menu_sub_state = 0;
                        MenuManager.m_menu_micro_state = 0;
                        MenuManager.ChangeMenuState(MenuState.MP_MENU, false);
                    }
                    break;
                case MenuSubState.START:
                    if (m_menu_state_timer > 0.25f)
                    {

                    }
                    break;
            }
        }

    }

    [HarmonyPatch(typeof(MenuManager), "MpMatchSetup")]
    class MPServerBrowser_MenuManager_MpMatchSetup
    {

        private static void MpMatchSetupMicrostate()
        {
            if (MPServerBrowser.selectedItem != null)
            {
                if (MPServerBrowser.selectedItem.maxPlayers > 0)
                {
                    // Join
                    MenuManager.m_menu_micro_state = 1;
                }
                else
                {
                    // Create
                    MenuManager.m_menu_micro_state = 2;
                }
            }
        }

        private static FieldInfo _MenuManager_m_back_stack_Field = AccessTools.Field(typeof(MenuManager), "m_back_stack");
        private static FieldInfo _MenuManager_m_menu_state_timer_Field = AccessTools.Field(typeof(MenuManager), "m_menu_state_timer");
        private static void MpBackButton()
        {
            switch (MenuManager.m_menu_sub_state)
            {
                case MenuSubState.BACK:
                    if (UIManager.PushedSelect(100))
                    {
                        Debug.Log("Pushed back");
                        Stack<MenuState> m_back_stack = (Stack<MenuState>)_MenuManager_m_back_stack_Field.GetValue(null);
                        _MenuManager_m_menu_state_timer_Field.SetValue(null, 0f);
                        UIManager.DestroyAll(false);
                        if (m_back_stack.Peek() == MenuState.MP_MENU)
                        {
                            MenuManager.ChangeMenuState(MenuState.MP_MENU, false);
                        }
                        else
                        {
                            MenuManager.ChangeMenuState(MPServerBrowser.msServerBrowser, false);
                            MenuManager.m_menu_state = 0;
                            MenuManager.m_menu_sub_state = 0;
                            MenuManager.m_menu_micro_state = 0;
                        }

                    }
                    return;
            }

        }

        private static void Postfix(ref float ___m_menu_state_timer)
        {
            switch (MenuManager.m_menu_sub_state)
            {
                case MenuSubState.ACTIVE:
                    switch (UIManager.m_menu_selection)
                    {
                        case 303:
                            Controls.m_disable_menu_letter_keys = false;
                            Controls.m_disable_menu_letter_keys = true;
                            ProcessInputField(ref MPServerBrowser.mms_match_notes, Loc.LS("MATCH NOTES"), false, ref ___m_menu_state_timer);
                            break;
                    }
                    break;
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var mpServerBrowser_MenuManager_MpMatchSetup_MpBackButton_Method = AccessTools.Method(typeof(MPServerBrowser_MenuManager_MpMatchSetup), "MpBackButton");
            var mpServerBrowser_MenuManager_MpMatchSetup_MpMatchSetupMicrostate_Method = AccessTools.Method(typeof(MPServerBrowser_MenuManager_MpMatchSetup), "MpMatchSetupMicrostate");

            int state = 0;
            foreach (var code in codes)
            {
                // Allow back button to return to browser if cancelling
                if (code.opcode == OpCodes.Ldsfld && ((FieldInfo)code.operand).Name == "m_updating_pm_settings")
                {
                    List<Label> labels = code.labels;
                    code.labels = new List<Label>();
                    yield return new CodeInstruction(OpCodes.Call, mpServerBrowser_MenuManager_MpMatchSetup_MpBackButton_Method) { labels = labels };
                }

                // MpMatchSetup init always sets m_menu_micro_state = 0, override if initiated by browser select
                if (state == 0 && code.opcode == OpCodes.Ldsfld &&
                    (((FieldInfo)code.operand).Name == "_mms_match_password" || ((FieldInfo)code.operand).Name == "mms_match_password"))
                {
                    state = 1;
                    yield return new CodeInstruction(OpCodes.Call, mpServerBrowser_MenuManager_MpMatchSetup_MpMatchSetupMicrostate_Method);
                }

                yield return code;
            }
        }

        private static bool ProcessInputField(ref string s, string title, bool hide, ref float m_menu_state_timer)
        {
            foreach (char c in Input.inputString)
            {
                if (c == '\b')
                {
                    if (s.Length != 0)
                    {
                        s = s.Substring(0, s.Length - 1);
                        SFXCueManager.PlayRawSoundEffect2D(SoundEffect.hud_notify_bot_died, 0.4f, UnityEngine.Random.Range(-0.15f, -0.05f), 0f, false);
                    }
                }
                else
                {
                    if (c == '\n' || c == '\r')
                    {
                        m_menu_state_timer = 0f;
                        s = s.Trim();
                        MenuManager.SetDefaultSelection((MenuManager.m_menu_micro_state != 1) ? 1 : 0);
                        MenuManager.PlayCycleSound(1f, 1f);
                        return true;
                    }
                    if (MenuManager.IsPrintableChar(c) && s.Length < 64)
                    {
                        s += c;
                        SFXCueManager.PlayRawSoundEffect2D(SoundEffect.hud_notify_bot_died, 0.5f, UnityEngine.Random.Range(0.1f, 0.2f), 0f, false);
                    }
                }
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawMpMatchSetup")]
    class MPServerBrowser_UIElement_DrawMpMatchSetup
    {
        private static void DrawMatchNotes(UIElement uie, ref Vector2 position)
        {
            position.y += 62f;
            SelectAndDrawTextEntry(uie, "MATCH NOTES", MPServerBrowser.mms_match_notes, position, 303, 1.5f, false);
        }

        private static void DrawFriendlyFire(UIElement uie, ref Vector2 position)
        {
            uie.SelectAndDrawStringOptionItem(Loc.LS("FRIENDLY FIRE"), position, 3, MenuManager.GetMMSFriendlyFire(), string.Empty, 1f, MenuManager.mms_mode == MatchMode.ANARCHY);
            position.y += 62f;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var mpServerBrowser_UIElement_DrawMpMatchSetup_DrawMatchNotes_Method = AccessTools.Method(typeof(MPServerBrowser_UIElement_DrawMpMatchSetup), "DrawMatchNotes");
            var mpServerBrowser_UIElement_DrawMpMatchSetup_DrawFriendlyFire_Method = AccessTools.Method(typeof(MPServerBrowser_UIElement_DrawMpMatchSetup), "DrawFriendlyFire");

            int state = 0;

            foreach (var code in codes)
            {

                // Check for Friendly Fire select
                if (code.opcode == OpCodes.Ldstr && (string)code.operand == "FRIENDLY FIRE")
                    state = 1;

                // Skip up until Time Limit (move FF to Advanced)
                if (state == 1 && !(code.opcode == OpCodes.Ldstr && (string)code.operand == "TIME LIMIT"))
                    continue;

                if (code.opcode == OpCodes.Ldstr && (string)code.operand == "TIME LIMIT")
                    state = 2;

                // Add Match Notes above Advanced
                if (code.opcode == OpCodes.Ldstr && (string)code.operand == "ADVANCED SETTINGS")
                {
                    state = 3;
                    yield return new CodeInstruction(OpCodes.Ldloca, 0);
                    yield return new CodeInstruction(OpCodes.Call, mpServerBrowser_UIElement_DrawMpMatchSetup_DrawMatchNotes_Method);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                }

                // Move Advanced Match section up slightly
                if (code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 155)
                    code.operand = 250f;

                yield return code;
            }
        }

        private static FieldInfo _UIElement_m_cursor_blink_timer_Field = AccessTools.Field(typeof(UIElement), "m_cursor_blink_timer");
        private static FieldInfo _UIElement_m_cursor_on_Field = AccessTools.Field(typeof(UIElement), "m_cursor_on");
        private static void SelectAndDrawTextEntry(UIElement uie, string label, string text, Vector2 pos, int selection, float width_scale = 1.5f, bool fade = false)
        {
            float width = 500f * width_scale;
            int quad_index = UIManager.m_quad_index;
            if (!fade)
            {
                uie.TestMouseInRect(pos, width * 0.5f + 22f, 24f, selection, true);
            }
            bool selected = UIManager.m_menu_selection == selection;

            Color label_color;
            var text_width = 244f;
            var text_right = 2f;
            float alpha8 = 1f - Mathf.Pow(1f - uie.m_alpha, 8f);
            if (selected)
            {
                Color bar_color = Color.Lerp(UIManager.m_col_ui5, UIManager.m_col_ui6, UnityEngine.Random.Range(0f, 0.2f * UIElement.FLICKER) + UIManager.m_select_flash * 0.3f);
                bar_color.a = alpha8;
                var bar_width = width - text_width - text_right;
                var rest_width = width - bar_width;
                // highlight left
                UIManager.DrawQuadBarHorizontal(pos - Vector2.right * (width - bar_width + 22f + 18f - 3f) * 0.5f, 0f, 22f, bar_width + 4f, bar_color, 12);
                // highlight around text field
                UIManager.DrawQuadBarHorizontal(pos + Vector2.right * (bar_width + 22f - 18f) * 0.5f - Vector2.up * 18f, 0f, 2f, rest_width + 22f + 18f, bar_color, 12);
                UIManager.DrawQuadBarHorizontal(pos + Vector2.right * (bar_width + 22f - 18f) * 0.5f + Vector2.up * 18f, 0f, 2f, rest_width + 22f + 18f, bar_color, 12);
                UIManager.DrawQuadBarHorizontal(pos + Vector2.right * (width - text_right + 20f * 2) * 0.5f, 0f, 22f, text_right + 4f, bar_color, 12);
                label_color = UIManager.m_col_ub3;
                label_color.a = alpha8;
            }
            else
            {
                label_color = UIManager.m_col_ui0;
                label_color.a = uie.m_alpha;
            }

            float alpha6 = 1f - Mathf.Pow(1f - uie.m_alpha, 6f);
            Color border_color = Color.Lerp(UIManager.m_col_ui5, UIManager.m_col_ui6, UnityEngine.Random.Range(0f, 0.5f * UIElement.FLICKER));
            border_color.a = alpha6;
            UIManager.DrawQuadBarHorizontal(pos, 22f, 22f, width, border_color, 7);

            Color text_color = UIManager.m_col_ui2;
            text_color.a = uie.m_alpha;
            uie.DrawStringSmallOverrideAlpha(label, pos - Vector2.right * (width * 0.5f + 15f), 0.75f, StringOffset.LEFT, label_color, width - 270f);
            pos.x += (width - text_width - text_right) * 0.5f;
            uie.DrawOutlineBackdrop(pos, 17f, text_width, text_color, 2);

            float m_cursor_blink_timer = (float)_UIElement_m_cursor_blink_timer_Field.GetValue(uie);
            bool m_cursor_on = (bool)_UIElement_m_cursor_on_Field.GetValue(uie);
            m_cursor_blink_timer -= RUtility.FRAMETIME_UI;
            if (m_cursor_blink_timer < 0f)
            {
                m_cursor_blink_timer += 0.25f;
                m_cursor_on = !m_cursor_on;
                _UIElement_m_cursor_on_Field.SetValue(uie, m_cursor_on);
            }
            _UIElement_m_cursor_blink_timer_Field.SetValue(uie, m_cursor_blink_timer);
            bool show_cursor = m_cursor_on && selected;

            float scale = 0.5f * 20f;
            var textCursor = text + "_";
            float stringWidth = UIManager.GetStringWidth(textCursor, scale);
            DrawStringAlignCenterWidth(show_cursor ? textCursor : text, pos, scale, text_color, stringWidth, 200f + 22f * 2 + 22f);

            if (fade)
            {
                UIManager.PreviousQuadsAlpha(quad_index, 0.3f);
            }
        }

        private static void DrawStringAlignCenterWidth(string s, Vector2 position, float scale, Color c, float stringWidth, float max_width = -1f)
        {
            Vector2 scl_pos = position;
            position.x -= ((!(max_width > 0f)) ? stringWidth : ((!(max_width < stringWidth)) ? stringWidth : max_width)) * 0.5f;
            UIManager.DrawStringScaled(s, position, scl_pos, scale, c, max_width, stringWidth, 0.5f);
        }

    }
}
