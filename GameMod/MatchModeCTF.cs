using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Overload;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace GameMod {
    public enum FlagState
    {
        HOME, PICKEDUP, LOST
    }
    public enum CTFEvent
    {
        NONE, PICKUP, DROP, RETURN, SCORE, CARRIER_DIED
    }
    public static class CTF
    {
        public const MatchMode MatchModeCTF = MatchMode.NUM;
        public const int TeamCount = 2;
        public static List<GameObject> FlagObjs = new List<GameObject>();
        public static bool[] HasSpawnPoint = new bool[TeamCount];
        public static Vector3[] SpawnPoint = new Vector3[TeamCount];
        public static FlagState[] FlagStates = new FlagState[TeamCount];
        public static Dictionary<NetworkInstanceId, int> PlayerHasFlag = new Dictionary<NetworkInstanceId, int>();
        public static float[] FlagReturnTime = new float[TeamCount];
        public static IEnumerator[] FlagReturnTimer = new IEnumerator[TeamCount];
        public const float ReturnTimeAmountDefault = 30;
        public static float ReturnTimeAmount = ReturnTimeAmountDefault;
        public static bool ShowReturnTimer = false;
        public static Item[] SpawnedFlags = new Item[TeamCount];
        public static bool FlagWasHome = true;
        public static bool CarrierBoostEnabled = true;
        public static object FlagLock = new object();
        private static MethodInfo _Item_ItemIsReachable_Method = typeof(Item).GetMethod("ItemIsReachable", BindingFlags.NonPublic | BindingFlags.Instance);
        public static Dictionary<NetworkInstanceId, CTFStats> PlayerStats = new Dictionary<NetworkInstanceId, CTFStats>();
        public static Dictionary<String, NetworkInstanceId> NameToID = new Dictionary<string, NetworkInstanceId>(); // this is hacky but avoids having to redo the PlayerStats dictionary by pilot name instead of network ID

        public class CTFStats
        {
            public int CarrierKills = 0;
            public int Pickups = 0;
            public int Captures = 0;
            public int Returns = 0;
            public int Drops = 0;
        }

        public static bool IsActive
        {
            get
            {
                return NetworkMatch.GetMode() == CTF.MatchModeCTF && GameplayManager.IsMultiplayerActive;
            }
        }

        public static bool IsActiveServer
        {
            get
            {
                return IsActive && Overload.NetworkManager.IsServer();
            }
        }

        public static bool IsFlagrunner(PlayerShip playerShip)
        {
            return CTF.PlayerHasFlag.ContainsKey(playerShip.c_player.netId);
        }

        public static Color FlagColor(int teamIdx)
        {
            return MPTeams.TeamColor(MPTeams.AllTeams[teamIdx], teamIdx == 1 ? 2 : 3); // make orange a bit darker
        }

        public static Color FlagColorUI(int teamIdx)
        {
            return MPTeams.TeamColor(MPTeams.AllTeams[teamIdx], 1);
        }

        private static void SendToClientOrAll(int conn_id, short msg_type, MessageBase msg)
        {
            if (conn_id == -1)
                NetworkServer.SendToAll(msg_type, msg);
            else
                NetworkServer.SendToClient(conn_id, msg_type, msg);
        }

        public static bool SendCTFPickup(int conn_id, Player player, int flag_id, FlagState state, Item item = null)
        {
            var player_id = player.netId;

            lock (CTF.FlagLock) {
                if (conn_id == -1)
                {
                    if (CTF.FlagStates[flag_id] == FlagState.PICKEDUP)
                    {
                        return false;
                    }

                    // Recheck that the object is still there in case another player got the flag first.
                    if (!(bool)_Item_ItemIsReachable_Method.Invoke(item, new object[] { player.c_player_ship.c_mesh_collider }))
                    {
                        return false;
                    }

                    CTF.FlagStates[flag_id] = state;

                    if (CTF.PlayerHasFlag.ContainsKey(player_id)) {
                        return false;
                    }

                    if (item != null) { //  && item.c_go != null
                        item.m_type = ItemType.NONE;
                        UnityEngine.Object.Destroy(item.c_go);
                    }

                    CTF.PlayerHasFlag.Add(player_id, flag_id);
                }
                else
                {
                    CTF.FlagStates[flag_id] = state;
                }
                SendToClientOrAll(conn_id, MessageTypes.MsgCTFPickup, new PlayerFlagMessage { m_player_id = player_id, m_flag_id = flag_id, m_flag_state = state });
            }

            return true;
        }

        public static bool SendCTFLose(int conn_id, NetworkInstanceId player_id, int flag_id, FlagState state, bool spawnFlagAtHome = false, bool destroyExisting = false)
        {
            lock (CTF.FlagLock) {
                if (CTF.FlagStates[flag_id] != FlagState.PICKEDUP) {
                    return false;
                }

                if (CTF.PlayerHasFlag.ContainsKey(player_id)) {
                    CTF.PlayerHasFlag.Remove(player_id);
                }
                CTF.FlagStates[flag_id] = state;

                if (destroyExisting) {
                    foreach (var item in GameObject.FindObjectsOfType<Item>()) {
                        if (item.m_type == ItemType.KEY_SECURITY && item.m_index == flag_id) {
                            item.m_type = ItemType.NONE;
                            UnityEngine.Object.Destroy(item.gameObject);
                        }
                    }
                }

                if (spawnFlagAtHome) {
                    SpawnAtHome(flag_id);
                }

                if (state == FlagState.LOST) {
                    CTF.FlagReturnTime[flag_id] = Time.time + CTF.ReturnTimeAmount;
                }

                SendToClientOrAll(conn_id, MessageTypes.MsgCTFLose, new PlayerFlagMessage { m_player_id = player_id, m_flag_id = flag_id, m_flag_state = state });
            }

            return true;
        }

        public static bool SendCTFFlagUpdate(int conn_id, NetworkInstanceId player_id, int flag_id, FlagState state, bool spawnFlagAtHome = false, Item item = null)
        {
            lock (CTF.FlagLock) {
                if (conn_id == -1 && CTF.FlagStates[flag_id] == state)
                {
                    return false;
                }

                CTF.FlagStates[flag_id] = state;

                if (spawnFlagAtHome) {
                    SpawnAtHome(flag_id);
                }

                if (item != null) {
                    item.m_type = ItemType.NONE;
                    UnityEngine.Object.Destroy(item.c_go);
                }

                SendToClientOrAll(conn_id, MessageTypes.MsgCTFFlagUpdate, new PlayerFlagMessage { m_player_id = player_id, m_flag_id = flag_id, m_flag_state = state });
            }

            return true;
        }

        public static void FindFlagSpawnPoints()
        {
            // finding spawn points from the goals doesn't work well since the triggers are not always in the goal centers :(
            /*
            SpawnPoint = new Vector3[TeamCount];
            int[] posCount = new int[TeamCount];
            foreach (var x in GameObject.FindObjectsOfType<MonsterBallGoal>())
            {
                int flag = MPTeams.TeamNum(x.m_team);
                if (flag < 0 || flag >= TeamCount)
                    continue;
                var box = x.gameObject.GetComponent<BoxCollider>();
                SpawnPoint[flag] += box.transform.position + box.center;
                posCount[flag]++;
            }
            for (int flag = 0; flag < TeamCount; flag++)
            {
                if (posCount[flag] == 0)
                {
                    Debug.Log("CTF: No goal found for team " + MPTeams.AllTeams[flag]);
                }
                else
                {
                    SpawnPoint[flag] /= posCount[flag];
                    HasSpawnPoint[flag] = true;
                }
            }
            */

            var rest = new Queue<LevelData.SpawnPoint>();
            foreach (var spawn in GameManager.m_level_data.m_player_spawn_points)
            {
                var team_mask = spawn.multiplayer_team_association_mask;
                if (team_mask != 1 && team_mask != 2)
                {
                    rest.AddItem(spawn);
                    continue;
                }
                var team_id = team_mask - 1;
                if (HasSpawnPoint[team_id])
                    continue;
                SpawnPoint[team_id] = spawn.position;
                HasSpawnPoint[team_id] = true;
            }
            for (int team_id = 0; team_id < CTF.TeamCount; team_id++)
                if (!HasSpawnPoint[team_id] && rest.Any())
                {
                    SpawnPoint[team_id] = rest.Dequeue().position;
                    HasSpawnPoint[team_id] = true;
                }

            // remove flag spawns from player spawn points
            GameManager.m_level_data.m_player_spawn_points = GameManager.m_level_data.m_player_spawn_points.Where(x => !SpawnPoint.Contains(x.position)).ToArray();
            //Debug.Log("InitForLevel HasSpawnPoint=" + string.Join(",", HasSpawnPoint.Select(x => x.ToString()).ToArray()));
        }

        public static void InitForMatch()
        {
            PlayerHasFlag.Clear();
            HasSpawnPoint = new bool[TeamCount];
            FlagStates = new FlagState[TeamCount];
            SpawnPoint = new Vector3[TeamCount];
            FlagReturnTime = new float[TeamCount];
            ShowReturnTimer = false;
            PlayerStats = new Dictionary<NetworkInstanceId, CTFStats>();
            NameToID = new Dictionary<string, NetworkInstanceId>();
            foreach (var timer in FlagReturnTimer)
                if (timer != null)
                    GameManager.m_gm.StopCoroutine(timer);
            FlagReturnTimer = new IEnumerator[TeamCount];
            SpawnedFlags = new Item[TeamCount];
            FlagWasHome = true;
        }

        // pickup of flag item
        public static bool Pickup(Player player, Item flagItem)
        {
            var flag = flagItem.m_index;

            if (flag < 0 || flag >= FlagObjs.Count)
                return false;
            var ownFlag = MPTeams.AllTeams[flag] == player.m_mp_team;
            if  (ownFlag && FlagStates[flag] == FlagState.HOME)
            {
                if (CTF.PlayerHasFlag.ContainsKey(player.netId))
                    CTF.Score(player);
                return false;
            }
            if (!ownFlag && (PlayerHasFlag.ContainsKey(player.netId) || PlayerHasFlag.ContainsValue(flag)))
                return false;
            
            var currentState = FlagStates[flag];

            // this also sends to 'client 0' so it'll get processed on the server as well
            CTFEvent evt;
            if (ownFlag) {
                if (!SendCTFFlagUpdate(-1, player.netId, flag, FlagState.HOME, true, flagItem)) {
                    return false;
                }

                evt = CTFEvent.RETURN;
            } else {
                if (!SendCTFPickup(-1, player, flag, FlagState.PICKEDUP, flagItem)) {
                    return false;
                }

                if (!CTF.CarrierBoostEnabled)
                {
                    player.c_player_ship.m_boosting = false;
                    player.c_player_ship.m_boost_overheat_timer = float.MaxValue;
                }
                evt = CTFEvent.PICKUP;
            }

            var msg = evt == CTFEvent.RETURN ? "{0} RETURNS THE {2} FLAG!" :
                currentState == FlagState.HOME ? "{0} ({1}) PICKS UP THE {2} FLAG!" :
                "{0} ({1}) FINDS THE {2} FLAG AMONG SOME DEBRIS!";
            CTF.NotifyAll(evt, string.Format(Loc.LS(msg), player.m_mp_name, MPTeams.TeamName(player.m_mp_team),
                MPTeams.TeamName(MPTeams.AllTeams[flag])), player, flag);
            if (FlagReturnTimer[flag] != null)
            {
                GameManager.m_gm.StopCoroutine(FlagReturnTimer[flag]);
                FlagReturnTimer[flag] = null;
            }
            return true;
        }

        public static void SpawnAtHome(int flag_id)
        {
            if (flag_id >= HasSpawnPoint.Length || !HasSpawnPoint[flag_id])
            {
                Debug.Log("CTF: No home spawn point for flag " + flag_id);
                return;
            }
            CTF.FlagWasHome = true;
            SpawnAt(flag_id, SpawnPoint[flag_id], Vector3.zero);
        }

        public static void SpawnAt(int flag_id, Vector3 pos, Vector3 vel)
        {
            var prefab = FlagObjs[flag_id];
            if (prefab == null)
            {
                Debug.LogWarningFormat("CTF: Missing prefab for flag {0}", flag_id);
                return;
            }
            //Debug.Log("CTF: Spawning flag " + flag_id);

            GameObject flag = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
            if (flag == null)
            {
                Debug.LogWarningFormat("CTF: Failed to instantiate prefab: {0} in CTF.SpawnAt", prefab.name);
                return;
            }

            flag.SetActive(true);
            NetworkServer.Spawn(flag);
            SpawnedFlags[flag_id] = flag.GetComponent<Item>();
            //item.m_spewed = true;
            //item.m_spawn_point = -1;
            SpawnedFlags[flag_id].c_rigidbody.velocity = vel * SpawnedFlags[flag_id].m_push_multiplier;
        }

        public static NetworkHash128 FlagAssetId(int flag)
        {
            var id = "07e810adf1a9f1a9f1a9";
            return NetworkHash128.Parse(id + flag.ToString("x4"));
        }

        public static void SpewFlag(int flag, PlayerShip playerShip)
        {
            Vector3 a = UnityEngine.Random.Range(3f, 5f) * UnityEngine.Random.onUnitSphere;
            SpawnAt(flag, playerShip.c_transform_position + a * 0.05f, a + playerShip.c_rigidbody.velocity * UnityEngine.Random.Range(1f, 2f));
        }

        public static void UpdateFlagColor(GameObject flag, int teamIdx)
        {
            Color c1 = CTF.FlagColor(teamIdx);
            var lightColor = MPTeams.TeamColor(MPTeams.AllTeams[teamIdx], 5);
            foreach (var rend in flag.GetComponentsInChildren<MeshRenderer>())
            {
                rend.sharedMaterial.SetColor("_Color", c1);
                rend.sharedMaterial.SetColor("_EmissionColor", c1);
            }
            var light = flag.GetChildByName("_light").GetComponent<Light>();
            light.color = lightColor;
            light.intensity = 2f;
            light.range = 4f;
            light.bounceIntensity = 0f;

            var keyA2 = flag.GetChildByName("keyA2(Clone)");
            keyA2.GetChildByName("inner_ring001").GetComponent<HighlighterConstant>().color = c1;
            var partRend = keyA2.GetChildByName("outer_ring_004").GetChildByName("_particle1").GetComponent<ParticleSystemRenderer>();
            partRend.sharedMaterial.SetColor("_CoreColor", c1);
            partRend.sharedMaterial.SetColor("_TintColor", c1);
        }

        private static void LogEvent(CTFEvent evt, Player player, MpTeam flag)
        {
            switch (evt)
            {
                case CTFEvent.RETURN:
                    ServerStatLog.AddFlagEvent(player, "Return", flag);
                    break;
                case CTFEvent.PICKUP:
                    ServerStatLog.AddFlagEvent(player, "Pickup", flag);
                    break;
                case CTFEvent.SCORE:
                    ServerStatLog.AddFlagEvent(player, "Capture", flag);
                    break;
            }
        }

        public static void NotifyAll(CTFEvent evt, string message, Player player, int flag)
        {
            Debug.Log("CTF.NotifyAll " + evt);
            NetworkServer.SendToAll(MessageTypes.MsgCTFNotify, new CTFNotifyMessage { m_event = evt, m_message = message,
                m_player_id = player == null ? default(NetworkInstanceId) : player.netId, m_flag_id = flag });
            LogEvent(evt, player, MPTeams.AllTeams[flag]);
        }

        public static void Score(Player player)
        {
            if (NetworkMatch.m_postgame) {
                return;
            }
            if (!PlayerHasFlag.TryGetValue(player.netId, out int flag) || FlagStates[MPTeams.TeamNum(player.m_mp_team)] != FlagState.HOME) {
                return;
            }
            PlayerHasFlag.Remove(player.netId);

            if (!SendCTFLose(-1, player.netId, flag, FlagState.HOME, true)) {
                return;
            }

            if (!CTF.CarrierBoostEnabled)
            {
                player.c_player_ship.m_boost_overheat_timer = 0;
                player.c_player_ship.m_boost_heat = 0;
            }

            NetworkMatch.AddPointForTeam(player.m_mp_team);

            NotifyAll(CTFEvent.SCORE, string.Format(Loc.LS("{0} ({1}) CAPTURES THE {2} FLAG!"), player.m_mp_name, MPTeams.TeamName(player.m_mp_team),
                MPTeams.TeamName(MPTeams.AllTeams[flag])), player, flag);
        }

        public static IEnumerator CreateReturnTimer(int flag)
        {
            yield return new WaitForSeconds(CTF.ReturnTimeAmount);

            if (!IsActiveServer || NetworkMatch.m_match_state != MatchState.PLAYING || CTF.FlagStates[flag] != FlagState.LOST) {
                yield break;
            }

            // this doesn't seem to be necessary here and was breaking stuff so I just turned it off - CC
            /*if (!SendCTFLose(-1, default(NetworkInstanceId), flag, FlagState.HOME, true, true)) {
                yield break;
            }*/

            SendCTFFlagUpdate(-1, NetworkInstanceId.Invalid, flag, FlagState.HOME, true, SpawnedFlags[flag]);
            NotifyAll(CTFEvent.RETURN, string.Format(Loc.LS("LOST {0} FLAG RETURNED AFTER TIMER EXPIRED!"), MPTeams.TeamName(MPTeams.AllTeams[flag])), null, flag);
            FlagReturnTimer[flag] = null;
        }

        public static void Drop(Player player) {
            if (!PlayerHasFlag.TryGetValue(player.netId, out int flag))
                return;
            SendCTFLose(-1, player.netId, flag, FlagState.LOST);
            if (!CTF.CarrierBoostEnabled) {
                player.c_player_ship.m_boost_overheat_timer = 0;
                player.c_player_ship.m_boost_heat = 0;
            }
            NotifyAll(CTFEvent.DROP, string.Format(Loc.LS("{0} ({1}) DROPPED THE {2} FLAG!"), player.m_mp_name, MPTeams.TeamName(player.m_mp_team), MPTeams.TeamName(MPTeams.AllTeams[flag])), player, flag);
            if (FlagReturnTimer[flag] != null)
                GameManager.m_gm.StopCoroutine(FlagReturnTimer[flag]);
            GameManager.m_gm.StartCoroutine(FlagReturnTimer[flag] = CreateReturnTimer(flag));
        }

        public static void SendJoinUpdate(Player player)
        {
            if (!CTF.IsActiveServer)
                return;

            int conn_id = player.connectionToClient.connectionId;
            var inv = NetworkInstanceId.Invalid;

            // sync flag possession
            foreach (var x in CTF.PlayerHasFlag) {
                SendToClientOrAll(conn_id, MessageTypes.MsgCTFJoinUpdate, new CTFJoinUpdateMessage { m_mode = 1, m_flag_id = x.Value, m_destroy = true, m_timer = 0, m_mp_name = "", m_player_id = inv, m_old_id = inv });
                CTF.SendCTFPickup(conn_id, NetworkServer.FindLocalObject(x.Key).GetComponent<Player>(), x.Value, FlagState.PICKEDUP);
            }
            
            // sync any dropped flags and their timers
            for (int flag = 0; flag < TeamCount; flag++)
            {
                if (FlagStates[flag] == FlagState.LOST)
                {
                    SendToClientOrAll(conn_id, MessageTypes.MsgCTFJoinUpdate, new CTFJoinUpdateMessage { m_mode = 1, m_flag_id = flag, m_destroy = true, m_timer = 0f, m_mp_name = "", m_player_id = inv, m_old_id = inv });
                    CTF.SendCTFFlagUpdate(conn_id, NetworkInstanceId.Invalid, flag, FlagStates[flag]);
                    SendToClientOrAll(conn_id, MessageTypes.MsgCTFJoinUpdate, new CTFJoinUpdateMessage { m_mode = 2, m_flag_id = flag, m_destroy = false, m_timer = CTF.FlagReturnTime[flag] - Time.time, m_mp_name = "", m_player_id = inv, m_old_id = inv });
                }
                else
                {
                    SendToClientOrAll(conn_id, MessageTypes.MsgCTFJoinUpdate, new CTFJoinUpdateMessage { m_mode = 1, m_flag_id = flag, m_destroy = false, m_timer = 0f, m_mp_name = "", m_player_id = inv, m_old_id = inv });
                }
            }

            // sync the stats and make the client update the NameToID dictionary 
            NetworkInstanceId old;
            if (!CTF.NameToID.TryGetValue(player.m_mp_name, out old))
            {
                old = NetworkInstanceId.Invalid;
            }
            SendToClientOrAll(-1, MessageTypes.MsgCTFJoinUpdate, new CTFJoinUpdateMessage { m_mode = 0, m_flag_id = 0, m_destroy = false, m_timer = 0f, m_mp_name = player.m_mp_name, m_player_id = player.netId, m_old_id = old });
        }

        // return Player object for given net id if it should show an effect, i.e. not local player or headless
        public static Player FindPlayerForEffect(NetworkInstanceId m_player_id)
        {
            if (m_player_id == GameManager.m_local_player.netId || GameplayManager.IsDedicatedServer())
                return null;
            var playerObj = ClientScene.FindLocalObject(m_player_id);
            if (playerObj == null)
                return null;
            return playerObj.GetComponent<Player>();
        }

        public static void UpdateShipEffects(Player player)
        {
            if (player == null || !CTF.PlayerHasFlag.ContainsKey(player.netId))
                return;

            CTFCarrierGlow.ResetMats();
            PlayerDisableRing(player);
            PlayerEnableRing(player, CTF.PlayerHasFlag[player.netId]);
        }

        public static void PlayerEnableRing(Player player, int flag_id)
        {
            if (player == null)
                return;
            var flag = CTF.FlagObjs[flag_id];
            var orgPart = flag.GetChildByNameRecursive("_particle1");
            if (orgPart == null)
                return;
            if (player.c_player_ship.gameObject.GetChildByName("carrier_ring") != null)
                return;
            var part = UnityEngine.Object.Instantiate<GameObject>(orgPart, player.c_player_ship.transform);
            var main = part.GetComponent<ParticleSystem>().main;
            main.scalingMode = ParticleSystemScalingMode.Local;
            part.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            part.transform.localRotation = Quaternion.Euler(0, 0, 90f);
            part.name = "carrier_ring";
            part.SetActive(true);
        }

        public static void PlayerDisableRing(Player player)
        {
            if (player == null)
                return;
            var part = player.c_player_ship.gameObject.GetChildByName("carrier_ring");
            if (part != null)
                UnityEngine.Object.Destroy(part);
        }

        public static void DrawFlagState(UIElement uie, Vector2 pos, float m_alpha, int flag)
        {
            var state = CTF.FlagStates[flag];
            if (state == FlagState.PICKEDUP)
            {
                var pickedupTeamIdx = 1 - flag;
                UIManager.DrawSpriteUIRotated(pos, 0.4f, 0.4f, Mathf.PI / 2f,
                    MPTeams.TeamColor(MPTeams.AllTeams[pickedupTeamIdx], 4), m_alpha, (int)AtlasIndex0.RING_MED0);

                UIManager.DrawSpriteUIRotated(pos, 0.18f, 0.18f, Mathf.PI / 2f,
                    MPTeams.TeamColor(MPTeams.AllTeams[flag], 4) * 0.5f, m_alpha, (int)AtlasIndex0.ICON_SECURITY_KEY1);
            }
            else
            {
                UIManager.DrawSpriteUIRotated(pos, 0.3f, 0.3f, Mathf.PI / 2f,
                    MPTeams.TeamColor(MPTeams.AllTeams[flag], 4) * (state == FlagState.LOST ? 0.2f : 1f), m_alpha, (int)AtlasIndex0.ICON_SECURITY_KEY1);
                if (state == FlagState.LOST && CTF.ShowReturnTimer)
                {
                    pos.y += 32f;
                    pos.x += 12f;
                    float t = CTF.FlagReturnTime[flag] - Time.time + 1f; // 'round up'
                    if (t < 0)
                        t = 0;
                    uie.DrawDigitsTimeNoHours(pos, t, 0.45f, MPTeams.TeamColor(MPTeams.AllTeams[flag], 4), m_alpha);
                }
            }
        }

        public static void DrawFlags(UIElement instance, Vector2 pos, float m_alpha)
        {
            for (int i = 0; i < CTF.TeamCount; i++)
            {
                DrawFlagState(instance, pos, m_alpha, i);
                pos.x += 60;
            }
        }

        public class PlayerStatesMessage : MessageBase
        {
            public override void Serialize(NetworkWriter writer)
            {
                writer.Write((byte)0); // version
                writer.WritePackedUInt32((uint)m_player_states.Count);
                foreach (var pl_state in m_player_states)
                {
                    writer.Write(pl_state.Key);
                    writer.WritePackedUInt32((uint)pl_state.Value.CarrierKills);
                    writer.WritePackedUInt32((uint)pl_state.Value.Pickups);
                    writer.WritePackedUInt32((uint)pl_state.Value.Captures);
                    writer.WritePackedUInt32((uint)pl_state.Value.Returns);
                    writer.WritePackedUInt32((uint)pl_state.Value.Drops);
                }
            }
            public override void Deserialize(NetworkReader reader)
            {
                var version = reader.ReadByte();
                var n = reader.ReadPackedUInt32();
                m_player_states = new Dictionary<NetworkInstanceId, CTF.CTFStats>();
                for (int i = 0; i < n; i++)
                    m_player_states.Add(reader.ReadNetworkId(), new CTF.CTFStats
                    {
                        CarrierKills = (int)reader.ReadPackedUInt32(),
                        Pickups = (int)reader.ReadPackedUInt32(),
                        Captures = (int)reader.ReadPackedUInt32(),
                        Returns = (int)reader.ReadPackedUInt32(),
                        Drops = (int)reader.ReadPackedUInt32()
                    });
            }
            public Dictionary<NetworkInstanceId, CTF.CTFStats> m_player_states;
        }
    }

    [HarmonyPatch(typeof(MenuManager), "GetMMSGameMode")]
    class CTFGetMMSGameMode {
        private static bool Prefix(ref string __result)
        {
            if (MenuManager.mms_mode == CTF.MatchModeCTF)
            {
                __result = "CTF";
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(NetworkSpawnItem), "RegisterSpawnHandlers")]
    class CTFRegisterSpawnHandlers
    {
        private static Dictionary<NetworkHash128, GameObject> m_registered_prefabs = new Dictionary<NetworkHash128, GameObject>();
        private static FieldInfo _NetworkIdentity_m_AssetId_Field = typeof(NetworkIdentity).GetField("m_AssetId", BindingFlags.NonPublic | BindingFlags.Instance);

        private static GameObject NetworkSpawnItemHandler(Vector3 pos, NetworkHash128 asset_id)
        {
            //Debug.Log("CTF: client " + NetworkMatch.m_my_lobby_id + " flag spawning " + asset_id);
            GameObject prefabFromAssetId = m_registered_prefabs[asset_id];
            if (prefabFromAssetId == null)
            {
                Debug.LogErrorFormat("CTF: Error looking up item prefab with asset_id {0}", asset_id.ToString());
                return null;
            }
            GameObject gameObject = UnityEngine.Object.Instantiate(prefabFromAssetId, pos, Quaternion.identity);
            if (gameObject == null)
            {
                Debug.LogErrorFormat("CTF: Error instantiating item prefab {0}", prefabFromAssetId.name);
                return null;
            }
            gameObject.SetActive(true);
            //Debug.Log("Spawning flag " + asset_id + " active " + gameObject.activeSelf + " seg " + gameObject.GetComponent<Item>().m_current_segment);

            var netId = gameObject.GetComponent<NetworkIdentity>();
            _NetworkIdentity_m_AssetId_Field.SetValue(netId, asset_id);
            CTF.UpdateFlagColor(gameObject, int.Parse(asset_id.ToString().Substring(asset_id.ToString().Length-1)));
            //Debug.Log("post spawn assetid " + gameObject.GetComponent<NetworkIdentity>().assetId);

            return gameObject;
        }

        private static void NetworkUnspawnItemHandler(GameObject spawned)
        {
            UnityEngine.Object.Destroy(spawned);
        }

        private static void Postfix()
        {
            // for some reason using entity_item_security_key directly doesn't work well in MP,
            // so take the relatively simple cloak powerup and give it the appearance and type of a security key
            GameObject prefab = PrefabManager.item_prefabs[(int)ItemPrefab.entity_item_cloak];
            var prefabkeyA2 = PrefabManager.item_prefabs[(int)ItemPrefab.entity_item_security_key].GetChildByName("keyA2");
            for (int i = 0; i < CTF.TeamCount; i++)
            {
                GameObject flag = UnityEngine.Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
                UnityEngine.Object.Destroy(flag.GetChildByName("cloak_ball (1)"));
                var color = CTF.FlagColor(i);
                var lightColor = MPTeams.TeamColor(MPTeams.AllTeams[i], 5);
                Material newMat = null;
                var keyA2 = UnityEngine.Object.Instantiate(prefabkeyA2, flag.transform);
                foreach (var rend in keyA2.GetComponentsInChildren<MeshRenderer>())
                {
                    if (newMat == null) {
                        newMat = new Material(rend.sharedMaterial.shader);
                        newMat.CopyPropertiesFromMaterial(rend.sharedMaterial);
                        newMat.SetColor("_Color", color);
                        newMat.SetColor("_EmissionColor", color);
                    }
                    rend.sharedMaterial = newMat;
                }
                var light = flag.GetChildByName("_light").GetComponent<Light>();
                light.color = lightColor;
                //light.intensity = 4.455539f;
                light.intensity = 2f;
                light.range = 4f;
                light.bounceIntensity = 0f;
                keyA2.GetChildByName("inner_ring001").GetComponent<HighlighterConstant>().color = color;
                var partRend = keyA2.GetChildByName("outer_ring_004").GetChildByName("_particle1").GetComponent<ParticleSystemRenderer>();
                var newPartMat = new Material(partRend.sharedMaterial.shader);
                newPartMat.CopyPropertiesFromMaterial(partRend.sharedMaterial);
                newPartMat.SetColor("_CoreColor", color);
                newPartMat.SetColor("_TintColor", color);
                partRend.sharedMaterial = newPartMat;

                if (flag == null)
                {
                    Debug.LogWarningFormat("CTF: Failed to instantiate prefab: {0} in RegisterSpawnHandlers", prefab.name);
                    return;
                }
                var assetId = CTF.FlagAssetId(i);
                var netId = flag.GetComponent<NetworkIdentity>();
                _NetworkIdentity_m_AssetId_Field.SetValue(netId, assetId);
                UnityEngine.Object.DontDestroyOnLoad(flag);
                flag.SetActive(false);
                var item = flag.GetComponent<Item>();
                //item.m_current_segment = -1;
                //item.m_stationary = false;
                item.m_index = i;
                //item.m_light_flicker = 0;
                item.m_type = ItemType.KEY_SECURITY;
                item.m_rotate_speed =  180f;
                item.m_light_flicker = 0.2f;
                //flag.tag = "Item";
                //flag.GetComponent<NetworkIdentity>().a
                CTF.FlagObjs.Add(flag);
                //Debug.Log("Flag " + i + " color " + color + " assetid " + flag.GetComponent<NetworkIdentity>().assetId);
                m_registered_prefabs.Add(assetId, flag);
                ClientScene.RegisterSpawnHandler(assetId, NetworkSpawnItemHandler, NetworkUnspawnItemHandler);
            }
            Debug.Log("CTF: Created " + CTF.FlagObjs.Count + " flag prefabs");
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "InitBeforeEachMatch")]
    class CTFInitBeforeEachMatch
    {
        private static void Postfix()
        {
            CTF.InitForMatch();
        }
    }

    // only called on server
    [HarmonyPatch(typeof(NetworkMatch), "StartPlaying")]
    class CTFStartPlaying
    {
        private static void SpawnFlags()
        {
            for (int i = 0; i < CTF.TeamCount; i++)
            {
                CTF.SpawnAtHome(i);
            }
        }

        private static void Postfix()
        {
            if (!CTF.IsActiveServer)
                return;
            CTF.FindFlagSpawnPoints();
            SpawnFlags();
        }
    }

    // pickup of flag item
    [HarmonyPatch(typeof(Item), "OnTriggerEnter")]
    class CTFOnTriggerEnter
    {
        private static MethodInfo _Item_ItemIsReachable_Method = typeof(Item).GetMethod("ItemIsReachable", BindingFlags.NonPublic | BindingFlags.Instance);

        static bool Prefix(Item __instance, Collider other)
        {
            if (__instance.m_type == ItemType.NONE) {
                return false;
            }

            if (__instance.m_type != ItemType.KEY_SECURITY || !CTF.IsActive) {
                return true;
            }

            if (other.attachedRigidbody == null)
            {
                return false;
            }

            PlayerShip component = other.attachedRigidbody.GetComponent<PlayerShip>();

            if (component == null)
            {
                return false;
            }

            Player c_player = component.c_player;
            if (!(bool)_Item_ItemIsReachable_Method.Invoke(__instance, new object[] { other }) || (bool)component.m_dying)
            {
                return false;
            }

            if (Overload.NetworkManager.IsServer() && !NetworkMatch.m_postgame && !c_player.c_player_ship.m_dying)
            {
                if (!CTF.Pickup(c_player, __instance)) {
                    return false;
                }
                c_player.CallRpcPlayItemPickupFX(__instance.m_type, __instance.m_super);
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(Player), "OnKilledByPlayer")]
    class CTFOnKilledByPlayer
    {
        private static void Prefix(Player __instance, DamageInfo di)
        {
            if (!CTF.IsActiveServer)
                return;

            if (!CTF.PlayerHasFlag.TryGetValue(__instance.netId, out int flag))
                return;

            CTF.NotifyAll(CTFEvent.CARRIER_DIED, null, __instance, flag);

            if (di.owner == null)
                return;

            Player attacker = di.owner.GetComponent<Player>();

            if (attacker == null || attacker.netId == __instance.netId)
                return;

            CTF.PlayerStats[attacker.netId].CarrierKills++;
            ServerStatLog.AddFlagEvent(attacker, "CarrierKill", MPTeams.AllTeams[flag]);
            NetworkServer.SendToAll(MessageTypes.MsgCTFPlayerStats, new CTF.PlayerStatesMessage() { m_player_states = CTF.PlayerStats });
        }
    }

    // draw flag on hud for carrier
    [HarmonyPatch(typeof(UIElement), "DrawHUDPrimaryWeapon")]
    class CTFDrawHUDPrimaryWeapon
    {
        static float m_anim_state;
        static void Prefix(Vector2 pos, UIElement __instance)
        {
            if (!CTF.IsActive)
                return;
            if (!CTF.PlayerHasFlag.TryGetValue(GameManager.m_local_player.netId, out int flag))
                return;
            var m_alpha = __instance.m_alpha;
            Color col_ub = UIManager.m_col_ub1;
            Color col_ui = UIManager.m_col_ui2;
            //Color col_ui2 = UIManager.m_col_ui5;
            //Color color = Color.Lerp(UIManager.m_col_hi4, UIManager.m_col_hi5, UnityEngine.Random.value * UIElement.FLICKER);
            col_ui.a = m_alpha;
            pos.y += ((!GameplayManager.VRActive) ? 50f : 25f);
            pos.x += ((MenuManager.opt_hud_weapons != 0) ? (-35f) : 35f);
            pos.y += 25f;

            var temp_pos = default(Vector2);
            pos.y -= 55f;
            pos.x += ((MenuManager.opt_hud_weapons != 0) ? (-102f) : 102f);

            pos.y -= 16f;
            temp_pos.y = pos.y;
            temp_pos.x = pos.x - 13f;
            m_anim_state = (m_anim_state + RUtility.FRAMETIME_UI) % ((float)Math.PI * 2f);
            UIManager.DrawSpriteUIRotated(temp_pos, 0.2f, 0.2f, m_anim_state,
                CTF.FlagColorUI(flag), m_alpha, 84);
        }
    }

    public class PlayerFlagMessage : MessageBase
    {
        public override void Serialize(NetworkWriter writer)
        {
            writer.Write((byte)1); // version
            writer.Write(m_player_id);
            writer.WritePackedUInt32((uint)m_flag_id);
            writer.Write((byte)m_flag_state);
        }
        public override void Deserialize(NetworkReader reader)
        {
            var version = reader.ReadByte();
            m_player_id = reader.ReadNetworkId();
            m_flag_id = (int)reader.ReadPackedUInt32();
            m_flag_state = version >= 1 ? (FlagState)reader.ReadByte() : FlagState.HOME;
        }
        public NetworkInstanceId m_player_id;
        public int m_flag_id;
        public FlagState m_flag_state;
    }

    public class CTFNotifyMessage : MessageBase
    {
        public override void Serialize(NetworkWriter writer)
        {
            writer.Write((byte)0); // version
            writer.Write((byte)m_event);
            writer.Write(m_message);
            writer.Write(m_player_id);
            writer.WritePackedUInt32((uint)m_flag_id);
        }
        public override void Deserialize(NetworkReader reader)
        {
            var version = reader.ReadByte();
            m_event = (CTFEvent)reader.ReadByte();
            m_message = reader.ReadString();
            m_player_id = reader.ReadNetworkId();
            m_flag_id = (int)reader.ReadPackedUInt32();
        }
        public CTFEvent m_event;
        public string m_message;
        public NetworkInstanceId m_player_id;
        public int m_flag_id;
    }

    public class CTFJoinUpdateMessage : MessageBase
    {
        public override void Serialize(NetworkWriter writer)
        {
            writer.Write((byte)0); // version
            writer.Write(m_mode);
            writer.WritePackedUInt32((uint)m_flag_id);
            writer.Write(m_destroy);
            writer.Write(m_timer);
            writer.Write(m_mp_name);
            writer.Write(m_player_id);
            writer.Write(m_old_id);
        }

        public override void Deserialize(NetworkReader reader)
        {
            var version = reader.ReadByte();
            m_mode = reader.ReadByte();
            m_flag_id = (int)reader.ReadPackedUInt32();
            m_destroy = reader.ReadBoolean();
            m_timer = reader.ReadSingle();
            m_mp_name = reader.ReadString();
            m_player_id = reader.ReadNetworkId();
            m_old_id = reader.ReadNetworkId();
        }
        public byte m_mode;
        public int m_flag_id;
        public bool m_destroy;
        public float m_timer;
        public string m_mp_name;
        public NetworkInstanceId m_player_id;
        public NetworkInstanceId m_old_id;
    }

    [HarmonyPatch(typeof(Client), "RegisterHandlers")]
    class CTFClientHandlers
    {
        private static void OnCTFPickup(NetworkMessage rawMsg)
        {
            var msg = rawMsg.ReadMessage<PlayerFlagMessage>();

            if (!CTF.IsActiveServer)
            {
                if (CTF.FlagStates[msg.m_flag_id] == FlagState.HOME)
                {
                    CTF.FlagWasHome = true;
                }
                CTF.FlagStates[msg.m_flag_id] = msg.m_flag_state;
                if (CTF.PlayerHasFlag.ContainsKey(msg.m_player_id))
                    return;
                CTF.PlayerHasFlag.Add(msg.m_player_id, msg.m_flag_id);
            }

            if (!CTF.CarrierBoostEnabled && GameManager.m_player_ship.netId == msg.m_player_id)
            {
                GameManager.m_player_ship.m_boosting = false;
                GameManager.m_player_ship.m_boost_overheat_timer = float.MaxValue;
            }

            // copy flag ring effect to carrier ship
            if (!GameplayManager.IsDedicatedServer())
            {
                CTF.PlayerEnableRing(CTF.FindPlayerForEffect(msg.m_player_id), msg.m_flag_id);
            }
        }

        private static void OnCTFLose(NetworkMessage rawMsg)
        {
            var msg = rawMsg.ReadMessage<PlayerFlagMessage>();

            if (!CTF.IsActiveServer)
            {
                if (CTF.PlayerHasFlag.ContainsKey(msg.m_player_id))
                    CTF.PlayerHasFlag.Remove(msg.m_player_id);
                CTF.FlagStates[msg.m_flag_id] = msg.m_flag_state;

                if (msg.m_flag_state == FlagState.LOST)
                    CTF.FlagReturnTime[msg.m_flag_id] = Time.time + CTF.ReturnTimeAmount;
            }

            if (!CTF.CarrierBoostEnabled && GameManager.m_player_ship.netId == msg.m_player_id)
            {
                GameManager.m_player_ship.m_boost_overheat_timer = 0;
                GameManager.m_player_ship.m_boost_heat = 0;
            }

            // remove flag ring effect from carrier ship
            if (!GameplayManager.IsDedicatedServer())
            {
                CTF.PlayerDisableRing(CTF.FindPlayerForEffect(msg.m_player_id));
            }
        }

        private static void OnCTFNotifyOld(NetworkMessage rawMsg)
        {
            var msg = rawMsg.ReadMessage<StringMessage>().value;
            GameplayManager.AddHUDMessage(msg, -1, true);
            SFXCueManager.PlayRawSoundEffect2D(SoundEffect.hud_notify_message1);
        }

        private static void OnCTFFlagUpdate(NetworkMessage rawMsg)
        {
            var msg = rawMsg.ReadMessage<PlayerFlagMessage>();

            if (!CTF.IsActiveServer)
            {
                CTF.FlagStates[msg.m_flag_id] = msg.m_flag_state;
                if (!msg.m_player_id.Equals(NetworkInstanceId.Invalid))
                    CTF.UpdateShipEffects(CTF.FindPlayerForEffect(msg.m_player_id));
            }
        }

        private static void OnCTFNotify(NetworkMessage rawMsg)
        {
            var msg = rawMsg.ReadMessage<CTFNotifyMessage>();
            var message = msg.m_message;
            if (message != null)
            {
                // Clients using custom colors need to construct their own HUD notifications instead of accepting Blue/Orange from server
                if (!Menus.mms_team_color_default)
                {
                    var player = Overload.NetworkManager.m_Players.FirstOrDefault(x => x.netId == msg.m_player_id);
                    switch (msg.m_event)
                    {
                        case CTFEvent.SCORE:
                            message = string.Format(Loc.LS("{0} ({1}) CAPTURES THE {2} FLAG!"), player.m_mp_name, MPTeams.TeamName(player.m_mp_team),
                                MPTeams.TeamName(MPTeams.AllTeams[msg.m_flag_id]), player, msg.m_flag_id);
                            break;
                        case CTFEvent.RETURN:
                            if (msg.m_player_id == null || msg.m_player_id == default(NetworkInstanceId))
                            {
                                message = string.Format(Loc.LS("LOST {0} FLAG RETURNED AFTER TIMER EXPIRED!"), MPTeams.TeamName(MPTeams.AllTeams[msg.m_flag_id]));
                            }
                            else
                            {
                                message = string.Format(Loc.LS("{0} RETURNS THE {1} FLAG!"), player.m_mp_name, MPTeams.TeamName(MPTeams.AllTeams[msg.m_flag_id]));
                            }
                            break;
                        case CTFEvent.PICKUP:
                            if (CTF.FlagWasHome)
                            {
                                message = string.Format(Loc.LS("{0} ({1}) PICKS UP THE {2} FLAG!"), player.m_mp_name, MPTeams.TeamName(player.m_mp_team),
                                    MPTeams.TeamName(MPTeams.AllTeams[msg.m_flag_id]), player, msg.m_flag_id);
                                CTF.FlagWasHome = false;
                            } else
                            {
                                message = message = string.Format(Loc.LS("{0} ({1}) FINDS THE {2} FLAG AMONG SOME DEBRIS!"), player.m_mp_name, MPTeams.TeamName(player.m_mp_team),
                                    MPTeams.TeamName(MPTeams.AllTeams[msg.m_flag_id]), player, msg.m_flag_id);
                            }
                            break;
                        case CTFEvent.DROP:
                            if (msg.m_player_id == null || msg.m_player_id == default(NetworkInstanceId))
                            {
                                message = string.Format(Loc.LS("{0} ({1}) DROPPED THE {2} FLAG!"), player.m_mp_name, MPTeams.TeamName(player.m_mp_team), MPTeams.TeamName(MPTeams.AllTeams[msg.m_flag_id]), player, msg.m_flag_id);
                            }
                            else // this seems to be the only message that can fire *after* a player disconnects
                            {
                                message = string.Format(Loc.LS("THE {0} FLAG HAS BEEN DROPPED!"), MPTeams.TeamName(MPTeams.AllTeams[msg.m_flag_id]));
                            }
                            break;
                    }
                }
                GameplayManager.AddHUDMessage(message, -1, true);
            }
                
            Debug.Log("OnCTFNotify " + msg.m_event);
            switch (msg.m_event) {
                case CTFEvent.PICKUP:
                    SFXCueManager.PlayRawSoundEffect2D(SoundEffect.sfx_lockdown_initiated, 1f, 0.8f, 0f, false);
                    SFXCueManager.PlayRawSoundEffect2D(SoundEffect.sfx_alien_tele_warp, 1f, 0f, 0f, false);
                    CTF.PlayerStats[msg.m_player_id].Pickups++;
                    break;
                case CTFEvent.DROP:
                    SFXCueManager.PlayRawSoundEffect2D(SoundEffect.cine_sfx_warning_2, 0.7f, 0f, 0f, false);
                    if (msg.m_player_id != null && msg.m_player_id != default(NetworkInstanceId))
                    {
                        CTF.PlayerStats[msg.m_player_id].Drops++;
                    }
                    break;
                case CTFEvent.RETURN:
                    SFXCueManager.PlayRawSoundEffect2D(SoundEffect.sfx_matcenter_warp_high1, 1.1f, 0f, 0f, false);
                    if (msg.m_player_id != null && msg.m_player_id != default(NetworkInstanceId))
                    {
                        CTF.PlayerStats[msg.m_player_id].Returns++;
                    }
                    break;
                case CTFEvent.SCORE:
                    SFXCueManager.PlayRawSoundEffect2D(SoundEffect.ui_upgrade, 1f, 0f, 0f, false);
                    CTF.PlayerStats[msg.m_player_id].Captures++;
                    break;
            }
        }

        // catch-all for problems encountered during JIP
        private static void OnCTFJoinUpdate(NetworkMessage rawMsg)
        {
            var msg = rawMsg.ReadMessage<CTFJoinUpdateMessage>();
            switch (msg.m_mode)
            {
                case 0: // player JIPed, update network IDs for scoreboard if needed
                    if (msg.m_player_id == GameManager.m_local_player.netId) // this is a freshly created ID from OnMatchStart, which needs to be deleted for the JIP client to sync to the old one correctly. Would be better to do this on join if we had a way to tell client-side.
                    {
                        //Debug.Log("CCF JoinUpdate deleting fresh entries due to JIP");
                        CTF.PlayerStats.Remove(msg.m_player_id);
                        CTF.NameToID.Remove(msg.m_mp_name);
                    }

                    if (CTF.NameToID.ContainsKey(msg.m_mp_name)) // found the old ID, sync up the player stats
                    {                        
                        //Debug.Log("CCF JoinUpdate existing player found, syncing");
                        NetworkInstanceId idx = CTF.NameToID[msg.m_mp_name];
                        CTF.NameToID[msg.m_mp_name] = msg.m_player_id;
                        CTF.PlayerStats.Add(msg.m_player_id, CTF.PlayerStats[idx]);
                        CTF.PlayerStats.Remove(idx);
                    }
                    else // didn't find it and need to create an entry
                    {
                        CTF.NameToID.Add(msg.m_mp_name, msg.m_player_id);

                        if (CTF.PlayerStats.ContainsKey(msg.m_old_id)) // name doesn't exist yet but stats do
                        {
                            //Debug.Log("CCF JoinUpdate creating new player entry to sync with existing stats");
                            CTF.PlayerStats.Add(msg.m_player_id, CTF.PlayerStats[msg.m_old_id]);
                            CTF.PlayerStats.Remove(msg.m_old_id);
                        }
                        else // totally new player
                        {
                            //Debug.Log("CCF JoinUpdate creating new blank player entry");
                            CTF.PlayerStats.Add(msg.m_player_id, new CTF.CTFStats());
                        }

                    }
                    break;

                case 1: // update flag colors and remove duplicates if needed for JIPed player
                    foreach (var item in GameObject.FindObjectsOfType<Item>())
                    {
                        if (item.m_type == ItemType.KEY_SECURITY && item.m_index == msg.m_flag_id)
                        {
                            if (msg.m_destroy)
                            {
                                item.m_type = ItemType.NONE;
                                UnityEngine.Object.Destroy(item.gameObject);
                            }
                            else
                            {
                                CTF.UpdateFlagColor(item.c_go, item.m_index);
                            }
                        }
                    }
                    break;

                case 2: // update one of the flag timers for JIPed players to reflect a dropped flag
                    CTF.FlagReturnTime[msg.m_flag_id] = msg.m_timer + Time.time;
                    //Debug.Log("CCF timer: " + msg.m_timer + ", current time: " + Time.time + ", new FlagReturnTime: " + CTF.FlagReturnTime[msg.m_flag_id]);
                    break;
            }
        }

        static void Postfix()
        {
            if (Client.GetClient() == null)
                return;
            Client.GetClient().RegisterHandler(MessageTypes.MsgCTFPickup, OnCTFPickup);
            Client.GetClient().RegisterHandler(MessageTypes.MsgCTFLose, OnCTFLose);
            Client.GetClient().RegisterHandler(MessageTypes.MsgCTFNotifyOld, OnCTFNotifyOld);
            Client.GetClient().RegisterHandler(MessageTypes.MsgCTFFlagUpdate, OnCTFFlagUpdate);
            Client.GetClient().RegisterHandler(MessageTypes.MsgCTFNotify, OnCTFNotify);
            Client.GetClient().RegisterHandler(MessageTypes.MsgCTFJoinUpdate, OnCTFJoinUpdate);
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "IsTeamMode")]
    class CTFIsTeamMode
    {
        private static bool Prefix(MatchMode mode, ref bool __result)
        {
            if (mode == CTF.MatchModeCTF)
            {
                __result = true;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "SpewItemsOnDeath")]
    class CTFSpewItemsOnDeath
    {
        private static void Postfix(PlayerShip __instance)
        {
            if (!CTF.IsActiveServer)
                return;
            if (!CTF.PlayerHasFlag.TryGetValue(__instance.c_player.netId, out int flag))
                return;
            CTF.Drop(__instance.c_player);
            CTF.SpewFlag(flag, __instance);
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "Update")]
    class CTFCarrierGlow
    {
        static Material[] mats;

        public static void ResetMats()
        {
            mats = null;
            SetupMat();
        }

        private static void SetupMat()
        {
            mats = new Material[CTF.TeamCount];
            var org_mat = UIManager.gm.m_pull_material; //UIManager.gm.m_damage_material; //UIManager.gm.m_pull_material;
            for (int i = 0; i < CTF.TeamCount; i++)
            {
                var team = MPTeams.AllTeams[i];
                var mat = new Material(org_mat.shader);
                mat.CopyPropertiesFromMaterial(org_mat);
                mat.SetColor("_EdgeColor", MPTeams.TeamColor(team, 0));
                //mat.SetColor("_GlowColor", MPTeams.TeamColor(team, 0));
                mats[i] = mat;
            }
        }

        private static void Postfix(PlayerShip __instance)
        {
            if (__instance.isLocalPlayer || __instance.m_dead ||
                !CTF.IsActive ||
                !CTF.PlayerHasFlag.TryGetValue(__instance.c_player.netId, out int flag) ||
                Overload.NetworkManager.IsHeadless())
                return;
            if (mats == null)
                SetupMat();
            __instance.DrawEffectMesh(mats[flag], 1f);
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "GetHighestScore")]
    class CTFGetHighestScore
    {
        private static bool Prefix(ref int __result)
        {
            if (NetworkMatch.GetMode() != CTF.MatchModeCTF)
                return true;
            __result = MPTeams.HighestScore();
            return false;
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawMpDeathOverlay")]
    class CTFDrawMpDeathOverlay
    {
        private static void Prefix(float ___m_alpha, UIElement __instance)
        {
            if (!GameplayManager.IsMultiplayerActive)
                return;
            var position = new Vector2();
            position.x = 0f;
            position.y = -230f;
            float num = -25f;
            for (int i = 0; i < GameplayManager.RecentMessageString.Length; i++)
            {
                if (GameplayManager.RecentMessageTimer[i] > 0f && GameplayManager.RecentMessageString[i] != string.Empty)
                {
                    __instance.DrawStringSmall(GameplayManager.RecentMessageString[i], position, 0.5f, StringOffset.CENTER, (!GameplayManager.RecentMessagePriority[i]) ? UIManager.m_col_ui3 : UIManager.m_col_hi5, Mathf.Min(1f, GameplayManager.RecentMessageTimer[i] * 2f));
                    position.y -= 25f;
                    num += 25f;
                }
            }

            if (!CTF.IsActive)
                return;
            position.x = 510f;
            position.y = -230f;

            position.x -= 100f;
            position.x -= 110f;
            position.y += 20f;
            CTF.DrawFlags(__instance, position, ___m_alpha);
        }
    }

    [HarmonyPatch(typeof(Player), "IsPressed")]
    class Player_IsPressed
    {
        static void Postfix(CCInput cc_type, ref bool __result, Player __instance)
        {
            if (!CTF.CarrierBoostEnabled && (!__instance.isLocalPlayer || !uConsole.IsOn()) && __instance.m_input_count[(int)cc_type] >= 1 && cc_type == CCInput.USE_BOOST && GameplayManager.IsMultiplayer && CTF.IsFlagrunner(__instance.c_player_ship))
            {
                __result = false;
            }
            else
            {
                __result = (!__instance.isLocalPlayer || !uConsole.IsOn()) && __instance.m_input_count[(int)cc_type] >= 1;
            }
        }
    }

    [HarmonyPatch(typeof(Client), "OnMatchStart")]
    class CTF_Client_OnMatchStart
    {
        static void Postfix()
        {
            if (MPModPrivateData.MatchMode != ExtMatchMode.CTF)
                return;

            foreach (var player in Overload.NetworkManager.m_PlayersForScoreboard)
            {
                //if (!CTF.PlayerStats.ContainsKey(player.netId))
                if (!CTF.NameToID.ContainsKey(player.m_mp_name))
                {
                    //Debug.Log("CCF Adding OnMatchStart Stats");
                    CTF.NameToID.Add(player.m_mp_name, player.netId);
                    CTF.PlayerStats.Add(player.netId, new CTF.CTFStats());
                }
            }
        }
    }

    [HarmonyPatch(typeof(Client), "RegisterHandlers")]
    class CTF_Client_RegisterHandlers
    {
        private static void OnCTFPlayerStats(NetworkMessage msg)
        {
            var msmsg = msg.ReadMessage<CTF.PlayerStatesMessage>();
            foreach (var mps in msmsg.m_player_states)
            {
                if (!CTF.PlayerStats.ContainsKey(mps.Key))
                {
                    //Debug.Log("CCF OnCTFPlayerStats firing, no key found");
                    // Likely JIP
                    CTF.PlayerStats.Add(mps.Key, new CTF.CTFStats
                    {
                        Captures = mps.Value.Captures,
                        CarrierKills = mps.Value.CarrierKills,
                        Drops = mps.Value.Drops,
                        Pickups = mps.Value.Pickups,
                        Returns = mps.Value.Returns
                    });
                    foreach (var player in Overload.NetworkManager.m_PlayersForScoreboard)
                    {
                        if (player.netId == mps.Key) // if this *doesn't* trigger, then we're on the joining client. Stats/Name will get properly updated with a CTFJoinUpdate momentarily
                        {
                            if (!CTF.NameToID.ContainsKey(player.m_mp_name) && player.m_mp_name != GameManager.m_local_player.m_mp_name)
                            {
                                // if this *doesn't* trigger, then we're on the joining client. Stats/Name will get properly updated with a CTFJoinUpdate momentarily
                                //Debug.Log("CCF OnCTFPlayerStats adding name to NameToID");
                                CTF.NameToID.Add(player.m_mp_name, mps.Key);
                            }
                        }
                    }
                }
                else
                {
                    //Debug.Log("CCF OnCTFPlayerStats firing, key WAS found");
                    CTF.PlayerStats[mps.Key].Captures = mps.Value.Captures;
                    CTF.PlayerStats[mps.Key].CarrierKills = mps.Value.CarrierKills;
                    CTF.PlayerStats[mps.Key].Drops = mps.Value.Drops;
                    CTF.PlayerStats[mps.Key].Pickups = mps.Value.Pickups;
                    CTF.PlayerStats[mps.Key].Returns = mps.Value.Returns;
                }
            }
        }

        static void Postfix()
        {
            if (Client.GetClient() == null)
                return;

            Client.GetClient().RegisterHandler(MessageTypes.MsgCTFPlayerStats, OnCTFPlayerStats);
        }
    }

    // CTF flag objects somehow end up in SP save games,
    // explicitly skip them when saving a game.
    [HarmonyPatch]
    class CTFPreventFlagObjectsInSave
    {
        static MethodInfo TargetMethod() {
            // This is the method generated for the SerializeObjects line:
            // where c.gameObject.scene.name != null
            return typeof(SaveLoad)
                .GetMethod("<SerializeObjects`1>m__0", BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(typeof(Item));
        }

        static bool WhereClauseForGameObject(GameObject item)
        {
            return item.scene.name != null && !CTF.FlagObjs.Contains(item);
        }

        static IEnumerable<CodeInstruction> Transpiler(ILGenerator ilGen, IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Callvirt)
                {
                    yield return code; // calls get_gameObject
                    yield return new CodeInstruction(OpCodes.Call,
                        typeof(CTFPreventFlagObjectsInSave)
                            .GetMethod("WhereClauseForGameObject", BindingFlags.NonPublic | BindingFlags.Static));
                    yield return new CodeInstruction(OpCodes.Ret);
                    break;
                }
                yield return code;
            }
        }
    }
}
