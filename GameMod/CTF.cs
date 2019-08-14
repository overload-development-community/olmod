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

namespace GameMod
{
    static class CTF
    {
        public const MatchMode MatchModeCTF = MatchMode.NUM;
        public static List<GameObject> FlagObjs = new List<GameObject>();
        public static bool IsActive { get { return NetworkMatch.GetMode() == CTF.MatchModeCTF && GameplayManager.IsMultiplayerActive; } }
        public static bool[] HasSpawnPoint = new bool[2];
        public static Vector3[] SpawnPoint = new Vector3[2];
        public const int TeamCount = 2;
        public static Color FlagColor(int teamIdx)
        {
            return MPTeams.TeamColor(MPTeams.AllTeams[teamIdx], teamIdx == 1 ? 2 : 3); // make orange a bit darker
        }
        public static Color FlagColorUI(int teamIdx)
        {
            return MPTeams.TeamColor(MPTeams.AllTeams[teamIdx], 1);
        }
        public static Dictionary<NetworkInstanceId, int> PlayerHasKey = new Dictionary<NetworkInstanceId, int>();

        public static void SendCTFPickup(NetworkInstanceId player_id, int flag_id)
        {
            NetworkServer.SendToAll(CTFCustomMsg.MsgCTFPickup, new PlayerFlagMessage { m_player_id = player_id, m_flag_id = flag_id });
        }

        public static void SendCTFLose(NetworkInstanceId player_id, int flag_id)
        {
            NetworkServer.SendToAll(CTFCustomMsg.MsgCTFLose, new PlayerFlagMessage { m_player_id = player_id, m_flag_id = flag_id });
        }
        public static void InitForLevel()
        {
            PlayerHasKey.Clear();
            HasSpawnPoint = new bool[CTF.TeamCount];
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
        public static bool Pickup(Player player, int flag)
        {
            if (flag < 0 || flag >= FlagObjs.Count || PlayerHasKey.ContainsKey(player.netId) || MPTeams.AllTeams[flag] == player.m_mp_team)
                return false;
            SendCTFPickup(player.netId, flag);
            // this will also send to 'client 0' so it'll get added on the server as well
            //PlayerHasKey.Add(player.connectionToClient.connectionId, flag);
            player.CallTargetAddHUDMessage(player.connectionToClient, string.Format(Loc.LS("PICKED UP {0} {1}!"), MPTeams.TeamName(MPTeams.AllTeams[flag]), Loc.LS("FLAG")), -1, true);
            foreach (var pl in Overload.NetworkManager.m_Players)
                if (!System.Object.ReferenceEquals(pl, player))
                    pl.CallTargetAddHUDMessage(pl.connectionToClient, player.m_mp_name + " PICKED UP A FLAG!", -1, true);
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
            if (!PlayerHasKey.TryGetValue(player.netId, out int flag)) {
                Debug.Log("CTF.Drop: player " + player.m_mp_name + " has no flag!");
                return;
            }
            //var player = ClientScene.FindLocalObject(player_id).GetComponent<Player>();
            SendCTFLose(player.netId, flag);
            Spawn(flag);
            //SendCTFPickup(player.connectionToClient.connectionId, flag);
            // this will also send to 'client 0' so it'll get added on the server as well
            //PlayerHasKey.Add(player.connectionToClient.connectionId, flag);
        }
        public static void Spawn(int flag_id)
        {
            if (flag_id >= HasSpawnPoint.Length || !HasSpawnPoint[flag_id]) // flag_id >= GameManager.m_level_data.m_player_spawn_points.Length || flag_id > 2) // || flag_id >= CTF.FlagObjs.Count)
            {
                Debug.Log("No spawn point for flag " + flag_id);
                return;
            }
            //var pos = GameManager.m_level_data.m_player_spawn_points[flag_id].position;
            //var prefab = PrefabManager.item_prefabs[(int)ItemPrefab.entity_item_log_entry];
            var prefab = CTF.FlagObjs[flag_id];
            if (prefab == null)
            {
                Debug.LogWarningFormat("Missing prefab for flag {0}", flag_id);
                return;
            }
            Debug.Log("Server spawning flag " + flag_id);
            //Item.Spew(prefab, pos, Vector3.zero);return;
            GameObject flag = UnityEngine.Object.Instantiate(prefab, SpawnPoint[flag_id], Quaternion.identity);
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
        }
        public static NetworkHash128 FlagAssetId(int flag)
        {
            var id = "07e810adf1a9f1a9f1a9";
            return NetworkHash128.Parse(id + flag.ToString("x4"));
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
            Debug.Log("client " + Client.GetClient().connection.connectionId + " item spawning " + asset_id + " = " + (prefabFromAssetId == null ? "???" : prefabFromAssetId.name));
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
                CTF.Spawn(i);
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
            if (__instance.m_type != ItemType.KEY_SECURITY || !GameplayManager.IsMultiplayerActive)
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
            if (!CTF.PlayerHasKey.TryGetValue(GameManager.m_local_player.netId, out int flag))
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
            writer.Write((byte)0); // version
            writer.Write(m_player_id);
            writer.WritePackedUInt32((uint)m_flag_id);
        }
        public override void Deserialize(NetworkReader reader)
        {
            var version = reader.ReadByte();
            m_player_id = reader.ReadNetworkId();
            m_flag_id = (int)reader.ReadPackedUInt32();
        }
        public NetworkInstanceId m_player_id;
        public int m_flag_id;
    }

    public class CTFCustomMsg
    {
        public const short MsgCTFPickup = 121;
        public const short MsgCTFLose = 122;
    }

    [HarmonyPatch(typeof(Client), "RegisterHandlers")]
    class CTFClientHandlers
    {
        private static void OnCTFPickup(NetworkMessage rawMsg)
        {
            var msg = rawMsg.ReadMessage<PlayerFlagMessage>();
            CTF.PlayerHasKey.Add(msg.m_player_id, msg.m_flag_id);
            SFXCueManager.PlayRawSoundEffect2D(SoundEffect.hud_notify_message1);
        }

        private static void OnCTFLose(NetworkMessage rawMsg)
        {
            var msg = rawMsg.ReadMessage<PlayerFlagMessage>();
            CTF.PlayerHasKey.Remove(msg.m_player_id);
            SFXCueManager.PlayRawSoundEffect2D(SoundEffect.hud_notify_message1);
        }

        static void Postfix()
        {
            if (Client.GetClient() == null)
                return;
            Client.GetClient().RegisterHandler(CTFCustomMsg.MsgCTFPickup, OnCTFPickup);
            Client.GetClient().RegisterHandler(CTFCustomMsg.MsgCTFLose, OnCTFLose);
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
            if (team == __instance.m_team && CTF.PlayerHasKey.ContainsKey(player.netId))
            {
                CTF.Drop(player);
                NetworkMatch.m_team_scores[(int)team] += 5;
                typeof(NetworkMatch).GetMethod("SendUpdatedTeamScoreToClients", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { team });

                player.CallTargetAddHUDMessage(player.connectionToClient, "YOU HAVE SCORED!", -1, true);
                foreach (var pl in Overload.NetworkManager.m_Players)
                    if (!System.Object.ReferenceEquals(pl, player))
                        pl.CallTargetAddHUDMessage(pl.connectionToClient, player.m_mp_name + " HAS SCORED!", -1, true);
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
