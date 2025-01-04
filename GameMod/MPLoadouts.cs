using HarmonyLib;
using Overload;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace GameMod
{
    public class MPLoadouts
    {
        public const WeaponType DefaultWeaponEnd = WeaponType.LANCER; // all weapons in the enum before that are not part of default loadouts
        public const MissileType DefaultMissileEnd = MissileType.NOVA; // all missiles in the enum before that are not part of default loadouts
        public const int MASK_ALL_WEAPONS = (1 << (int)WeaponType.NUM) - 1;
        public const int MASK_ALL_MISSILES = (( 1 << (int)MissileType.NUM) -1) << (int)WeaponType.NUM;
        public const int MASK_DEFAULT_WEAPONS = (1 << (int)DefaultWeaponEnd) - 1;
        public const int MASK_DEFAULT_MISSILES = (( 1 << (int)DefaultMissileEnd) -1) << (int)WeaponType.NUM;

        public const int MASK_DEFAULT = (MASK_DEFAULT_WEAPONS | MASK_DEFAULT_MISSILES);
        public static int LoadoutFilterBitmask = MASK_DEFAULT;

        private static int GetFilterBit(WeaponType weapon)
        {
            int w = (int)weapon;
            if (w < 0 || w >= (int)WeaponType.NUM) {
                return 0;
            }
            return (1<<w);
        }

        private static int GetFilterBit(MissileType missile)
        {
            int m = (int)missile;
            if (m < 0 || m >= (int)MissileType.NUM) {
                return 0;
            }
            return (1<<(m + (int)MissileType.NUM));
        }

        private static void SetFilterBit(int bit, bool allowed = true)
        {
            if (allowed) {
                LoadoutFilterBitmask |= bit;
            } else {
                LoadoutFilterBitmask &= ~bit;
            }
        }

        public static bool IsAllowedByFilter(WeaponType weapon, int filter)
        {
            if (weapon >= WeaponType.NUM) {
                return true; // the "no such weapon" is also always allowed
            }
            return ( (filter & GetFilterBit(weapon)) != 0);
        }

        public static bool IsAllowedByFilter(MissileType missile, int filter)
        {
            if (missile >= MissileType.NUM) {
                return true; // the "no such missile" is also always allowed
            }
            return ( (filter & GetFilterBit(missile)) != 0);
        }

        public static bool IsAllowed(WeaponType weapon)
        {
            return IsAllowedByFilter(weapon, LoadoutFilterBitmask);
        }

        public static bool IsAllowed(MissileType missile)
        {
            return IsAllowedByFilter(missile, LoadoutFilterBitmask);
        }

        public static void SetAllowed(WeaponType weapon, bool allowed = true)
        {
            SetFilterBit(GetFilterBit(weapon), allowed);
        }

        public static void SetAllowed(MissileType missile, bool allowed = true)
        {
            SetFilterBit(GetFilterBit(missile), allowed);
        }

        private static WeaponType FindFirstUnusedAllowed(List<WeaponType> weapons, int filter)
        {
            for (WeaponType weapon = (WeaponType)0; weapon < WeaponType.NUM; weapon++) {
                if (IsAllowedByFilter(weapon, filter) && !weapons.Contains(weapon)) {
                    return weapon;
                }
            }
            return WeaponType.NUM;
        }

        private static MissileType FindFirstUnusedAllowed(List<MissileType> missiles, int filter)
        {
            for (MissileType missile = (MissileType)0; missile < MissileType.NUM; missile++) {
                if (IsAllowedByFilter(missile, filter) && !missiles.Contains(missile)) {
                    return missile;
                }
            }
            return MissileType.NUM;
        }

        private static int CountAllowed(int filter)
        {
            int cnt = 0;
            for (int i=0; i<((int)WeaponType.NUM+(int)MissileType.NUM); i++) {
                if ( (filter & (1<<i)) != 0) {
                    cnt++;
                }
            }
            return cnt;
        }

        public static List<string> GetItems(int filter, ref string mode)
        {
            int cntDefault = CountAllowed(filter & MASK_DEFAULT);
            int cntAll = CountAllowed(filter & (MASK_ALL_WEAPONS | MASK_ALL_MISSILES));
            int cntDefaultMax = CountAllowed(MASK_DEFAULT);
            if ((cntAll > 0) && (cntDefault > cntDefaultMax / 2)) {
                // more than half of the default elements are allowed, list only the disabled ones
                mode = "DISABLED IN LOADOUTS:";
                return GetDefaultItems(filter, false, (WeaponType)0, DefaultWeaponEnd, (MissileType)0, DefaultMissileEnd);
            }
            mode = "RESTRICTED LOADOUTS:";
            if (cntAll < 1) {
                return new List<string>() {"NONE"};
            }
            return GetDefaultItems(filter, true, (WeaponType)0, WeaponType.NUM, (MissileType)0, MissileType.NUM);
        }

        public static List<string> GetDefaultItems(int filter, bool allowed, WeaponType wStart, WeaponType wEnd, MissileType mStart, MissileType mEnd)
        {
            List<string> list = new List<string>();
            for (WeaponType weapon = wStart; weapon < wEnd; weapon++) {
                bool isAllowed = IsAllowedByFilter(weapon, filter);
                if ((allowed && isAllowed) || (!allowed && !isAllowed)) {
                    list.Add(Player.GetWeaponNameNoDefault(weapon));
                }
            }
            for (MissileType missile = mStart; missile < mEnd; missile++) {
                bool isAllowed = IsAllowedByFilter(missile, filter);
                if ((allowed && isAllowed) || (!allowed && !isAllowed)) {
                    list.Add(Player.GetMissileNameNoDefault(missile));
                }
            }
            return list;
        }

        public static Dictionary<int, LoadoutDataMessage> NetworkLoadouts = new Dictionary<int, LoadoutDataMessage>();

        public static CustomLoadout[] Loadouts = new CustomLoadout[4]
        {
            new BomberLoadout(WeaponType.IMPULSE, MissileType.MISSILE_POD, MissileType.HUNTER),
            new GunnerLoadout(WeaponType.DRILLER, WeaponType.CYCLONE, MissileType.HUNTER),
            new BomberLoadout(WeaponType.THUNDERBOLT, MissileType.FALCON, MissileType.HUNTER),
            new GunnerLoadout(WeaponType.CRUSHER, WeaponType.CYCLONE, MissileType.HUNTER)
        };

        public enum LoadoutType
        {
            BOMBER = 0, // One primary, two secondaries
            GUNNER = 1  // Two primaries, one secondary
        }

        public class CustomLoadout
        {
            public LoadoutType loadoutType;
            public List<WeaponType> weapons;
            public List<MissileType> missiles;

            public CustomLoadout()
            {
                weapons = new List<WeaponType>();
                missiles = new List<MissileType>();
            }

            public string Describe()
            {
                string desc = "weapons: {";
                for(int i=0; i< weapons.Count; i++) {
                    if (i > 0) {
                        desc = desc + ", ";
                    }
                    desc = desc + Player.GetWeaponNameNoDefault(weapons[i]);
                }
                desc = desc + "} missiles: {";
                for(int i=0; i<missiles.Count; i++) {
                    if (i > 0) {
                        desc = desc + ", ";
                    }
                    desc = desc + Player.GetMissileNameNoDefault(missiles[i]);
                }
                desc = desc + "}";
                return desc;
            }

            private static void CleanupList<T>(List<T> l, T emptyMarker)
            {
                if (l.Count < 1) {
                    return;
                }

                for (int i=0; i<l.Count; i++) {
                    if (l[i].Equals(emptyMarker)) {
                        int found = -1;
                        // check if any non-empty element is present later in the list
                        for (int j=i+1; j<l.Count; j++) {
                            if (!l[j].Equals(emptyMarker)) {
                                found = j;
                                break;
                            }
                        }
                        if (found >= 0) {
                            // swap it
                            l[i] = l[found];
                            l[found] = emptyMarker;
                        } else {
                            break;
                        }
                    }
                }
            }

            public int ApplyFilter(int filter)
            {
                int i;
                int replaced = 0;
                int weaponCnt = weapons.Count;
                int missileCnt = missiles.Count;

                //Debug.LogFormat("Loadout filter: XX: {0}", filter);
                //Debug.LogFormat("Loadout filter: before: {0}", Describe());

                // filter out not allowed entries, replace by allowed ones
                for (i=0; i<weapons.Count; i++) {
                    if (!IsAllowedByFilter(weapons[i], filter)) {
                        weapons[i] = WeaponType.NUM;
                        replaced++;
                    }
                    if (weapons[i] == WeaponType.NUM) {
                        weapons[i] = FindFirstUnusedAllowed(weapons, filter);
                    }
                }
                for (i=0; i<missiles.Count; i++) {
                    if (!IsAllowedByFilter(missiles[i], filter)) {
                        missiles[i] = MissileType.NUM;
                        replaced++;
                    }
                    if (missiles[i] == MissileType.NUM) {
                        missiles[i] = FindFirstUnusedAllowed(missiles, filter);
                    }
                }
                // sort the available entries before all "NUM" empty markers
                CleanupList<WeaponType>(weapons, WeaponType.NUM);
                CleanupList<MissileType>(missiles, MissileType.NUM);

                /*
                // Overload crashes when used without any weapon
                if (weapons.Count > 0 && weapons[0] == WeaponType.NUM) {
                    weapons[0] = (WeaponType)0;
                }
                */


                //Debug.LogFormat("Loadout filter: after: {0}", Describe());
                return replaced;
            }

            public void ApplyFilter()
            {
                ApplyFilter(LoadoutFilterBitmask);
            }

            public void ImportLegacyLoadout(Overload.LoadoutDataMessage ldm, int index)
            {
                weapons.Clear();
                weapons.Add(ldm.GetMpLoadoutWeapon1(index));
                WeaponType w = ldm.GetMpLoadoutWeapon2(index);
                if (w != WeaponType.NUM) {
                    weapons.Add(w);
                    loadoutType = LoadoutType.GUNNER;
                } else {
                    loadoutType = LoadoutType.BOMBER;
                }

                missiles.Clear();
                missiles.Add(ldm.GetMpLoadoutMissile1(index));
                if (loadoutType == LoadoutType.BOMBER) {
                    missiles.Add(ldm.GetMpLoadoutMissile2(index));
                }
            }

            public int ExportToLegacyLoadout(Overload.LoadoutDataMessage ldm)
            {
                if (loadoutType == LoadoutType.BOMBER) {
                    for (int i=0; i<6; i++) {
                        MissileType m1 = ldm.GetMpLoadoutMissile1(i);
                        MissileType m2 = ldm.GetMpLoadoutMissile2(i);
                        if (ldm.GetMpLoadoutWeapon1(i)  == weapons[0] && ( (m1 == missiles[0] && m2 == missiles[1]) || (m1 == missiles[1] && m2 == missiles[0]) )) {
                            return i;
                        }
                    }
                    ldm.m_mp_custom1_w1 = weapons[0];
                    ldm.m_mp_custom1_m1 = missiles[0];
                    ldm.m_mp_custom1_m2 = missiles[1];
                    return 6;
                }
                for (int i=0; i<6; i++) {
                    WeaponType w1 = ldm.GetMpLoadoutWeapon1(i);
                    WeaponType w2 = ldm.GetMpLoadoutWeapon2(i);
                    if ( ((w1 == weapons[0] && w2 == weapons[1]) || (w1 == weapons[1] && w2 == weapons[0])) && ldm.GetMpLoadoutMissile1(i) == missiles[0]) {
                        return i;
                    }
                }
                ldm.m_mp_custom2_w1 = weapons[0];
                ldm.m_mp_custom2_w2 = weapons[1];
                ldm.m_mp_custom2_m1 = missiles[0];
                return 7;
            }
        }

        public class BomberLoadout : CustomLoadout
        {
            public BomberLoadout(WeaponType weapon, MissileType missile1, MissileType missile2)
            {
                loadoutType = LoadoutType.BOMBER;
                weapons = new List<WeaponType>() { weapon };
                missiles = new List<MissileType>() { missile1, missile2 };
            }
        }

        public class GunnerLoadout : CustomLoadout
        {
            public GunnerLoadout(WeaponType weapon1, WeaponType weapon2, MissileType missile)
            {
                loadoutType = LoadoutType.GUNNER;
                weapons = new List<WeaponType>() { weapon1, weapon2 };
                missiles = new List<MissileType>() { missile };
            }
        }

        public class LoadoutDataMessage : MessageBase
        {
            public override void Serialize(NetworkWriter writer)
            {
                writer.WritePackedUInt32((uint)this.lobby_id);
                writer.WritePackedUInt32((uint)this.selected_idx);
                writer.WritePackedUInt32((uint)loadouts.Count);
                for (int i = 0; i < loadouts.Count; i++)
                {
                    writer.WritePackedUInt32((uint)i);
                    writer.WritePackedUInt32((uint)loadouts[i].loadoutType);
                    writer.WritePackedUInt32((uint)loadouts[i].weapons.Count);
                    for (int j = 0; j < loadouts[i].weapons.Count; j++)
                    {
                        writer.WritePackedUInt32((uint)loadouts[i].weapons[j]);
                    }
                    writer.WritePackedUInt32((uint)loadouts[i].missiles.Count);
                    for (int j = 0; j < loadouts[i].missiles.Count; j++)
                    {
                        writer.WritePackedUInt32((uint)loadouts[i].missiles[j]);
                    }
                }
            }

            public override void Deserialize(NetworkReader reader)
            {
                loadouts = new List<CustomLoadout>();
                this.lobby_id = (int)reader.ReadPackedUInt32();
                this.selected_idx = (int)reader.ReadPackedUInt32();
                uint numLoadouts = reader.ReadPackedUInt32();
                for (int i = 0; i < numLoadouts; i++)
                {
                    uint loadoutIndex = reader.ReadPackedUInt32();
                    CustomLoadout loadout = new CustomLoadout();
                    loadout.loadoutType = (LoadoutType)reader.ReadPackedUInt32();
                    uint weaponCount = reader.ReadPackedUInt32();
                    for (int j = 0; j < weaponCount; j++)
                    {
                        loadout.weapons.Add((WeaponType)reader.ReadPackedUInt32());
                    }
                    uint missileCount = reader.ReadPackedUInt32();
                    for (int j = 0; j < missileCount; j++)
                    {
                        loadout.missiles.Add((MissileType)reader.ReadPackedUInt32());
                    }
                    loadouts.Add(loadout);
                }
            }

            public int lobby_id;
            public int selected_idx;
            public List<CustomLoadout> loadouts;
        }

        public class SetCustomLoadoutMessage : MessageBase
        {
            public override void Serialize(NetworkWriter writer)
            {
                writer.WritePackedUInt32((uint)this.lobby_id);
                writer.WritePackedUInt32((uint)this.selected_idx);
            }

            public override void Deserialize(NetworkReader reader)
            {
                this.lobby_id = (int)reader.ReadPackedUInt32();
                this.selected_idx = (int)reader.ReadPackedUInt32();
            }

            public int lobby_id;
            public int selected_idx;
        }

        // Process client's UI selection of cycling custom loadout weapon
        public static void MpCycleWeapon(int loadoutIndex, int weaponIndex)
        {
            MPLoadouts.Loadouts[loadoutIndex].weapons[weaponIndex] = (WeaponType)((((int)MPLoadouts.Loadouts[loadoutIndex].weapons[weaponIndex]) + 1) % (int)WeaponType.LANCER);

            Func<bool> IsValidPrimary = () =>
            {
                return MPLoadouts.Loadouts[loadoutIndex].weapons[weaponIndex] != WeaponType.REFLEX
                    && MPLoadouts.Loadouts[loadoutIndex].weapons.Count(x => x == MPLoadouts.Loadouts[loadoutIndex].weapons[weaponIndex]) <= 1;
            };

            if (IsValidPrimary())
                return;

            // A little bit awkward - we need to ensure that we didn't land on reflex and keep trying to
            // increment until we find a valid combo.  Bunch of extra code to avoid a simple 'while (!success)' or recursion
            // if we get wild with modifying things in the future :).
            for (int i = 0; i < (int)WeaponType.LANCER; i++)
            {
                MPLoadouts.Loadouts[loadoutIndex].weapons[weaponIndex] = (WeaponType)((((int)MPLoadouts.Loadouts[loadoutIndex].weapons[weaponIndex]) + 1) % (int)WeaponType.LANCER);
                if (IsValidPrimary())
                    break;
            }
        }

        // Process client's UI selection of cycling custom loadout missile
        public static void MpCycleMissile(int loadoutIndex, int missileIndex)
        {
            MPLoadouts.Loadouts[loadoutIndex].missiles[missileIndex] = (MissileType)((((int)MPLoadouts.Loadouts[loadoutIndex].missiles[missileIndex]) + 1) % (int)MissileType.NOVA);

            // Skip to next if we've landed on an already selected missile
            if (MPLoadouts.Loadouts[loadoutIndex].missiles.Count(x => x == MPLoadouts.Loadouts[loadoutIndex].missiles[missileIndex]) > 1)
                MPLoadouts.Loadouts[loadoutIndex].missiles[missileIndex] = (MissileType)((((int)MPLoadouts.Loadouts[loadoutIndex].missiles[missileIndex]) + 1) % (int)MissileType.NOVA);
        }

        // Action for input binding "Toggle Loadout Primary"
        public static void ToggleLoadoutPrimary(Player player)
        {
            if (Menus.mms_classic_spawns)
                return;

                var nextWeapon = WeaponType.NUM;
            if (NetworkMatch.m_force_loadout == 1)
            {
                if (NetworkMatch.m_force_w2 == WeaponType.NUM)
                {
                    nextWeapon = NetworkMatch.m_force_w1;
                }
                else
                {
                    nextWeapon = NetworkMatch.m_force_w1 == player.m_weapon_type ? NetworkMatch.m_force_w2 : NetworkMatch.m_force_w1;
                }
            }
            else
            {
                var currentLoadout = MPLoadouts.Loadouts[Menus.mms_selected_loadout_idx];
                if (currentLoadout.loadoutType == MPLoadouts.LoadoutType.BOMBER)
                {
                    nextWeapon = currentLoadout.weapons[0];
                }
                else
                {
                    nextWeapon = currentLoadout.weapons[0] != player.m_weapon_type
                        ? currentLoadout.weapons[0]
                        : currentLoadout.weapons[1];
                }
            }

            if (nextWeapon == WeaponType.NUM || nextWeapon == player.m_weapon_type)
                return;

            player.Networkm_weapon_type = nextWeapon;
            player.CallCmdSetCurrentWeapon(player.m_weapon_type);
            player.c_player_ship.WeaponSelectFX();
            player.UpdateCurrentWeaponName();
        }

        public static void SendPlayerLoadoutToServer()
        {
            if (Client.GetClient() == null)
            {
                Debug.LogErrorFormat("Null client in MPLoadouts.SendServerPlayerLoadout for player", new object[0]);
                return;
            }

            LoadoutDataMessage loadoutDataMessage = new LoadoutDataMessage();
            loadoutDataMessage.lobby_id = NetworkMatch.m_my_lobby_id;
            loadoutDataMessage.selected_idx = Menus.mms_selected_loadout_idx;
            loadoutDataMessage.loadouts = MPLoadouts.Loadouts.ToList();
            Client.GetClient().Send(MessageTypes.MsgCustomLoadouts, loadoutDataMessage);
        }

        public static void SendCustomLoadoutToServer(int idx)
        {
            if (Client.GetClient() == null)
            {
                Debug.LogErrorFormat("Null client in MPLoadouts.CallCmdToggleLoadout for player", new object[0]);
                return;
            }

            Menus.mms_selected_loadout_idx = idx;

            MPLoadouts.SetCustomLoadoutMessage clm = new MPLoadouts.SetCustomLoadoutMessage();
            clm.lobby_id = NetworkMatch.m_my_lobby_id;
            clm.selected_idx = idx;
            Client.GetClient().Send(MessageTypes.MsgSetCustomLoadout, clm);
        }

        public static bool MaybeIncomplete()
        {
            int allowedWeapons = 0;
            int allowedMissiles = 0;
            for (WeaponType w = (WeaponType)0; w < WeaponType.NUM;  w++) {
                if (IsAllowed(w)) {
                    allowedWeapons++;
                }
            }
            for (MissileType m = (MissileType)0; m < MissileType.NUM;  m++) {
                if (IsAllowed(m)) {
                    allowedMissiles++;
                }
            }
            return ((allowedWeapons < 2) || (allowedMissiles < 2));
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "InitBeforeEachMatch")]
    internal class MPLoadouts_NetworkMatch_InitBeforeEachMatch
    {
        static void Postfix()
        {
            MPLoadouts.NetworkLoadouts.Clear();
        }
    }

    [HarmonyPatch(typeof(Client), "SendPlayerLoadoutToServer")]
    internal class MPLoadouts_Client_SendPlayerLoadoutToServer
    {
        static void Postfix()
        {
            MPLoadouts.SendPlayerLoadoutToServer();
        }
    }

    // Make sure to only send the custom loadout data to clients supporting 'customloadouts' tweak to not kill older clients
    [HarmonyPatch(typeof(Server), "SendLoadoutDataToClients")]
    internal class MPLoadouts_Server_SendLoadoutDataToClients
    {
        static void Postfix()
        {
            bool incompleteLoadouts = MPLoadouts.MaybeIncomplete();
            foreach (var player in Overload.NetworkManager.m_Players.Where(x => x.connectionToClient.connectionId > 0))
            {
                // disconnect legacy clients with a message if they do not support the filtered loadouts
                if (incompleteLoadouts && !MPTweaks.ClientHasTweak(player.connectionToClient.connectionId, "incompleteloadouts")) {
                    NetworkServer.SendToClient(player.connectionToClient.connectionId, CustomMsgType.UnsupportedMatch, new StringMessage("This match enforces INCOMPLETE LOADOUTS, which is not supported for legacy clients"));
                }
                if (MPTweaks.ClientHasTweak(player.connectionToClient.connectionId, "customloadouts"))
                {
                    foreach (var kvp in MPLoadouts.NetworkLoadouts)
                    {
                        NetworkServer.SendToClient(player.connectionToClient.connectionId, MessageTypes.MsgCustomLoadouts, kvp.Value);
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(Server), "RegisterHandlers")]
    internal class MPLoadouts_Server_RegisterHandlers
    {
        static void Postfix()
        {
            NetworkServer.RegisterHandler(MessageTypes.MsgCustomLoadouts, OnCustomLoadoutDataMessage);
            NetworkServer.RegisterHandler(MessageTypes.MsgSetCustomLoadout, OnSetCustomLoadoutMessage);
        }

        private static void OnCustomLoadoutDataMessage(NetworkMessage rawMsg)
        {
            var msg = rawMsg.ReadMessage<MPLoadouts.LoadoutDataMessage>();
            if (!MPLoadouts.NetworkLoadouts.ContainsKey(msg.lobby_id))
            {
                MPLoadouts.NetworkLoadouts.Add(msg.lobby_id, msg);
            }
            else
            {
                MPLoadouts.NetworkLoadouts[msg.lobby_id] = msg;
            }

            // Add free Reflex sidearm
            if (MPLoadouts.IsAllowedByFilter(WeaponType.REFLEX, MPLoadouts.LoadoutFilterBitmask)) {
                MPLoadouts.NetworkLoadouts[msg.lobby_id].loadouts
                    .Where(x => !x.weapons.Contains(WeaponType.REFLEX))
                    .ToList()
                    .ForEach(x => x.weapons.Add(WeaponType.REFLEX));
            }

            // Filter the loadout according to the allowed weapons in the match setting
            foreach(var loadout in  MPLoadouts.NetworkLoadouts[msg.lobby_id].loadouts) {
                loadout.ApplyFilter();
            }
        }

        private static void OnSetCustomLoadoutMessage(NetworkMessage rawMsg)
        {
            var msg = rawMsg.ReadMessage<MPLoadouts.SetCustomLoadoutMessage>();
            if (MPLoadouts.NetworkLoadouts.ContainsKey(msg.lobby_id))
            {
                MPLoadouts.NetworkLoadouts[msg.lobby_id].selected_idx = msg.selected_idx;

                foreach (var player in Overload.NetworkManager.m_Players.Where(x => x.connectionToClient.connectionId > 0))
                {
                    if (MPTweaks.ClientHasTweak(player.connectionToClient.connectionId, "customloadouts"))
                    {
                            NetworkServer.SendToClient(player.connectionToClient.connectionId, MessageTypes.MsgSetCustomLoadout, msg);
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(Client), "RegisterHandlers")]
    internal class MPLoadouts_Client_RegisterHandlers
    {
        static void Postfix()
        {
            if (Client.GetClient() == null)
                return;

            Client.GetClient().RegisterHandler(MessageTypes.MsgCustomLoadouts, OnCustomLoadoutDataMessage);
            Client.GetClient().RegisterHandler(MessageTypes.MsgSetCustomLoadout, OnSetCustomLoadoutMessage);
        }

        private static void OnCustomLoadoutDataMessage(NetworkMessage rawMsg)
        {
            var msg = rawMsg.ReadMessage<MPLoadouts.LoadoutDataMessage>();
            if (!MPLoadouts.NetworkLoadouts.ContainsKey(msg.lobby_id))
            {
                MPLoadouts.NetworkLoadouts.Add(msg.lobby_id, msg);
            }
            else
            {
                MPLoadouts.NetworkLoadouts[msg.lobby_id] = msg;
            }
        }

        private static void OnSetCustomLoadoutMessage(NetworkMessage rawMsg)
        {
            var msg = rawMsg.ReadMessage<MPLoadouts.SetCustomLoadoutMessage>();
            if (MPLoadouts.NetworkLoadouts.ContainsKey(msg.lobby_id))
                MPLoadouts.NetworkLoadouts[msg.lobby_id].selected_idx = msg.selected_idx;
        }
    }

    // Break out NetworkSpawnPlayer.SetMultiplayerLoadout() call to instead go to new SetMultiplayerLoadoutAndModifiers(),
    // which decides whether to use original NetworkSpawnPlayer.SetMultiplayerLoadout() or new custom loadout path
    [HarmonyPatch(typeof(Client), "OnRespawnMsg")]
    internal class MPLoadouts_Client_OnRespawnMsg
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(NetworkSpawnPlayer), "SetMultiplayerLoadout"))
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_3); // int lobby_id
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPLoadouts_Client_OnRespawnMsg), "SetMultiplayerLoadoutAndModifiers"));
                    continue;
                }
                yield return code;
            }
        }

        static void SetMultiplayerLoadout(Player player, int lobby_id, int loadout_idx)
        {
            for (int i = 0; i < 8; i++)
            {
                player.m_weapon_level[i] = WeaponUnlock.LOCKED;
            }
            for (int j = 0; j < 8; j++)
            {
                player.m_missile_level[j] = WeaponUnlock.LOCKED;
                player.m_missile_ammo[j] = 0;
            }
            for (int k = 0; k < 10; k++)
            {
                player.m_upgrade_level[k] = 0;
            }
            player.m_ammo = 0;
            player.m_weapon_level[0] = WeaponUnlock.LOCKED;
            player.m_missile_level[0] = WeaponUnlock.LOCKED;
            player.m_upgrade_level[3] = 0;

            int num2 = 0;
            if (NetworkMatch.m_force_loadout == 1)
            {
                player.m_weapon_level[(int)NetworkMatch.m_force_w1] = WeaponUnlock.LEVEL_1;
                if (Player.WeaponUsesAmmo2(NetworkMatch.m_force_w1))
                {
                    num2++;
                }
                if (NetworkMatch.m_force_w2 != WeaponType.NUM)
                {
                    player.m_weapon_level[(int)NetworkMatch.m_force_w2] = WeaponUnlock.LEVEL_1;
                    if (Player.WeaponUsesAmmo2(NetworkMatch.m_force_w2))
                    {
                        num2++;
                    }
                }
                if (NetworkMatch.m_force_m1 != MissileType.NUM)
                {
                    player.m_missile_level[(int)NetworkMatch.m_force_m1] = WeaponUnlock.LEVEL_1;
                    player.m_missile_ammo[(int)NetworkMatch.m_force_m1] = Player.MP_DEFAULT_MISSILE_AMMO[(int)NetworkMatch.m_force_m1];
                }
                if (NetworkMatch.m_force_m2 != MissileType.NUM)
                {
                    player.m_missile_level[(int)NetworkMatch.m_force_m2] = WeaponUnlock.LEVEL_1;
                    player.m_missile_ammo[(int)NetworkMatch.m_force_m2] = Player.MP_DEFAULT_MISSILE_AMMO[(int)NetworkMatch.m_force_m2];
                }
                player.Networkm_weapon_type = NetworkMatch.m_force_w1;
                player.Networkm_missile_type = ((NetworkMatch.m_force_m1 != MissileType.NUM) ? NetworkMatch.m_force_m1 : NetworkMatch.m_force_m2);
            }
            else
            {
                var loadout_data = MPLoadouts.NetworkLoadouts[lobby_id];
                var loadout = loadout_data.loadouts[loadout_idx];

                foreach (var weapon in loadout.weapons)
                {
                    if (weapon != WeaponType.NUM) {
                        player.m_weapon_level[(int)weapon] = WeaponUnlock.LEVEL_1;
                        if (Player.WeaponUsesAmmo2(weapon))
                            num2++;
                    }
                }

                foreach (var missile in loadout.missiles)
                {
                    if (missile != MissileType.NUM) {
                        player.m_missile_level[(int)missile] = WeaponUnlock.LEVEL_1;
                        player.m_missile_ammo[(int)missile] = Player.MP_DEFAULT_MISSILE_AMMO[(int)missile];
                    }
                }

                player.Networkm_weapon_type = loadout.weapons[0];
                player.Networkm_missile_type = loadout.missiles[0];
            }
            if (player.isLocalPlayer)
            {
                player.CallCmdSetCurrentWeapon(player.m_weapon_type);
                player.c_player_ship.SwitchVisibleWeapon(false, player.m_weapon_type);
            }
            player.m_ammo += ((num2 <= 1) ? ((num2 <= 0) ? 0 : 200) : 300);
            player.UpdateCurrentWeaponName();
            if (player.isLocalPlayer)
            {
                player.CallCmdSetCurrentMissile(player.m_missile_type);
            }
            player.UpdateCurrentMissileName();
        }

        static void SetMultiplayerModifiers(Player player, LoadoutDataMessage loadout_data, bool use_loadout1)
        {
            int mp_mod = loadout_data.m_mp_modifier1;
            if (NetworkMatch.m_force_modifier1 != 4)
            {
                mp_mod = NetworkMatch.m_force_modifier1;
            }
            player.m_mp_mod1 = mp_mod;
            int num = NetworkMatch.m_turn_speed_limit;
            switch (mp_mod)
            {
                case 0:
                    num++;
                    break;
                case 1:
                    player.m_unlock_boost_speed = true;
                    player.m_unlock_boost_heatsink = true;
                    break;
                case 2:
                    player.m_upgrade_level[3] = 1;
                    break;
                case 3:
                    player.m_upgrade_level[0] = 3;
                    break;
            }
            int mp_mod2 = loadout_data.m_mp_modifier2;
            if (NetworkMatch.m_force_modifier2 != 4)
            {
                mp_mod2 = NetworkMatch.m_force_modifier2;
            }
            player.m_mp_mod2 = mp_mod2;
            switch (mp_mod2)
            {
                case 0:
                    num++;
                    break;
                case 1:
                    player.m_unlock_blast_damage = true;
                    break;
                case 2:
                    player.m_upgrade_level[2] = 1;
                    player.m_ammo += 100;
                    player.m_upgrade_level[1] = 3;
                    break;
                case 3:
                    player.m_unlock_fast_forward = true;
                    break;
            }
            player.c_player_ship.m_turn_speed_mp = num;
            return;
        }

        static void SetMultiplayerLoadoutAndModifiers(Player player, LoadoutDataMessage loadout_data, bool use_loadout1, int lobby_id)
        {
            if (MPLoadouts.NetworkLoadouts.ContainsKey(lobby_id))
            {
                var loadout_idx = player.isLocalPlayer ? Menus.mms_selected_loadout_idx : MPLoadouts.NetworkLoadouts[lobby_id].selected_idx;
                SetMultiplayerLoadout(player, lobby_id, loadout_idx);
                SetMultiplayerModifiers(player, loadout_data, use_loadout1);
            }
            else
            {
                Debug.Log($"Didn't find custom loadout data for {player.m_mp_name}, {lobby_id}, using stock LoadoutDataMessage.  (Old client?)");
                NetworkSpawnPlayer.SetMultiplayerLoadout(player, loadout_data, use_loadout1);
            }
        }
    }

    // Pregame overlay position needs adjusted for 4x loadouts
    [HarmonyPatch(typeof(UIElement), "DrawMpPreGameOverlay")]
    internal class MPLoadouts_UIElement_DrawMpPreGameOverlay
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 150f)
                    code.operand = 250f;

                if (code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 185f)
                    code.operand = 165f;

                yield return code;
            }
        }
    }

    // This handles the in-match simple overlay selection
    [HarmonyPatch(typeof(UIElement), "DrawMpOverlayLoadout")]
    internal class MPLoadouts_UIElement_DrawMpOverlayLoadout
    {
        static void DrawMpLoadoutSimple(UIElement uie, Vector2 pos, int idx, bool active)
        {
            // This check needed as there can be brief moments where this data isn't populated client-side yet
            if (!MPLoadouts.NetworkLoadouts.ContainsKey(NetworkMatch.m_my_lobby_id))
                return;

            var player = GameManager.m_local_player;
            MPLoadouts.CustomLoadout loadout = MPLoadouts.NetworkLoadouts[NetworkMatch.m_my_lobby_id].loadouts[idx];
            float num = 535f;
            float middle_h = 2f;
            Color c = (!active) ? UIManager.m_col_ub0 : UIManager.m_col_ui5;
            c.a = uie.m_alpha;
            UIManager.DrawFrameEmptyCenter(pos + Vector2.up * 11f, 17f, 17f, num, middle_h, c, 7);
            num *= 0.345f;
            pos.x -= num;
            pos.y += 11f;

            uie.DrawStringSmall((idx + 1).ToString(), pos - Vector2.right * 90f, 0.6f, StringOffset.LEFT, c, uie.m_alpha);

            pos.x += 30f;
            if (loadout.loadoutType == MPLoadouts.LoadoutType.GUNNER)
            {
                DrawMpWeaponSimple(uie, pos, loadout.weapons[0], active);
                pos.x += num - 12f;
                DrawMpWeaponSimple(uie, pos, loadout.weapons[1], active);
                pos.x += num - 12f;
                DrawMpMissileSimple(uie, pos, loadout.missiles[0], active);
            }
            else
            {
                DrawMpWeaponSimple(uie, pos, loadout.weapons[0], active);
                pos.x += num - 12f;
                DrawMpMissileSimple(uie, pos, loadout.missiles[0], active);
                pos.x += num - 12f;
                DrawMpMissileSimple(uie, pos, loadout.missiles[1], active);
            }
        }

        private static void DrawMpWeaponSimple(UIElement uie, Vector2 pos, WeaponType wt, bool highlight)
        {
            float num = 140f;
            Color color = (!highlight) ? UIManager.m_col_ub1 : UIManager.m_col_ui2;
            color.a = uie.m_alpha;
            UIManager.DrawQuadBarHorizontal(pos, 11f, 11f, num, color, 8);
            color = ((!highlight) ? UIManager.m_col_ui1 : UIManager.m_col_ui5);
            if (wt < WeaponType.NUM) {
                UIManager.DrawSpriteUI(pos - Vector2.right * (num * 0.5f + 2f), 0.16f, 0.16f, color, uie.m_alpha, (int)(26 + wt));
            }
            uie.DrawStringSmall(Player.GetWeaponNameNoDefault(wt), pos - Vector2.right * (num * 0.5f - 10f), 0.4f, StringOffset.LEFT, color, 1f, num * 0.95f);
        }

        private static void DrawMpMissileSimple(UIElement uie, Vector2 pos, MissileType mt, bool highlight)
        {
            float num = 140f;
            Color color = (!highlight) ? UIManager.m_col_ub1 : UIManager.m_col_ui2;
            color.a = uie.m_alpha;
            UIManager.DrawQuadBarHorizontal(pos, 11f, 11f, num, color, 8);
            color = ((!highlight) ? UIManager.m_col_ui1 : UIManager.m_col_ui5);
            if (mt < MissileType.NUM) {
                UIManager.DrawSpriteUI(pos - Vector2.right * (num * 0.5f + 2f), 0.16f, 0.16f, color, uie.m_alpha, (int)(104 + mt));
            }
            uie.DrawStringSmall(Player.GetMissileNameNoDefault(mt), pos - Vector2.right * (num * 0.5f - 10f), 0.4f, StringOffset.LEFT, color, 1f, num * 0.95f);
        }

        static bool Prefix(UIElement __instance, Vector2 pos, bool dead, bool show_loadout, bool show_respawn_timer, bool showing_auto_timer)
        {
            if (show_loadout)
            {
                //pos.y -= 80f;
                pos.x = -300f;
                DrawMpLoadoutSimple(__instance, pos, 0, Menus.mms_selected_loadout_idx == 0);
                pos.x = 300f;
                DrawMpLoadoutSimple(__instance, pos, 1, Menus.mms_selected_loadout_idx == 1);
                pos.y += 45f;
                pos.x = -300f;
                DrawMpLoadoutSimple(__instance, pos, 2, Menus.mms_selected_loadout_idx == 2);
                pos.x = 300f;
                DrawMpLoadoutSimple(__instance, pos, 3, Menus.mms_selected_loadout_idx == 3);
                pos.y += 60f;
                pos.x = 0f;
                float alpha_mod = (GameManager.m_local_player.c_player_ship.m_dying_timer >= 2.5f) ? 0.25f : 1f;
                __instance.DrawStringSmall(ScriptTutorialMessage.ControlString(CCInput.FIRE_MISSILE) + " - " + Loc.LS("TOGGLE LOADOUT"), pos, 0.4f, StringOffset.CENTER, UIManager.m_col_ui2, alpha_mod, -1f);
            }
            else
            {
                pos.y += 60f;
            }
            if (dead)
            {
                pos.y += 20f;
                if (UIElement.ReadyToRespawn)
                {
                    string text = Loc.LS("TOGGLE AUTO-RESPAWN (STATUS: READY!)");
                    __instance.DrawStringSmall(ScriptTutorialMessage.ControlString(CCInput.FIRE_FLARE) + " - " + text, pos, 0.4f, StringOffset.CENTER, UIManager.m_col_hi5, 1f, -1f);
                    if (showing_auto_timer && !GameplayManager.ShowMpScoreboard)
                    {
                        pos.y = -45f;
                        __instance.DrawStringSmall(Loc.LS("AUTO-RESPAWN TIMER"), pos, 0.4f, StringOffset.CENTER, UIManager.m_col_ui3, 1f, 250f);
                        __instance.DrawWideBox(pos, 120f, 12f, UIManager.m_col_ui3, __instance.m_alpha, 7);
                    }
                }
                else if (show_respawn_timer)
                {
                    string text = ScriptTutorialMessage.ControlString(CCInput.FIRE_FLARE) + " - " + Loc.LS("RESPAWN NOW");
                    __instance.DrawStringSmall(text, pos, 0.4f, StringOffset.CENTER, UIManager.m_col_hi5, 1f, -1f);
                    float stringWidth = UIManager.GetStringWidth(text, 8f, 0, -1);
                    float a = __instance.m_alpha * __instance.m_anim_state2;
                    __instance.DrawWideBox(pos, stringWidth * 0.5f, 10f, UIManager.m_col_hi5, a, 7);
                    if (!GameplayManager.ShowMpScoreboard)
                    {
                        pos.y = -45f;
                        __instance.DrawStringSmall(Loc.LS("RESPAWN TIMER"), pos, 0.4f, StringOffset.CENTER, UIManager.m_col_hi3, 1f, 250f);
                        __instance.DrawWideBox(pos, 120f, 12f, UIManager.m_col_hi3, __instance.m_alpha, 7);
                    }
                }
                else
                {
                    string text = Loc.LS("TOGGLE AUTO-RESPAWN (STATUS: DISABLED)");
                    __instance.DrawStringSmall(ScriptTutorialMessage.ControlString(CCInput.FIRE_FLARE) + " - " + text, pos, 0.4f, StringOffset.CENTER, UIManager.m_col_hi5, 1f, -1f);
                    if (showing_auto_timer && !GameplayManager.ShowMpScoreboard)
                    {
                        pos.y = -45f;
                        __instance.DrawStringSmall(Loc.LS("AUTO-RESPAWN TIMER"), pos, 0.4f, StringOffset.CENTER, UIManager.m_col_ui3, 0.2f, 250f);
                        __instance.DrawWideBox(pos, 120f, 12f, UIManager.m_col_ui3, __instance.m_alpha * 0.2f, 7);
                    }
                }
            }

            return false;
        }
    }

    // Entry point for client changing loadout selection in active match (pregame or after death)
    [HarmonyPatch(typeof(Player), "CallCmdToggleLoadout")]
    internal class MPLoadouts_Player_CallCmdToggleLoadout
    {
        static void Postfix(Player __instance)
        {
            MPLoadouts.SendCustomLoadoutToServer((Menus.mms_selected_loadout_idx + 1) % 4);
        }
    }

    // process the loadout selection -- uses hotkeys for selecting primary weapon slots 1-4
    [HarmonyPatch(typeof(PlayerShip), "Update")]
    internal class MPLoadouts_PlayerShip_Update
    {
        public static void LoadoutSelect(PlayerShip ps)
        {
            if (!PlayerShip.m_typing_in_chat && NetworkMatch.m_force_loadout == 0 && (float)ps.m_dying_timer < 2.5f)
            {
                switch (Menus.mms_loadout_hotkeys)
                {
                    // weapon selection using Primary 1/2, 3/4, 5/6, 7/8
                    case 1:
                        if (Controls.JustPressed(CCInput.WEAPON_1x2))
                        {
                            MPLoadouts.SendCustomLoadoutToServer(0);
                            MenuManager.PlayCycleSound();
                        }
                        else if (Controls.JustPressed(CCInput.WEAPON_3x4))
                        {
                            MPLoadouts.SendCustomLoadoutToServer(1);
                            MenuManager.PlayCycleSound();
                        }
                        else if (Controls.JustPressed(CCInput.WEAPON_5x6))
                        {
                            MPLoadouts.SendCustomLoadoutToServer(2);
                            MenuManager.PlayCycleSound();
                        }
                        else if (Controls.JustPressed(CCInput.WEAPON_7x8))
                        {
                            MPLoadouts.SendCustomLoadoutToServer(3);
                            MenuManager.PlayCycleSound();
                        }
                        break;

                    // weapon selection using Primary 1, 2, 3, 4
                    case 2:
                        if (Controls.JustPressed(CCInput.WEAPON_1x2))
                        {
                            if (Controls.BothAssigned(CCInput.WEAPON_1x2))
                            {
                                if (Controls.PressedSlot(CCInput.WEAPON_1x2, 0))
                                {
                                    MPLoadouts.SendCustomLoadoutToServer(0);
                                }
                                else
                                {
                                    MPLoadouts.SendCustomLoadoutToServer(1);
                                }
                                MenuManager.PlayCycleSound();
                            }
                        }
                        else if (Controls.JustPressed(CCInput.WEAPON_3x4))
                        {
                            if (Controls.BothAssigned(CCInput.WEAPON_3x4))
                            {
                                if (Controls.PressedSlot(CCInput.WEAPON_3x4, 0))
                                {
                                    MPLoadouts.SendCustomLoadoutToServer(2);
                                }
                                else
                                {
                                    MPLoadouts.SendCustomLoadoutToServer(3);
                                }
                                MenuManager.PlayCycleSound();
                            }
                        }
                        break;

                    // weapon selection using number & numpad keys 1-4 on the keyboard only
                    case 3:
                        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                        {
                            MPLoadouts.SendCustomLoadoutToServer(0);
                            MenuManager.PlayCycleSound();
                        }
                        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                        {
                            MPLoadouts.SendCustomLoadoutToServer(1);
                            MenuManager.PlayCycleSound();
                        }
                        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
                        {
                            MPLoadouts.SendCustomLoadoutToServer(2);
                            MenuManager.PlayCycleSound();
                        }
                        else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
                        {
                            MPLoadouts.SendCustomLoadoutToServer(3);
                            MenuManager.PlayCycleSound();
                        }
                        break;

                    // disabled
                    default:
                        break;
                }
            }
        }

        // catches the selection hotkeys while respawning
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                yield return code;

                if (code.opcode == OpCodes.Stsfld && code.operand == AccessTools.Field(typeof(GameplayManager), "ShowMpScoreboard"))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPLoadouts_PlayerShip_Update), "LoadoutSelect"));
                }
            }
        }
    }

    // catches the selection hotkeys at round start
    [HarmonyPatch(typeof(PlayerShip), "UpdateReadImmediateControls")]
    internal class MPLoadouts_PlayerShip_UpdateReadImmediateControls
    {
        public static void Prefix(PlayerShip __instance)
        {
            if (GameplayManager.IsMultiplayer && __instance.c_player.m_pregame)
            {
                MPLoadouts_PlayerShip_Update.LoadoutSelect(__instance);
            }
        }
    }

    // Take care about the old-style loadout messages, especially apply the filters there, too
    [HarmonyPatch(typeof(NetworkMatch), "UpdatePlayerLoadout")]
    class MPLoadouts_NetworkMatch_UpdatePlayerLoadout {
        static void Prefix(int lobby_id, ref Overload.LoadoutDataMessage ldm) {
            MPLoadouts.CustomLoadout l1 = new MPLoadouts.CustomLoadout();
            MPLoadouts.CustomLoadout l2 = new MPLoadouts.CustomLoadout();

            l1.ImportLegacyLoadout(ldm, ldm.m_mp_loadout1);
            l2.ImportLegacyLoadout(ldm, ldm.m_mp_loadout2);

            ldm.m_mp_custom1_w1 = WeaponType.NUM;
            ldm.m_mp_custom2_w1 = WeaponType.NUM;
            ldm.m_mp_custom2_w2 = WeaponType.NUM;

            ldm.m_mp_custom1_m1 = MissileType.NUM;
            ldm.m_mp_custom1_m2 = MissileType.NUM;
            ldm.m_mp_custom2_m1 = MissileType.NUM;

            l1.ApplyFilter();
            l2.ApplyFilter();

            ldm.m_mp_loadout1 = l1.ExportToLegacyLoadout(ldm);
            ldm.m_mp_loadout2 = l2.ExportToLegacyLoadout(ldm);
        }
    }


    // Prevent SwitchVisibleWeapon if no primary is allowed at all
    [HarmonyPatch(typeof(PlayerShip), "SwitchVisibleWeapon")]
    class MPLoadouts_PlayerShip_SwitchVisibleWeapon {
        [HarmonyPriority(Priority.First)]
        static bool Prefix(PlayerShip __instance, bool force_visible = false, WeaponType wt = WeaponType.NUM) {
            if (wt == WeaponType.NUM && __instance.c_player.m_weapon_type == WeaponType.NUM) {
                return false;
            }
            return true;
        }
    }

    // Prevent DrawHUDPrimaryWeapon if no primary is allowed at all
    [HarmonyPatch(typeof(UIElement), "DrawHUDPrimaryWeapon")]
    class MPLoadouts_UIElement_DrawHUDPrimaryWeapon {
        [HarmonyPriority(Priority.First)]
        static bool Prefix() {
            if (GameManager.m_local_player.m_weapon_type == WeaponType.NUM) {
                return false;
            }
            return true;
        }
    }

    // Fix crash in SpewItemsOnDeath if no primary is available at all
    [HarmonyPatch(typeof(PlayerShip), "SpewItemsOnDeath")]
    class MPLoadouts_PlayerShip_SpewItemsOnDeath {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;
            if ((int)WeaponType.NUM  != 8) {// just to make sure...
                throw new System.Exception("MPLoadouts_PlayerShip_SpewItemsOnDeath expects WeaponType.NUM == 8");
            }
            foreach (var code in codes)
            {
                if (state == 0) {
                    if (code.opcode == OpCodes.Ldfld && ((FieldInfo)code.operand).Name == "m_weapon_type") {
                        state = 1;
                    }
                } else if (state == 1) {
                    if (code.opcode == OpCodes.Ldelem_U1)
                    {
                        yield return new CodeInstruction(OpCodes.Ldc_I4_7);
                        yield return new CodeInstruction(OpCodes.And);
                        state = 2;
                    }
                }
                yield return code;
            }
        }
    }

    // if loadout forbids all primaries, make sure to switch to the first weapon which is picked up
    [HarmonyPatch(typeof(Player), "UnlockWeaponClient")]
    class MPLoadouts_Player_UnlockWeaponClient
    {
        public static void Postfix(WeaponType wt, bool silent, Player __instance)
        {
            if (__instance.m_weapon_type >= WeaponType.NUM) {
                __instance.Networkm_weapon_type = wt;
                __instance.CallCmdSetCurrentWeapon(__instance.m_weapon_type);
                __instance.c_player_ship.WeaponSelectFX();
                __instance.UpdateCurrentWeaponName();
            }
        }
    }

    // Draw Selection Menu 
    [HarmonyPatch(typeof(UIElement), "DrawMpMatchSetup")]
    class MPLoadouts_UIElement_DrawMpMatchSetup {

        [HarmonyPriority(Priority.Normal + 1)]
        private static void Postfix(UIElement __instance)
        {
            
            switch (MenuManager.m_menu_micro_state)
            {
                case 15:
                    Vector2 position = Vector2.up * (UIManager.UI_TOP + 70f);
                    position.y += 62f;
                    __instance.DrawLabelSmall(Vector2.up * (UIManager.UI_TOP + 70f), Loc.LS("ALLOWED LOADOUT WEAPONS"), 250f, 24f, 1f);
                    position.y = -133f;
                    __instance.DrawMenuSeparator(position - Vector2.up * 40f);
                    position.y += 40f;
                    __instance.DrawSmallHeader1(position - Vector2.up * 51f, Loc.LS("PRIMARY WEAPONS"), 300f);
                    for (int i = 0; i < 4; i++)
                    {
                        string weaponNameNoDefault = Player.GetWeaponNameNoDefault((WeaponType)i);
                        int num = i;
                        __instance.SelectAndDrawCheckboxItem(weaponNameNoDefault, position + Vector2.right * (((float)i - 1.5f) * 320f), num, MPLoadouts.IsAllowed((WeaponType)num), false, 0.5f, -1);
                    }
                    position.y += 62f;
                    for (int j = 0; j < 4; j++)
                    {
                        string weaponNameNoDefault2 = Player.GetWeaponNameNoDefault(j + WeaponType.DRILLER);
                        int num2 = j + 4;
                        __instance.SelectAndDrawCheckboxItem(weaponNameNoDefault2, position + Vector2.right * (((float)j - 1.5f) * 320f), num2, MPLoadouts.IsAllowed((WeaponType)num2), false, 0.5f, -1);
                    }
                    position.y += 62f;
                    position.y += 40f;
                    __instance.DrawSmallHeader1(position - Vector2.up * 51f, Loc.LS("SECONDARY WEAPONS"), 300f);
                    for (int k = 0; k < 4; k++)
                    {
                        string missileNameNoDefault = Player.GetMissileNameNoDefault((MissileType)k);
                        int num3 = k + 8;
                        __instance.SelectAndDrawCheckboxItem(missileNameNoDefault, position + Vector2.right * (((float)k - 1.5f) * 320f), num3, MPLoadouts.IsAllowed((MissileType)k), false, 0.5f, -1);
                    }
                    position.y += 62f;
                    for (int l = 0; l < 4; l++)
                    {
                        string missileNameNoDefault2 = Player.GetMissileNameNoDefault(l + MissileType.NOVA);
                        int num4 = l + 12;
                        __instance.SelectAndDrawCheckboxItem(missileNameNoDefault2, position + Vector2.right * (((float)l - 1.5f) * 320f), num4, MPLoadouts.IsAllowed((MissileType)(l + MissileType.NOVA)), false, 0.5f, -1);
                    }
                    __instance.DrawMenuSeparator(position + Vector2.up * 40f);
                    position.y += 80.6f;
                    __instance.SelectAndDrawHalfItem2(Loc.LS("ALLOWED POWERUPS"), position - Vector2.right * 470f, 22, false);
                    __instance.SelectAndDrawHalfItem2(Loc.LS("CLEAR"), position - Vector2.right * 160f, 20, false);
                    __instance.SelectAndDrawHalfItem2(Loc.LS("RESET"), position + Vector2.right * 160f, 21, false);
                    break;
                default:
                    break;
            }
        }
    }

    // Handle Logic for Selection Menu
    [HarmonyPatch(typeof(MenuManager), "MpMatchSetup")]
    class MPLoadouts_MenuManager_MpMatchSetup
    {
        [HarmonyPriority(Priority.Normal + 1)]
        static void Postfix()
        {
            //uConsole.Log("Fire: " + MenuManager.m_menu_micro_state + " : " + UIManager.m_menu_selection);

            // Disables reflex powerups since it's provided as a standard sidearm now.
            // But if Reflex is forbidden in the loadout settings, you may add it as powerup
            if (!Menus.mms_classic_spawns && MPLoadouts.IsAllowedByFilter(WeaponType.REFLEX, MPLoadouts.LoadoutFilterBitmask))
            {
                MenuManager.mms_powerup_filter[2] = false;
            }

            if (!UIManager.PushedSelect(100) && (!MenuManager.option_dir || !UIManager.PushedDir()))
                return;

            switch (MenuManager.m_menu_micro_state)
            {
                case 6:
                    switch (UIManager.m_menu_selection)
                    {
                        // Button to lead into the "Allowed Loadouts Weapon" Menu
                        case 23:
                            MenuManager.m_menu_micro_state = 15;
                            MenuManager.UIPulse(2f);
                            MenuManager.PlaySelectSound(1f);
                            return;
                        case 100:
                            MenuManager.m_menu_micro_state = 6;
                            MenuManager.UIPulse(2f);
                            MenuManager.PlaySelectSound(1f);
                            return;
                        default:
                            return;
                    }
                // Handling for the Backlink button in the allowed powerups menu
                case 7:
                    switch (UIManager.m_menu_selection)
                    {
                        case 30:
                            MenuManager.m_menu_micro_state = 15;
                            MenuManager.UIPulse(2f);
                            MenuManager.PlaySelectSound(1f);
                            return;
                        default:
                            return;
                    }
                // The "Allowed Loadout Weapons" Menu
                case 15:
                    switch (UIManager.m_menu_selection)
                    {
                        // Primary Weapons
                        case 0:
                        case 1:
                        case 2:
                        case 3:
                        case 4:
                        case 5:
                        case 6:
                        case 7:
                            MPLoadouts.SetAllowed((WeaponType)UIManager.m_menu_selection, !MPLoadouts.IsAllowed((WeaponType)UIManager.m_menu_selection));
                            MenuManager.PlaySelectSound(1f);
                            return;
                        // Secondary Weapons
                        case 8:
                        case 9:
                        case 10:
                        case 11:
                        case 12:
                        case 13:
                        case 14:
                        case 15:
                            MPLoadouts.SetAllowed((MissileType)UIManager.m_menu_selection - 8, !MPLoadouts.IsAllowed((MissileType)UIManager.m_menu_selection - 8));
                            MenuManager.PlaySelectSound(1f);
                            return;
                        // Clear All Button
                        case 20:
                            for (int i = 0; i < 8; i++)
                            {
                                MPLoadouts.SetAllowed((WeaponType)i, false);
                                MPLoadouts.SetAllowed((MissileType)i, false);
                            }
                            MenuManager.PlaySelectSound(1f);
                            return;
                        // Set Default State Button
                        case 21:
                            MPLoadouts.LoadoutFilterBitmask = MPLoadouts.MASK_DEFAULT;
                            MenuManager.PlaySelectSound(1f);
                            return;
                        // Switch to Powerup menu
                        case 22:
                            MenuManager.m_menu_micro_state = 7;
                            MenuManager.UIPulse(2f);
                            MenuManager.PlaySelectSound(1f);
                            return;
                        // Back to parent menu Button
                        case 100:
                            MenuManager.m_menu_micro_state = 6;
                            MenuManager.UIPulse(2f);
                            MenuManager.PlaySelectSound(1f);
                            return;
                        default:
                            return;
                    }
            }
        }
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int state = 0;
            int fail = 0;
            Label jumpTarget = new Label();
            List<CodeInstruction> instructionsSaved = new List<CodeInstruction>();

            foreach (var code in instructions) {
                if (state == 0) {
                    if (code.opcode == OpCodes.Ldsfld && (code.operand as FieldInfo).Name == "mms_powerup_filter") {
                        state++;
                    }
                } else if (state == 1) {
                    if (code.opcode == OpCodes.Ldsfld && (code.operand as FieldInfo).Name == "m_menu_selection") {
                        state++;
                    } else {
                        fail = state;
                    }
                } else if (state == 2) {
                    if (code.opcode == OpCodes.Ldsfld && (code.operand as FieldInfo).Name == "mms_powerup_filter") {
                        state++;
                    } else {
                        fail = state;
                    }
                } else if (state == 3) {
                    if (code.opcode == OpCodes.Ldsfld && (code.operand as FieldInfo).Name == "m_menu_selection") {
                        state++;
                    } else {
                        fail = state;
                    }
                } else if (state == 4) {
                    if (code.opcode == OpCodes.Br) {
                        jumpTarget = (Label)code.operand;
                        // add additional branch
                        yield return new CodeInstruction(OpCodes.Ldlen);
                        yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(UIManager), "m_menu_selection"));
                        yield return new CodeInstruction(OpCodes.Ble, jumpTarget);
                        yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MenuManager), "mms_powerup_filter"));

                        // emit the saved instructions now
                        foreach (var c in instructionsSaved) {
                            yield return c;
                        }
                        state++;
                    }
                }
                if (state > 1 && state < 5) {
                    instructionsSaved.Add(code);
                } else {
                    yield return code;
                }
            }
            if (state != 5 || fail > 0) {
            } else {
                Debug.LogFormat("MPLoadouts_MenuManager_MpMatchSetup: transpiler failed at state {0} {1}", state, fail);
            }
        }
    }
}
