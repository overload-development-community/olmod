using Harmony;
using Overload;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace GameMod
{
    public enum FlagState
    {
        HOME, PICKEDUP, LOST
    }
    public enum CTFEvent
    {
        NONE, PICKUP, DROP, RETURN, SCORE, CARRIER_DIED
    }
    static class CTF
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
        public static float ReturnTimeAmount = 30;
        public static bool ShowReturnTimer = false;

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

        public static void SendCTFPickup(int conn_id, NetworkInstanceId player_id, int flag_id, FlagState state)
        {
            SendToClientOrAll(conn_id, CTFCustomMsg.MsgCTFPickup, new PlayerFlagMessage { m_player_id = player_id, m_flag_id = flag_id, m_flag_state = state });
        }

        public static void SendCTFLose(int conn_id, NetworkInstanceId player_id, int flag_id, FlagState state)
        {
            SendToClientOrAll(conn_id, CTFCustomMsg.MsgCTFLose, new PlayerFlagMessage { m_player_id = player_id, m_flag_id = flag_id, m_flag_state = state });
        }

        public static void SendCTFFlagUpdate(int conn_id, NetworkInstanceId player_id, int flag_id, FlagState state)
        {
            SendToClientOrAll(conn_id, CTFCustomMsg.MsgCTFFlagUpdate, new PlayerFlagMessage { m_player_id = player_id, m_flag_id = flag_id, m_flag_state = state });
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
                    rest.Add(spawn);
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
            foreach (var timer in FlagReturnTimer)
                if (timer != null)
                    GameManager.m_gm.StopCoroutine(timer);
            FlagReturnTimer = new IEnumerator[TeamCount];
        }

        // pickup of flag item
        public static bool Pickup(Player player, int flag)
        {
            if (flag < 0 || flag >= FlagObjs.Count)
                return false;
            var ownFlag = MPTeams.AllTeams[flag] == player.m_mp_team;
            if  (ownFlag && FlagStates[flag] == FlagState.HOME)
                return false;
            if (!ownFlag && PlayerHasFlag.ContainsKey(player.netId))
                return false;

            // this also sends to 'client 0' so it'll get processed on the server as well
            CTFEvent evt;
            if (ownFlag) {
                SendCTFFlagUpdate(-1, player.netId, flag, FlagState.HOME);
                SpawnAtHome(flag);
                evt = CTFEvent.RETURN;
            } else {
                SendCTFPickup(-1, player.netId, flag, FlagState.PICKEDUP);
                evt = CTFEvent.PICKUP;
            }

            var msg = FlagStates[flag] == FlagState.HOME ? "{0} ({1}) PICKS UP THE {2} FLAG!" :
                ownFlag ? "{0} RETURNS THE {2} FLAG!" :
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
            var item = flag.GetComponent<Item>();
            //item.m_spewed = true;
            //item.m_spawn_point = -1;
            item.c_rigidbody.velocity = vel * item.m_push_multiplier;
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

        private static void LogEvent(CTFEvent evt, Player player)
        {
            switch (evt)
            {
                case CTFEvent.RETURN:
                    ServerStatLog.AddFlagEvent(player, "Return");
                    break;
                case CTFEvent.PICKUP:
                    ServerStatLog.AddFlagEvent(player, "Pickup");
                    break;
                case CTFEvent.SCORE:
                    ServerStatLog.AddFlagEvent(player, "Capture");
                    break;
            }
        }

        public static void NotifyAll(CTFEvent evt, string message, Player player, int flag)
        {
            Debug.Log("CTF.NotifyAll " + evt);
            NetworkServer.SendToAll(CTFCustomMsg.MsgCTFNotify, new CTFNotifyMessage { m_event = evt, m_message = message,
                m_player_id = player == null ? default(NetworkInstanceId) : player.netId, m_flag_id = flag });
            LogEvent(evt, player);
        }

        public static void Score(Player player)
        {
            if (NetworkMatch.m_postgame)
                return;
            if (!PlayerHasFlag.TryGetValue(player.netId, out int flag) || FlagStates[MPTeams.TeamNum(player.m_mp_team)] != FlagState.HOME)
                return;
            PlayerHasFlag.Remove(player.netId);
            SendCTFLose(-1, player.netId, flag, FlagState.HOME);
            SpawnAtHome(flag);
            NetworkMatch.AddPointForTeam(player.m_mp_team);

            NotifyAll(CTFEvent.SCORE, string.Format(Loc.LS("{0} ({1}) CAPTURES THE {2} FLAG!"), player.m_mp_name, MPTeams.TeamName(player.m_mp_team),
                MPTeams.TeamName(MPTeams.AllTeams[flag])), player, flag);
        }

        public static IEnumerator CreateReturnTimer(int flag)
        {
            yield return new WaitForSeconds(CTF.ReturnTimeAmount);
            if (!IsActiveServer || NetworkMatch.m_match_state != MatchState.PLAYING || CTF.FlagStates[flag] != FlagState.LOST)
                yield break;
            SendCTFLose(-1, default(NetworkInstanceId), flag, FlagState.HOME);
            SpawnAtHome(flag);
            NotifyAll(CTFEvent.RETURN, string.Format(Loc.LS("LOST {0} FLAG RETURNED AFTER TIMER EXPIRED!"), MPTeams.TeamName(MPTeams.AllTeams[flag])), null, flag);
            FlagReturnTimer[flag] = null;
        }

        public static void Drop(Player player)
        {
            if (!PlayerHasFlag.TryGetValue(player.netId, out int flag))
                return;
            SendCTFLose(-1, player.netId, flag, FlagState.LOST);
            NotifyAll(CTFEvent.DROP, null, player, flag);
            if (FlagReturnTimer[flag] != null)
                GameManager.m_gm.StopCoroutine(FlagReturnTimer[flag]);
            GameManager.m_gm.StartCoroutine(FlagReturnTimer[flag] = CreateReturnTimer(flag));
        }

        public static void SendJoinUpdate(Player player)
        {
            if (!CTF.IsActiveServer)
                return;
            int conn_id = player.connectionToClient.connectionId;
            foreach (var x in CTF.PlayerHasFlag)
                CTF.SendCTFPickup(conn_id, x.Key, x.Value, FlagState.PICKEDUP);
            for (int flag = 0; flag < TeamCount; flag++)
                if (FlagStates[flag] == FlagState.LOST)
                    CTF.SendCTFFlagUpdate(conn_id, NetworkInstanceId.Invalid, flag, FlagStates[flag]);
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

    [HarmonyPatch(typeof(NetworkMatch), "GetModeString")]
    class CTFGetModeString
    {
        // there's a mode argument but in actual usage this is always NetworkMatch.GetMode()
        // so ignore it here, since it uses MatchMode.NUM differently :(
        private static bool Prefix(MatchMode ___m_match_mode, ref string __result)
        {
            if (___m_match_mode == CTF.MatchModeCTF)
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
            netId.GetType().GetField("m_AssetId", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(netId, asset_id);
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
                netId.GetType().GetField("m_AssetId", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(netId, assetId);
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
                CTF.SpawnAtHome(i);
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
        static MethodInfo ItemIsReachable;
        static void Prepare()
        {
            ItemIsReachable = typeof(Item).GetMethod("ItemIsReachable", BindingFlags.NonPublic | BindingFlags.Instance);
        }
        static bool Prefix(Item __instance, Collider other)
        {
            //Debug.Log("OnTriggerEnter " + __instance.m_type + " server =" + Overload.NetworkManager.IsServer() + " index=" + __instance.m_index);
            if (__instance.m_type != ItemType.KEY_SECURITY || !CTF.IsActive)
                return true;
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
            if (!(bool)ItemIsReachable.Invoke(__instance, new object[] { other }) || (bool)component.m_dying)
            {
                return false;
            }
            if (Overload.NetworkManager.IsServer() && !NetworkMatch.m_postgame)
            {
                if (!CTF.Pickup(c_player, __instance.m_index))
                    return false;
                c_player.CallRpcPlayItemPickupFX(__instance.m_type, __instance.m_super);
                UnityEngine.Object.Destroy(__instance.c_go);
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

            ServerStatLog.AddFlagEvent(attacker, "CarrierKill");
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

    public class CTFCustomMsg
    {
        public const short MsgCTFPickup = 121;
        public const short MsgCTFLose = 122;
        public const short MsgCTFNotifyOld = 123;
        public const short MsgCTFFlagUpdate = 124;
        public const short MsgCTFNotify = 125;
    }

    [HarmonyPatch(typeof(Client), "RegisterHandlers")]
    class CTFClientHandlers
    {
        private static void OnCTFPickup(NetworkMessage rawMsg)
        {
            var msg = rawMsg.ReadMessage<PlayerFlagMessage>();
            CTF.FlagStates[msg.m_flag_id] = msg.m_flag_state;
            if (CTF.PlayerHasFlag.ContainsKey(msg.m_player_id))
                return;
            CTF.PlayerHasFlag.Add(msg.m_player_id, msg.m_flag_id);

            // copy flag ring effect to carrier ship
            CTF.PlayerEnableRing(CTF.FindPlayerForEffect(msg.m_player_id), msg.m_flag_id);
        }

        private static void OnCTFLose(NetworkMessage rawMsg)
        {
            var msg = rawMsg.ReadMessage<PlayerFlagMessage>();
            if (CTF.PlayerHasFlag.ContainsKey(msg.m_player_id))
                CTF.PlayerHasFlag.Remove(msg.m_player_id);
            CTF.FlagStates[msg.m_flag_id] = msg.m_flag_state;

            // remove flag ring effect from carrier ship
            CTF.PlayerDisableRing(CTF.FindPlayerForEffect(msg.m_player_id));

            if (msg.m_flag_state == FlagState.LOST)
                CTF.FlagReturnTime[msg.m_flag_id] = Time.time + CTF.ReturnTimeAmount;
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
            CTF.FlagStates[msg.m_flag_id] = msg.m_flag_state;
        }

        private static void OnCTFNotify(NetworkMessage rawMsg)
        {
            var msg = rawMsg.ReadMessage<CTFNotifyMessage>();
            if (msg.m_message != null)
                GameplayManager.AddHUDMessage(msg.m_message, -1, true);
            Debug.Log("OnCTFNotify " + msg.m_event);
            switch (msg.m_event) {
                case CTFEvent.PICKUP:
                    SFXCueManager.PlayRawSoundEffect2D(SoundEffect.sfx_lockdown_initiated, 1f, 0.8f, 0f, false);
                    SFXCueManager.PlayRawSoundEffect2D(SoundEffect.sfx_alien_tele_warp, 1f, 0f, 0f, false);
                    break;
                case CTFEvent.DROP:
                    SFXCueManager.PlayRawSoundEffect2D(SoundEffect.cine_sfx_warning_2, 0.7f, 0f, 0f, false);
                    break;
                case CTFEvent.RETURN:
                    SFXCueManager.PlayRawSoundEffect2D(SoundEffect.sfx_matcenter_warp_high1, 1.1f, 0f, 0f, false);
                    break;
                case CTFEvent.SCORE:
                    SFXCueManager.PlayRawSoundEffect2D(SoundEffect.ui_upgrade, 1f, 0f, 0f, false);
                    break;
            }
        }

        static void Postfix()
        {
            if (Client.GetClient() == null)
                return;
            Client.GetClient().RegisterHandler(CTFCustomMsg.MsgCTFPickup, OnCTFPickup);
            Client.GetClient().RegisterHandler(CTFCustomMsg.MsgCTFLose, OnCTFLose);
            Client.GetClient().RegisterHandler(CTFCustomMsg.MsgCTFNotifyOld, OnCTFNotifyOld);
            Client.GetClient().RegisterHandler(CTFCustomMsg.MsgCTFFlagUpdate, OnCTFFlagUpdate);
            Client.GetClient().RegisterHandler(CTFCustomMsg.MsgCTFNotify, OnCTFNotify);
        }
    }

    [HarmonyPatch(typeof(MonsterBallGoal), "OnTriggerEnter")]
    internal class CTFScore
    {
        private static void Prefix(Collider other, MonsterBallGoal __instance)
        {
            if (!CTF.IsActiveServer || other.attachedRigidbody == null)
                return;
            PlayerShip playerShip = other.attachedRigidbody.GetComponent<PlayerShip>();
            if (playerShip == null)
                return;
            Player player = playerShip.c_player;
            var team = player.m_mp_team;
            if (team == __instance.m_team && CTF.PlayerHasFlag.ContainsKey(player.netId))
            {
                CTF.Score(player);
            }
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

    [HarmonyPatch(typeof(UIElement), "DrawHUDScoreInfo")]
    class CTFDrawHUDScoreInfo
    {
        private static void DrawFlagState(UIElement uie, Vector2 pos, float m_alpha, int flag)
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
                    float t = CTF.FlagReturnTime[flag] - Time.time;
                    if (t < 0)
                        t = 0;
                    uie.DrawDigitsTimeNoHours(pos, t, 0.45f, MPTeams.TeamColor(MPTeams.AllTeams[flag], 4), m_alpha);
                }
            }
        }

        private static void Prefix(Vector2 pos, float ___m_alpha, UIElement __instance)
        {
            if (!CTF.IsActive)
                return;
            pos.x -= 4f;
            pos.y -= 5f;
            pos.x -= 100f;
            pos.y -= 20f;

            pos.x -= 110f;
            pos.y += 20f;

            for (int i = 0; i < CTF.TeamCount; i++)
            {
                DrawFlagState(__instance, pos, ___m_alpha, i);
                pos.x += 60;
            }
        }
    }
}
