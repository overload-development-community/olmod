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
    static class CTF
    {
        public const MatchMode MatchModeCTF = MatchMode.NUM;
        public const int TeamCount = 2;
        public static List<GameObject> FlagObjs = new List<GameObject>();
        public static bool IsActive { get { return NetworkMatch.GetMode() == CTF.MatchModeCTF && GameplayManager.IsMultiplayerActive; } }
        public static bool[] HasSpawnPoint = new bool[TeamCount];
        public static Vector3[] SpawnPoint = new Vector3[TeamCount];
        public static FlagState[] FlagStates = new FlagState[TeamCount];
        public static Color FlagColor(int teamIdx)
        {
            return MPTeams.TeamColor(MPTeams.AllTeams[teamIdx], teamIdx == 1 ? 2 : 3); // make orange a bit darker
        }
        public static Color FlagColorUI(int teamIdx)
        {
            return MPTeams.TeamColor(MPTeams.AllTeams[teamIdx], 1);
        }
        public static Dictionary<NetworkInstanceId, int> PlayerHasFlag = new Dictionary<NetworkInstanceId, int>();

        public static void SendCTFPickup(NetworkInstanceId player_id, int flag_id, FlagState state)
        {
            NetworkServer.SendToAll(CTFCustomMsg.MsgCTFPickup, new PlayerFlagMessage { m_player_id = player_id, m_flag_id = flag_id, m_flag_state = state });
        }

        public static void SendCTFLose(NetworkInstanceId player_id, int flag_id, FlagState state)
        {
            NetworkServer.SendToAll(CTFCustomMsg.MsgCTFLose, new PlayerFlagMessage { m_player_id = player_id, m_flag_id = flag_id, m_flag_state = state });
        }
        public static void SendCTFFlagUpdate(NetworkInstanceId player_id, int flag_id, FlagState state)
        {
            NetworkServer.SendToAll(CTFCustomMsg.MsgCTFFlagUpdate, new PlayerFlagMessage { m_player_id = player_id, m_flag_id = flag_id, m_flag_state = state });
        }
        public static void InitForLevel()
        {
            //foreach (var x in GameObject.FindObjectsOfType<MonsterBallGoal>())
            //    x.gameObject.GetComponent<BoxCollider>()
            PlayerHasFlag.Clear();
            HasSpawnPoint = new bool[TeamCount];
            FlagStates = new FlagState[TeamCount];
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
            //Debug.Log("InitForLevel HasSpawnPoint=" + string.Join(",", HasSpawnPoint.Select(x => x.ToString()).ToArray()));
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
            if (ownFlag) {
                SendCTFFlagUpdate(player.netId, flag, FlagState.HOME);
                SpawnAtHome(flag);
                ServerStatLog.AddFlagEvent(player, "Return");
            }
            else {
                SendCTFPickup(player.netId, flag, FlagState.PICKEDUP);
                ServerStatLog.AddFlagEvent(player, "Pickup");
            }
            // this will also send to 'client 0' so it'll get added on the server as well
            //PlayerHasKey.Add(player.connectionToClient.connectionId, flag);
            var msg = FlagStates[flag] == FlagState.HOME ? "{0} ({1}) PICKS UP THE {2} FLAG!" :
                ownFlag ? "{0} RETURNS THE {2} FLAG!" :
                "{0} ({1}) FINDS THE {2} FLAG AMONG SOME DEBRIS!";

            CTF.Notify(player, string.Format(Loc.LS(msg), player.m_mp_name, MPTeams.TeamName(player.m_mp_team),
                MPTeams.TeamName(MPTeams.AllTeams[flag])));
            /*
            var otherMsg = player.m_mp_name + " PICKED UP A FLAG!";
            foreach (var pl in Overload.NetworkManager.m_Players)
                if (!System.Object.ReferenceEquals(pl, player))
                    CTF.Notify(pl, otherMsg);
            */
            return true;
        }

        public static void Drop(Player player)
        {
            /*
            NetworkInstanceId player_id = default(NetworkInstanceId);
            foreach (var x in PlayerHasKey)
                if (x.Value == flag)
                    player_id = x.Key;
            if (player_id.IsEmpty())
            {
                Debug.Log("Drop flag " + flag + ": No player found with flag!");
                return;
            }
            */
            if (!PlayerHasFlag.TryGetValue(player.netId, out int flag)) {
                Debug.Log("CTF.Drop: player " + player.m_mp_name + " has no flag!");
                return;
            }
            //var player = ClientScene.FindLocalObject(player_id).GetComponent<Player>();
            CTF.PlayerHasFlag.Remove(player.netId);
            SendCTFLose(player.netId, flag, FlagState.LOST);
            SpawnAtHome(flag);
            //SendCTFPickup(player.connectionToClient.connectionId, flag);
            // this will also send to 'client 0' so it'll get added on the server as well
            //PlayerHasKey.Add(player.connectionToClient.connectionId, flag);
        }
        public static void SpawnAtHome(int flag_id)
        {
            if (flag_id >= HasSpawnPoint.Length || !HasSpawnPoint[flag_id]) // flag_id >= GameManager.m_level_data.m_player_spawn_points.Length || flag_id > 2) // || flag_id >= CTF.FlagObjs.Count)
            {
                Debug.Log("No home spawn point for flag " + flag_id);
                return;
            }
            //var pos = GameManager.m_level_data.m_player_spawn_points[flag_id].position;
            //var prefab = PrefabManager.item_prefabs[(int)ItemPrefab.entity_item_log_entry];
            SpawnAt(flag_id, SpawnPoint[flag_id], Vector3.zero);
        }
        public static void SpawnAt(int flag_id, Vector3 pos, Vector3 vel)
        {
            var prefab = CTF.FlagObjs[flag_id];
            if (prefab == null)
            {
                Debug.LogWarningFormat("Missing prefab for flag {0}", flag_id);
                return;
            }
            Debug.Log("Server spawning flag " + flag_id);
            //Item.Spew(prefab, pos, Vector3.zero);return;
            GameObject flag = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
            if (flag == null)
            {
                Debug.LogWarningFormat("Failed to instantiate prefab: {0} in CTFOnSceneLoaded", prefab.name);
                return;
            }
            //yield return new WaitForFixedUpdate();

            //var netId = flag.GetComponent<NetworkIdentity>();
            //netId.GetType().GetField("m_AssetId", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(netId, CTF.FlagAssetId(flag_id));
            Debug.Log("pre spawn flag " + flag_id + " assetid " + flag.GetComponent<NetworkIdentity>().assetId);
            flag.SetActive(true);
            NetworkServer.Spawn(flag); //, CTF.FlagAssetId(flag_id));
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
        public static void Notify(Player player, string message)
        {
            //player.CallTargetAddHUDMessage(player.connectionToClient, message, -1, true);
            NetworkServer.SendToClient(player.connectionToClient.connectionId, CTFCustomMsg.MsgCTFNotify, new StringMessage(message));
        }
        public static void NotifyAll(string message)
        {
            //player.CallTargetAddHUDMessage(player.connectionToClient, message, -1, true);
            NetworkServer.SendToAll(CTFCustomMsg.MsgCTFNotify, new StringMessage(message));
        }

        public static void Score(Player player)
        {
            if (NetworkMatch.m_postgame)
            {
                return;
            }

            if (!PlayerHasFlag.TryGetValue(player.netId, out int flag) || FlagStates[MPTeams.TeamNum(player.m_mp_team)] != FlagState.HOME)
                return;
            PlayerHasFlag.Remove(player.netId);
            SendCTFLose(player.netId, flag, FlagState.HOME);
            SpawnAtHome(flag);
            NetworkMatch.AddPointForTeam(player.m_mp_team);
            //typeof(NetworkMatch).GetMethod("SendUpdatedTeamScoreToClients", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { team });

            NotifyAll(string.Format(Loc.LS("{0} ({1}) CAPTURES THE {2} FLAG!"), player.m_mp_name, MPTeams.TeamName(player.m_mp_team),
                MPTeams.TeamName(MPTeams.AllTeams[flag])));

            ServerStatLog.AddFlagEvent(player, "Capture");
        }
        public static void SendJoinUpdate(Player player)
        {
            if (!CTF.IsActive)
                return;
            foreach (var x in CTF.PlayerHasFlag)
                CTF.SendCTFPickup(x.Key, x.Value, FlagState.PICKEDUP);
            for (int flag = 0; flag < TeamCount; flag++)
                if (FlagStates[flag] == FlagState.LOST)
                    CTF.SendCTFFlagUpdate(NetworkInstanceId.Invalid, flag, FlagStates[flag]);
        }
    }

    [HarmonyPatch(typeof(Player), "OnKilledByPlayer")]
    class CTFOnKilledByPlayer
    {
        private static void Prefix(Player __instance, DamageInfo di)
        {
            if (!NetworkServer.active)
                return;

            if (!CTF.PlayerHasFlag.TryGetValue(__instance.netId, out int flag))
                return;

            Player player = null;
            if (di.owner != null)
            {
                player = di.owner.GetComponent<Player>();
            }

            if (player == null || player.netId == __instance.netId)
                return;

            ServerStatLog.AddFlagEvent(player, "CarrierKill");
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

    /*
    [HarmonyPatch(typeof(NetworkSpawnItem), "NetworkSpawnItemHandler")]
    class CTFLogSpawn
    {
        private static void Prefix(NetworkHash128 asset_id)
        {
            GameObject prefabFromAssetId = Client.GetPrefabFromAssetId(asset_id);
            
        .Log("client " + Client.GetClient().connection.connectionId + " item spawning " + asset_id + " = " + (prefabFromAssetId == null ? "???" : prefabFromAssetId.name));
        }
    }
    */

    [HarmonyPatch(typeof(NetworkSpawnItem), "RegisterSpawnHandlers")]
    class CTFRegisterSpawnHandlers
    {
        private static Dictionary<NetworkHash128, GameObject> m_registered_prefabs = new Dictionary<NetworkHash128, GameObject>();

        private static GameObject NetworkSpawnItemHandler(Vector3 pos, NetworkHash128 asset_id)
        {
            Debug.Log("client " + NetworkMatch.m_my_lobby_id + " flag spawning " + asset_id);
            GameObject prefabFromAssetId = m_registered_prefabs[asset_id];
            if (prefabFromAssetId == null)
            {
                Debug.LogErrorFormat("Error looking up item prefab with asset_id {0}", asset_id.ToString());
                return null;
            }
            GameObject gameObject = UnityEngine.Object.Instantiate(prefabFromAssetId, pos, Quaternion.identity);
            if (gameObject == null)
            {
                Debug.LogErrorFormat("Error instantiating item prefab {0}", prefabFromAssetId.name);
                return null;
            }
            gameObject.SetActive(true);
            Debug.Log("Spawning flag " + asset_id + " active " + gameObject.activeSelf + " seg " + gameObject.GetComponent<Item>().m_current_segment);

            var netId = gameObject.GetComponent<NetworkIdentity>();
            netId.GetType().GetField("m_AssetId", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(netId, asset_id);
            Debug.Log("post spawn assetid " + gameObject.GetComponent<NetworkIdentity>().assetId);

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
                    Debug.LogWarningFormat("Failed to instantiate prefab: {0} in RegisterSpawnHandlers", prefab.name);
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
                Debug.Log("Flag " + i + " color " + color + " assetid " + flag.GetComponent<NetworkIdentity>().assetId);
                //Client.RegesterSpawnHandler(flag, NetworkSpawnItemHandler, NetworkUnspawnItemHandler);
                m_registered_prefabs.Add(assetId, flag);
                ClientScene.RegisterSpawnHandler(assetId, NetworkSpawnItemHandler, NetworkUnspawnItemHandler);
            }
            Debug.Log("RegisterSpawnHandlers: now " + CTF.FlagObjs.Count + " flags");
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "StartPlaying")]
    class CTFStartPlaying
    {
        private static void SpawnFlags()
        {
            /*
            var LoadScreenStillNeeded = typeof(MenuManager).GetMethod("LoadScreenStillNeeded", BindingFlags.NonPublic | BindingFlags.Static);
            while ((bool)LoadScreenStillNeeded.Invoke(null, null))
                yield return null;
            Debug.Log("CTF.SpawnFlags: scene fully loaded");
            */
            for (int i = 0; i < CTF.TeamCount; i++)
                CTF.SpawnAtHome(i);
        }

        private static void Postfix()
        {
            if (NetworkMatch.GetMode() != CTF.MatchModeCTF || !Overload.NetworkManager.IsServer())
                return;
            //GameManager.m_gm.StartCoroutine(SpawnFlags());
            CTF.InitForLevel();
            SpawnFlags();
            /*

            GameObject gameObject = UnityEngine.Object.Instantiate(monsterball, Vector3.zero, Quaternion.identity);
            if (gameObject != null)
            {
                m_monsterball = gameObject.GetComponent<MonsterBall>();
                NetworkHash128 assetId = gameObject.GetComponent<NetworkIdentity>().assetId;
                NetworkServer.Spawn(gameObject, assetId);
            }
            */
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

            if (NetworkMatch.m_postgame)
            {
                return false;
            }

            if (Overload.NetworkManager.IsServer())
            {
                //Debug.Log("OnTriggerEnter KEY_SECURITY is reachable on server");
                if (!CTF.Pickup(c_player, __instance.m_index))
                    return false;
                //Debug.Log("OnTriggerEnter KEY_SECURITY is reachable on server, picked up");
                c_player.CallRpcPlayItemPickupFX(__instance.m_type, __instance.m_super);
                UnityEngine.Object.Destroy(__instance.c_go);
            }
            return false;
        }
    }

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
            /*
            __instance.DrawOutlineBackdrop(pos, 11f, 32f, col_ub, 2);
            temp_pos.y = pos.y;
            temp_pos.x = pos.x - 13f;
            UIManager.DrawSpriteUI(temp_pos, 0.1f, 0.1f, UIManager.m_col_super1, m_alpha, 83);
            temp_pos.x = pos.x + 16f;
            __instance.DrawDigitsTwo(temp_pos, GameManager.m_local_player.m_upgrade_points2, 0.45f, StringOffset.RIGHT, col_ui, m_alpha);
            */
            //pos.y -= 24f;
            /*
            __instance.DrawOutlineBackdrop(pos, 11f, 32f, col_ub, 2);
            temp_pos.y = pos.y;
            temp_pos.x = pos.x - 13f;
            UIManager.DrawSpriteUI(temp_pos, 0.1f, 0.1f, col_ui, m_alpha, 82);
            temp_pos.x = pos.x + 16f;
            __instance.DrawDigitsTwo(temp_pos, GameManager.m_local_player.m_upgrade_points1, 0.45f, StringOffset.RIGHT, col_ui, m_alpha);
            */
            //pos.y -= 24f;
            pos.y -= 16f;
            //__instance.DrawOutlineBackdrop(pos, 32f, 32f, col_ub, 2);
            temp_pos.y = pos.y;
            temp_pos.x = pos.x - 13f;
            m_anim_state = (m_anim_state + RUtility.FRAMETIME_UI) % ((float)Math.PI * 2f);
            //GameManager.m_local_player.connectionToServer.
            UIManager.DrawSpriteUIRotated(temp_pos, 0.2f, 0.2f, m_anim_state,
                //(float)Math.PI / 2f, 
                CTF.FlagColorUI(flag), m_alpha, 84);
            //temp_pos.x = pos.x + 7f;
            //__instance.DrawDigitsOne(temp_pos, (int)GameManager.m_local_player.m_unlock_level, 0.45f, col_ui, m_alpha);
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

    public class CTFCustomMsg
    {
        public const short MsgCTFPickup = 121;
        public const short MsgCTFLose = 122;
        public const short MsgCTFNotify = 123;
        public const short MsgCTFFlagUpdate = 124;
    }

    [HarmonyPatch(typeof(Client), "RegisterHandlers")]
    class CTFClientHandlers
    {
        private static void OnCTFPickup(NetworkMessage rawMsg)
        {
            var msg = rawMsg.ReadMessage<PlayerFlagMessage>();
            if (!CTF.PlayerHasFlag.ContainsKey(msg.m_player_id))
                CTF.PlayerHasFlag.Add(msg.m_player_id, msg.m_flag_id);
            CTF.FlagStates[msg.m_flag_id] = msg.m_flag_state;
            //SFXCueManager.PlayRawSoundEffect2D(SoundEffect.hud_notify_message1);
        }

        private static void OnCTFLose(NetworkMessage rawMsg)
        {
            var msg = rawMsg.ReadMessage<PlayerFlagMessage>();
            if (CTF.PlayerHasFlag.ContainsKey(msg.m_player_id))
                CTF.PlayerHasFlag.Remove(msg.m_player_id);
            CTF.FlagStates[msg.m_flag_id] = msg.m_flag_state;
            //SFXCueManager.PlayRawSoundEffect2D(SoundEffect.hud_notify_message1);
        }

        private static void OnCTFNotify(NetworkMessage rawMsg)
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

        static void Postfix()
        {
            if (Client.GetClient() == null)
                return;
            Client.GetClient().RegisterHandler(CTFCustomMsg.MsgCTFPickup, OnCTFPickup);
            Client.GetClient().RegisterHandler(CTFCustomMsg.MsgCTFLose, OnCTFLose);
            Client.GetClient().RegisterHandler(CTFCustomMsg.MsgCTFNotify, OnCTFNotify);
            Client.GetClient().RegisterHandler(CTFCustomMsg.MsgCTFFlagUpdate, OnCTFFlagUpdate);
        }
    }

    /*
    [HarmonyPatch(typeof(PlayerShip), "MaybeFireWeapon")]
    class CTFDrop
    {
        static void Prefix(PlayerShip __instance)
        {
            if (Overload.NetworkManager.IsServer() && CTF.PlayerHasKey.TryGetValue(__instance.c_player.netId, out int flag))
                CTF.Drop(player.m_mp_team);
        }
    }
    */

    /*
    [HarmonyPatch(typeof(Item), "UpdateSegmentIndex")]
    class CTFUpdateSegmentIndex
    {
        static void Postfix(ItemType ___m_type, bool ___m_stationary, Rigidbody ___c_rigidbody, int ___m_current_segment)
        {
            Debug.Log("Item.UpdateSegmentIndex server " + Overload.NetworkManager.IsServer() + " type " + ___m_type + " ___m_stationary " + ___m_stationary + " ___c_rigidbody=null " + (___c_rigidbody == null) + " ___m_current_segment " + ___m_current_segment);
        }
    }

    [HarmonyPatch(typeof(RobotManager), "ItemInRelevantSegment")]
    class CTFItemInRelevantSegment
    {
        static void Postfix(Item item)
        {
            Debug.Log("RobotManager.ItemInRelevantSegment server " + Overload.NetworkManager.IsServer() + " type " + item.m_type);
        }
    }
    */

    [HarmonyPatch(typeof(MonsterBallGoal), "OnTriggerEnter")]
    internal class CTFScore
    {
        private static void Prefix(Collider other, MonsterBallGoal __instance)
        {
            if (NetworkMatch.GetMode() != CTF.MatchModeCTF || !Overload.NetworkManager.IsServer() || other.attachedRigidbody == null)
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
            if (!Overload.NetworkManager.IsServer() || !GameplayManager.IsMultiplayerActive || NetworkMatch.GetMode() != CTF.MatchModeCTF)
                return;
            if (!CTF.PlayerHasFlag.TryGetValue(__instance.c_player.netId, out int flag))
                return;
            CTF.SendCTFLose(__instance.c_player.netId, flag, FlagState.LOST);
            CTF.SpewFlag(flag, __instance);
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "Update")]
    class CTFRing
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
        private static void DrawFlagState(Vector2 pos, float m_alpha, int flag)
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
            }
        }

        private static void Prefix(Vector2 pos, float ___m_alpha)
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
                DrawFlagState(pos, ___m_alpha, i);
                pos.x += 60;
            }
            /*
            UIManager.DrawSpriteUIRotated(pos, 0.3f, 0.3f, Mathf.PI / 2f,
                MPTeams.TeamColor(MPTeams.AllTeams[0], 4), ___m_alpha, (int)AtlasIndex0.ICON_SECURITY_KEY1);

            pos.x += 60f;

            UIManager.DrawSpriteUIRotated(pos, 0.3f, 0.3f, Mathf.PI / 2f,
                MPTeams.TeamColor(MPTeams.AllTeams[1], 4), ___m_alpha, (int)AtlasIndex0.ICON_SECURITY_KEY1);

            // test
            pos.x -= 60f;
            pos.y += 60f;
            //UIManager.DrawSpriteUIRotated(pos, 0.3f, 0.3f, Mathf.PI / 2f,
            //    MPTeams.TeamColor(MPTeams.AllTeams[0], 0) * .4f, ___m_alpha, 84);

            //UIManager.DrawSpriteUIRotated(pos + new Vector2(-30f, -30f), 0.2f, 0.2f, Mathf.PI / 2f,
            //    MPTeams.TeamColor(MPTeams.AllTeams[1], 4), ___m_alpha, 84);

            UIManager.DrawSpriteUIRotated(pos, 0.4f, 0.4f, Mathf.PI / 2f,
                MPTeams.TeamColor(MPTeams.AllTeams[1], 4), ___m_alpha, (int)AtlasIndex0.RING_MED0);

            UIManager.DrawSpriteUIRotated(pos, 0.18f, 0.18f, Mathf.PI / 2f,
                MPTeams.TeamColor(MPTeams.AllTeams[0], 4) * 0.5f, ___m_alpha, (int)AtlasIndex0.ICON_SECURITY_KEY1);

            pos.x += 60f;

            UIManager.DrawSpriteUIRotated(pos, 0.4f, 0.4f, Mathf.PI / 2f,
                MPTeams.TeamColor(MPTeams.AllTeams[0], 4), ___m_alpha, (int)AtlasIndex0.RING_MED0);

            UIManager.DrawSpriteUIRotated(pos, 0.18f, 0.18f, Mathf.PI / 2f,
                MPTeams.TeamColor(MPTeams.AllTeams[1], 4) * 0.5f, ___m_alpha, (int)AtlasIndex0.ICON_SECURITY_KEY1);

            //UIManager.DrawSpriteUIRotated(pos, 0.3f, 0.3f, Mathf.PI / 2f,
            //    MPTeams.TeamColor(MPTeams.AllTeams[1], 0), ___m_alpha * .2f, 84);

            //UIManager.DrawSpriteUIRotated(pos + new Vector2(-30f, -30f), 0.2f, 0.2f, Mathf.PI / 2f,
            //    MPTeams.TeamColor(MPTeams.AllTeams[0], 4), ___m_alpha, 84);
            //UIManager.DrawCharQuad(pos, 0xd7, 0.3f, MPTeams.TeamColor(MPTeams.AllTeams[0], 4));
            //UIManager.DrawStringScaled("\xd7", pos + new Vector2(-30f, 0), Vector2.zero, 50f, MPTeams.TeamColor(MPTeams.AllTeams[0], 3), -1f, 0f, 0f);
            */
        }
    }

    #if false
    [HarmonyPatch(typeof(Overload.MenuManager), "MpMatchSetup")]
    class CTFModeSel
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int n = 0;
            var codes = new List<CodeInstruction>(instructions);
            for (var i = 0; i < codes.Count; i++)
            {
                // increase max mode to allow ctf mode
                if (codes[i].opcode == OpCodes.Ldsfld && (codes[i].operand as FieldInfo).Name == "mms_mode")
                {
                    i++;
                    if (codes[i].opcode == OpCodes.Ldc_I4_2 || codes[i].opcode == OpCodes.Ldc_I4_3) // maybe already changed for MB
                        codes[i].opcode = OpCodes.Ldc_I4_4;
                    i++;
                    while (codes[i].opcode == OpCodes.Add || codes[i].opcode == OpCodes.Ldsfld)
                        i++;
                    if (codes[i].opcode == OpCodes.Ldc_I4_2 || codes[i].opcode == OpCodes.Ldc_I4_3)  // maybe already changed for MB
                        codes[i].opcode = OpCodes.Ldc_I4_4;
                    n++;
                }
            }
            return codes;
        }
    }
#endif
}
