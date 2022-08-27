using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod
{
    public class MPLoadouts
    {
        public static Dictionary<int, LoadoutDataMessage> NetworkLoadouts = new Dictionary<int, LoadoutDataMessage>();
        public static int loadoutSelection1 = 0;
        public static int loadoutSelection2 = 1;

        public static CustomLoadout[] Loadouts = new CustomLoadout[4]
        {
            new BomberLoadout(WeaponType.IMPULSE, MissileType.FALCON, MissileType.CREEPER),
            new GunnerLoadout(WeaponType.DRILLER, WeaponType.CYCLONE, MissileType.HUNTER),
            new BomberLoadout(WeaponType.THUNDERBOLT, MissileType.FALCON, MissileType.CREEPER),
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
            public List<CustomLoadout> loadouts;
        }

        public static void MpCycleWeapon(int loadoutIndex, int weaponIndex)
        {
            MPLoadouts.Loadouts[loadoutIndex].weapons[weaponIndex] = (WeaponType)((((int)MPLoadouts.Loadouts[loadoutIndex].weapons[weaponIndex]) + 1) % (int)WeaponType.LANCER);

            if (MPLoadouts.Loadouts[loadoutIndex].weapons.Count(x => x == MPLoadouts.Loadouts[loadoutIndex].weapons[weaponIndex]) > 1)
                MPLoadouts.Loadouts[loadoutIndex].weapons[weaponIndex] = (WeaponType)((((int)MPLoadouts.Loadouts[loadoutIndex].weapons[weaponIndex]) + 1) % (int)WeaponType.LANCER);
        }

        public static void MpCycleMissile(int loadoutIndex, int missileIndex)
        {
            MPLoadouts.Loadouts[loadoutIndex].missiles[missileIndex] = (MissileType)((((int)MPLoadouts.Loadouts[loadoutIndex].missiles[missileIndex]) + 1) % (int)MissileType.NOVA);

            if (MPLoadouts.Loadouts[loadoutIndex].missiles.Count(x => x == MPLoadouts.Loadouts[loadoutIndex].missiles[missileIndex]) > 1)
                MPLoadouts.Loadouts[loadoutIndex].missiles[missileIndex] = (MissileType)((((int)MPLoadouts.Loadouts[loadoutIndex].missiles[missileIndex]) + 1) % (int)MissileType.NOVA);
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
            loadoutDataMessage.loadouts = new List<CustomLoadout> { MPLoadouts.Loadouts[loadoutSelection1], MPLoadouts.Loadouts[loadoutSelection2] };
            Client.GetClient().Send(MessageTypes.MsgCustomLoadouts, loadoutDataMessage);
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

    [HarmonyPatch(typeof(Server), "SendLoadoutDataToClients")]
    internal class MPLoadouts_Server_SendLoadoutDataToClients
    {
        static void Postfix()
        {
            foreach (var player in Overload.NetworkManager.m_Players.Where(x => x.connectionToClient.connectionId > 0))
            {
                if (MPTweaks.ClientHasTweak(player.connectionToClient.connectionId, "customloadouts"))
                {
                    foreach (var kvp in MPLoadouts.NetworkLoadouts)
                    {
                        NetworkServer.SendToClient(player.connectionToClient.connectionId, MessageTypes.MsgCustomLoadouts, kvp.Value);
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
        }

        [HarmonyPatch(typeof(Client), "RegisterHandlers")]
        internal class MPLoadouts_Client_RegisterHandlers
        {
            static void Postfix()
            {
                if (Client.GetClient() == null)
                    return;

                Client.GetClient().RegisterHandler(MessageTypes.MsgCustomLoadouts, OnCustomLoadoutDataMessage);
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
        }

        [HarmonyPatch(typeof(Client), "OnRespawnMsg")]
        internal class MPLoadouts_Client_OnRespawnMsg
        {
            static void SetMultiplayerLoadout(Player player, int lobby_id, bool use_loadout1)
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
                    var loadout = (!use_loadout1) ? loadout_data.loadouts[1] : loadout_data.loadouts[0];

                    foreach (var weapon in loadout.weapons)
                    {
                        player.m_weapon_level[(int)weapon] = WeaponUnlock.LEVEL_1;
                        if (Player.WeaponUsesAmmo2(weapon))
                            num2++;
                    }

                    foreach (var missile in loadout.missiles)
                    {
                        player.m_missile_level[(int)missile] = WeaponUnlock.LEVEL_1;
                        player.m_missile_ammo[(int)missile] = Player.MP_DEFAULT_MISSILE_AMMO[(int)missile];
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

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
            {
                foreach (var code in codes)
                {
                    if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(NetworkSpawnPlayer), "SetMultiplayerLoadout"))
                    {
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPLoadouts_Client_OnRespawnMsg), "SetMultiplayerModifiers"));
                        yield return new CodeInstruction(OpCodes.Ldloc_1); // Player 
                        yield return new CodeInstruction(OpCodes.Ldloc_3); // int lobby_id
                        yield return new CodeInstruction(OpCodes.Ldloc_0); // RespawnMessage
                        yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(RespawnMessage), "use_loadout1"));
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPLoadouts_Client_OnRespawnMsg), "SetMultiplayerLoadout"));
                        continue;
                    }
                    yield return code;
                }
            }
        }

        [HarmonyPatch(typeof(UIElement), "DrawMpOverlayLoadout")]
        internal class MPLoadouts_UIElement_DrawMpOverlayLoadout
        {
            static void FakeDrawMpLoadoutSimple(UIElement uie, Vector2 pos, int idx, Player player, bool active)
            {
                return;
            }

            static void DrawMpLoadoutSimple(UIElement uie, Vector2 pos, int idx, bool active)
            {
                var player = GameManager.m_local_player;
                MPLoadouts.CustomLoadout loadout = new MPLoadouts.CustomLoadout();
                try
                {
                    loadout = MPLoadouts.NetworkLoadouts[NetworkMatch.m_my_lobby_id].loadouts[idx];
                }
                catch (System.Exception ex)
                {
                    Debug.Log($"Unable to find {NetworkMatch.m_my_lobby_id}, {idx}");
                    return;
                }
                float num = 535f;
                float middle_h = 35f;
                Color c = (!active) ? UIManager.m_col_ub0 : UIManager.m_col_ui5;
                c.a = uie.m_alpha;
                UIManager.DrawFrameEmptyCenter(pos + Vector2.up * 11f, 17f, 17f, num, middle_h, c, 7);
                uie.DrawWideBox(pos, 265f, 15f, (!active) ? UIManager.m_col_ub0 : UIManager.m_col_ui5, uie.m_alpha, 8);
                uie.DrawStringSmall(loadout.loadoutType.ToString(), pos, 0.75f, StringOffset.CENTER, (!active) ? UIManager.m_col_ui1 : UIManager.m_col_ui7, 1f, -1f);
                pos.y += 28f;
                num *= 0.345f;
                pos.x -= num;

                if (loadout.loadoutType == MPLoadouts.LoadoutType.GUNNER)
                {
                    uie.DrawMpWeaponSimple(pos, loadout.weapons[0], active);
                    pos.x += num;
                    uie.DrawMpWeaponSimple(pos, loadout.weapons[1], active);
                    pos.x += num;
                    uie.DrawMpMissileSimple(pos, loadout.missiles[0], active);
                }
                else
                {
                    uie.DrawMpWeaponSimple(pos, loadout.weapons[0], active);
                    pos.x += num;
                    uie.DrawMpMissileSimple(pos, loadout.missiles[0], active);
                    pos.x += num;
                    uie.DrawMpMissileSimple(pos, loadout.missiles[1], active);
                }
            }

            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
            {
                int state = 0;
                foreach (var code in codes)
                {
                    if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(UIElement), "DrawMpLoadoutSimple"))
                    {
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPLoadouts_UIElement_DrawMpOverlayLoadout), "FakeDrawMpLoadoutSimple"));
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Ldarg_1);
                        yield return new CodeInstruction(OpCodes.Ldc_I4, state);
                        yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(GameManager), "m_local_player"));
                        yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Player), "m_use_loadout1"));
                        if (state == 1)
                        {
                            yield return new CodeInstruction(OpCodes.Ldc_I4_0);
                            yield return new CodeInstruction(OpCodes.Ceq);
                        }
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPLoadouts_UIElement_DrawMpOverlayLoadout), "DrawMpLoadoutSimple"));
                        state++;
                        continue;
                    }
                    yield return code;
                }
            }
        }
    }