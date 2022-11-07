using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using GameMod.Metadata;
using Newtonsoft.Json;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod.ModdedMenus.ServerBrowser {
    [Mod(Mods.ServerBrowser)]
    public static class ServerBrowser {
        private const string browserUrl = "https://tracker.otl.gg/api/browser";
        private const int refreshTime = 10000; // 10s

        public const MenuState msServerBrowser = (MenuState)75;
        public const UIElementType uiServerBrowser = (UIElementType)89;

        private static DateTime lastUpdated = new DateTime(1970, 1, 1);
        private static bool isRefreshing = false;

        public static string mms_match_notes = "";
        public static List<ServerItem> Items = new List<ServerItem>();
        public static ServerItem selectedItem { get; set; }
        public static bool browserCoroutineActive = false;

        public enum BrowserItemStatus {
            OFFLINE,
            READY,
            INLOBBY,
            PLAYING
        }

        private static bool menuActive {
            get {
                return MenuManager.m_menu_state == msServerBrowser;
            }
        }

        private static IEnumerator GetTrackerData(string url) {
            UnityWebRequest www = UnityWebRequest.Get(url);
            yield return www.SendWebRequest();

            lastUpdated = DateTime.UtcNow;
            isRefreshing = false;

            if (www.isNetworkError || www.isHttpError) {
                Debug.Log(www.error);
                yield break;
            }

            JsonSerializerSettings settings = new JsonSerializerSettings {
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc
            };
            List<TrackerEntry> results = JsonConvert.DeserializeObject<List<TrackerEntry>>(www.downloadHandler.text, settings);
            List<ServerItem> newItems = new List<ServerItem>();
            foreach (var result in results.Where(x => x.server.online)) {
                var existing = Items.FirstOrDefault(x => x.ip == result.server.ip && x.port == result.server.port);

                if (existing == null) {
                    // Add
                    BrowserItemStatus status;
                    if (result.game == null) {
                        // No game/lobby
                        if (result.server.online) {
                            status = BrowserItemStatus.READY;
                        } else {
                            status = BrowserItemStatus.OFFLINE;
                        }
                    } else {
                        // Game or lobby going
                        if (result.game.inLobby) {
                            status = BrowserItemStatus.INLOBBY;
                        } else {
                            status = BrowserItemStatus.PLAYING;
                        }
                    }

                    IPAddress addr = IPAddress.Any;
                    try {
                        if (!IPAddress.TryParse(result.server.ip, out addr)) {
                            var addrs = Dns.GetHostAddresses(result.server.ip);
                            addr = addrs == null || addrs.Length == 0 ? null : addrs[0];
                        }
                    } catch (Exception) {
                        //Debug.LogException(ex);
                    }

                    newItems.Add(new ServerItem {
                        ip = result.server.ip,
                        port = result.server.port,
                        name = result.server.name,
                        version = result.server.version?.Replace("olmod ", ""),
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
                } else {
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
                    if (result.game == null) {
                        // No game/lobby
                        if (result.server.online) {
                            existing.status = BrowserItemStatus.READY;
                        } else {
                            existing.status = BrowserItemStatus.OFFLINE;
                        }
                    } else {
                        // Game or lobby going
                        if (result.game.inLobby) {
                            existing.status = BrowserItemStatus.INLOBBY;
                        } else {
                            existing.status = BrowserItemStatus.PLAYING;
                        }
                    }
                }
            }

            foreach (var item in Items) {
                if (!item.online)
                    item.ping = 0;
            }
            Items.AddRange(newItems);
            Items = Items.OrderByDescending(x => x.currentPlayers).ThenByDescending(x => x.status).ThenBy(x => x.name).ToList();
            UpdateList();
        }

        private static byte[] CreatePingPacket(long time, int idx) {
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

        private static bool CheckPingHash(byte[] packet) {
            var hash = BitConverter.ToUInt32(packet, 8);
            Array.Copy(BitConverter.GetBytes(0), 0, packet, 8, 4);
            var srcHash = xxHashSharp.xxHash.CalculateHash(packet);
            return srcHash == hash;
        }

        private static long PingGetTime(byte[] packet) {
            return BitConverter.ToInt64(packet, 19 + 4 + 4);
        }

        private struct ReceivedPacketInfo {
            public byte[] packet;
            public IPEndPoint endPoint;
            public long time;
        }

        public static void UpdateList() {
            if (!menuActive)
                return;

            MenuManager.m_list_items_total_count = Items.Count();
            MenuManager.m_list_items_max_per_page = Math.Min(MenuManager.m_list_items_total_count, 12);
            while (MenuManager.m_list_items_first > MenuManager.m_list_items_total_count) {
                MenuManager.m_list_items_first -= 12;
            }
            MenuManager.m_list_items_last = Mathf.Min(MenuManager.m_list_items_first + MenuManager.m_list_items_max_per_page - 1, MenuManager.m_list_items_total_count - 1);
            MenuManager.m_list_item_paging = (MenuManager.m_list_items_total_count > 12);
        }

        public static IEnumerator Update() {
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
                var receivedPacket = new ReceivedPacketInfo {
                    packet = inPacket,
                    endPoint = endPoint,
                    time = DateTime.UtcNow.Ticks
                };
                lock (receivedPackets) {
                    receivedPackets.Add(receivedPacket);
                }
                socket.BeginReceive(receiver, arSocket);
            });
            socket.BeginReceive(receiver, socket);

            while (menuActive) {
                List<ReceivedPacketInfo> newPackets = null;
                lock (receivedPackets) {
                    if (receivedPackets.Any()) {
                        newPackets = new List<ReceivedPacketInfo>();
                        newPackets.AddRange(receivedPackets);
                        receivedPackets.Clear();
                    }
                }
                if (newPackets != null) {
                    foreach (var packetInfo in newPackets) {
                        var endPoint = packetInfo.endPoint;
                        var entry = Items.FirstOrDefault(x => x.addr.Equals(endPoint.Address)); // && x.port == endPoint.Port);
                        if (entry == null)
                            continue;
                        var orgTime = PingGetTime(packetInfo.packet);
                        entry.ping = (int)((packetInfo.time - orgTime) / 10000);
                    }
                }

                var now = DateTime.UtcNow;
                if (now.Subtract(lastUpdated).TotalMilliseconds > refreshTime && !isRefreshing) {
                    // Update servers from json
                    isRefreshing = true;
                    GameManager.m_gm.StartCoroutine(GetTrackerData(browserUrl));
                }

                foreach (var entry in Items.Where(x => x.online && now.Subtract(x.lastPingRequest).TotalMilliseconds > 5000)) {
                    try {
                        entry.lastPingRequest = DateTime.UtcNow;

                        var packet = CreatePingPacket(DateTime.UtcNow.Ticks, 1);
                        socket.BeginSend(packet, packet.Length, new IPEndPoint(entry.addr, 8001),
                            (ar) => ((UdpClient)ar.AsyncState).EndSend(ar), socket);
                    } catch (Exception) {
                        //Debug.LogException(ex);
                        //uConsole.Log("Unable to ping " + entry.name);
                    }

                }
                yield return new WaitForSecondsRealtime(1f);
            }
            browserCoroutineActive = false;
        }
    }
}
