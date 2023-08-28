using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod
{
    /// <summary>
    /// Revamps multiplayer networking in olmod to better trust the client when it comes to things like weapon firing position/rotation, selected primary/secondary, and resource amounts (except armor, that is still handled server-side).  The goal is to provide a more consistent game experience for the pilot when it comes to what they see firing on their screen and what actually happens with their fire on the server.  This also fixes several super annoying synchronization bugs that have been brought up by the community.
    /// </summary>
    public class MPSniperPackets
    {
        public const int NET_VERSION_SNIPER_PACKETS = 1;

        /// <summary>
        /// Determines whether sniper packets are enabled for the current game.
        /// </summary>
        static public bool enabled = true;

        /// <summary>
        /// Indicates whether a dev was just fired and should not be exploded.
        /// </summary>
        static internal bool justFiredDev = false;

        /// <summary>
        /// Indicates whether the server is allowed to detonate devs.
        /// </summary>
        static internal bool serverCanDetonate = false;

        /// <summary>
        /// Simple function to set a player's weapon, previous weapon, missile, or previous missile.  Used in PlayerWeaponSynchronizationMessage handlers.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="msg"></param>
        static internal void SetWeapon(Player player, PlayerWeaponSynchronizationMessage msg)
        {
            if (!enabled) return;

            switch (msg.m_type)
            {
                case PlayerWeaponSynchronizationMessage.ValueType.WEAPON:
                    player.Networkm_weapon_type = (WeaponType)msg.m_value;
                    player.m_weapon_type = (WeaponType)msg.m_value;
                    break;
                case PlayerWeaponSynchronizationMessage.ValueType.WEAPON_PREV:
                    player.Networkm_weapon_type_prev = (WeaponType)msg.m_value;
                    player.m_weapon_type_prev = (WeaponType)msg.m_value;
                    break;
                case PlayerWeaponSynchronizationMessage.ValueType.MISSILE:
                    player.Networkm_missile_type = (MissileType)msg.m_value;
                    player.m_missile_type = (MissileType)msg.m_value;
                    break;
                case PlayerWeaponSynchronizationMessage.ValueType.MISSILE_PREV:
                    player.Networkm_missile_type_prev = (MissileType)msg.m_value;
                    player.m_missile_type_prev = (MissileType)msg.m_value;
                    break;
            }
        }

        /// <summary>
        /// Replacement function for Server.IsActive() in MaybeFireWeapon and other places that need to deduct from the player's energy pool regardless if the function is called on client or server.
        /// </summary>
        /// <returns></returns>
        public static bool AlwaysUseEnergy()
        {
            if (!enabled) return Server.IsActive();

            return true;
        }

        /// <summary>
        /// Replacement function for ProjectileManager.PlayerFire in MaybeFireWeapon, MaybeFireMissile, and other places where we don't want weapon fire getting simulated on the server.  Also makes devastators, novas, creepers, and time bombs without collision on the client, so that they don't seemingly bounce off ships without actually hitting them.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="type"></param>
        /// <param name="pos"></param>
        /// <param name="rot"></param>
        /// <param name="strength"></param>
        /// <param name="upgrade_lvl"></param>
        /// <param name="no_sound"></param>
        /// <param name="slot"></param>
        /// <param name="force_id"></param>
        /// <returns></returns>
        public static ParticleElement MaybePlayerFire(Player player, ProjPrefab type, Vector3 pos, Quaternion rot, float strength = 0, WeaponUnlock upgrade_lvl = WeaponUnlock.LEVEL_0, bool no_sound = false, int slot = -1, int force_id = -1)
        {
            if (!enabled) return ProjectileManager.PlayerFire(player, type, pos, rot, strength, upgrade_lvl, no_sound, slot, force_id);
            if (!GameplayManager.IsMultiplayerActive) return ProjectileManager.PlayerFire(player, type, pos, rot, strength, upgrade_lvl, no_sound, slot, force_id);
            if (NetworkServer.active && !MPTweaks.ClientHasTweak(player.connectionToClient.connectionId, "sniper")) return ProjectileManager.PlayerFire(player, type, pos, rot, strength, upgrade_lvl, no_sound, slot, force_id);

            // Set this to false so that creepers and time bombs do not explode unless the server tells us.
            CreeperSyncExplode.m_allow_explosions = false;

            Weapon weapon = MPWeapons.WeaponLookup[(int)type];

            //if (player.isLocalPlayer && type == ProjPrefab.missile_devastator)
            if (player.isLocalPlayer && weapon != null && weapon.firingMode == FiringMode.DETONATOR && (int)type == (int)weapon.projprefab) // we only want this on the main projectile
            {
                MPSniperPackets.justFiredDev = true;
            }

            if (NetworkServer.active)
            {
                return null;
            }

            //if (player.isLocalPlayer && type != ProjPrefab.missile_devastator_mini && type != ProjPrefab.missile_smart_mini)
            if (player.isLocalPlayer && weapon != null && (int)type == (int)weapon.projprefab) // if it indexed to this but it's not projprefab, it's the subprojectile
            {
                Client.GetClient().Send(MessageTypes.MsgSniperPacket, new SniperPacketMessage
                {
                    m_player_id = player.netId,
                    m_type = type,
                    m_pos = pos,
                    m_rot = rot,
                    m_strength = strength,
                    m_upgrade_lvl = upgrade_lvl,
                    m_no_sound = no_sound,
                    m_slot = slot,
                    m_force_id = force_id
                });
            }

            var result = ProjectileManager.PlayerFire(player, type, pos, rot, strength, upgrade_lvl, no_sound, slot, force_id);

            //if (type == ProjPrefab.missile_devastator || type == ProjPrefab.missile_smart || type == ProjPrefab.missile_timebomb || type == ProjPrefab.missile_creeper)
            //if (weapon != null && weapon.GetType() == typeof(SecondaryWeapon) && (((SecondaryWeapon)weapon).subproj != ProjPrefabExt.none || weapon.MineHoming))
            if (weapon != null && MPCreeperSync.ExplodeSync.Contains(weapon.projprefab))
            {
                foreach (var proj in ProjectileManager.proj_list[(int)type])
                {
                    //Debug.Log("CCF disabling collider on a " + ((ProjPrefabExt)type).ToString() + " on " + (GameplayManager.IsDedicatedServer() ? "server" : "client"));
                    proj.c_go.GetComponent<Collider>().enabled = false;
                }
            }

            return result;
        }
    }

    /// <summary>
    /// This message allows for communication of sniper packets between the client and the server.
    /// </summary>
    public class SniperPacketMessage : MessageBase
    {
        public override void Serialize(NetworkWriter writer)
        {
            writer.Write((byte)MPSniperPackets.NET_VERSION_SNIPER_PACKETS);
            writer.Write(m_player_id);
            writer.Write((byte)m_type);
            writer.Write(m_pos.x);
            writer.Write(m_pos.y);
            writer.Write(m_pos.z);
            writer.Write(m_rot.w);
            writer.Write(m_rot.x);
            writer.Write(m_rot.y);
            writer.Write(m_rot.z);
            writer.Write(m_strength);
            writer.Write((byte)m_upgrade_lvl);
            writer.Write(m_no_sound);
            writer.Write(m_slot);
            writer.Write(m_force_id);
        }
        public override void Deserialize(NetworkReader reader)
        {
            var version = reader.ReadByte();
            m_player_id = reader.ReadNetworkId();
            m_type = (ProjPrefab)reader.ReadByte();
            m_pos = new Vector3();
            m_pos.x = reader.ReadSingle();
            m_pos.y = reader.ReadSingle();
            m_pos.z = reader.ReadSingle();
            m_rot = new Quaternion();
            m_rot.w = reader.ReadSingle();
            m_rot.x = reader.ReadSingle();
            m_rot.y = reader.ReadSingle();
            m_rot.z = reader.ReadSingle();
            m_strength = reader.ReadSingle();
            m_upgrade_lvl = (WeaponUnlock)reader.ReadByte();
            m_no_sound = reader.ReadBoolean();
            m_slot = reader.ReadInt32();
            m_force_id = reader.ReadInt32();
        }

        public NetworkInstanceId m_player_id;
        public ProjPrefab m_type;
        public Vector3 m_pos;
        public Quaternion m_rot;
        public float m_strength;
        public WeaponUnlock m_upgrade_lvl;
        public bool m_no_sound;
        public int m_slot;
        public int m_force_id;
    }

    /// <summary>
    /// This message allows for synchronization of what primary or secondary the player has selected.  Required for clients and server to agree on what weapon the player is using for things like sounds, ship model with weapons, etc.
    /// </summary>
    public class PlayerWeaponSynchronizationMessage : MessageBase
    {
        public enum ValueType
        {
            WEAPON,
            WEAPON_PREV,
            MISSILE,
            MISSILE_PREV
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write((byte)MPSniperPackets.NET_VERSION_SNIPER_PACKETS);
            writer.Write(m_player_id);
            writer.Write((byte)m_type);
            writer.Write(m_value);
        }

        public override void Deserialize(NetworkReader reader)
        {
            try
            {
                var version = reader.ReadByte();
                m_player_id = reader.ReadNetworkId();
                m_type = (ValueType)reader.ReadByte();
                m_value = reader.ReadInt32();
            }
            catch (Exception) { }
        }

        public NetworkInstanceId m_player_id;
        public ValueType m_type;
        public int m_value;
    }

    /// <summary>
    /// This message allows for the server to permit adding energy or ammo to a player, whether it be through picking up an energy orb or ammo pack, picking up a primary, or refueling.
    /// </summary>
    public class PlayerAddResourceMessage : MessageBase
    {
        public enum ValueType
        {
            ENERGY,
            AMMO,
            FALCON,
            MISSLE_POD,
            HUNTER,
            CREEPER,
            NOVA,
            DEVASTATOR,
            TIME_BOMB,
            VORTEX
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write((byte)MPSniperPackets.NET_VERSION_SNIPER_PACKETS);
            writer.Write(m_player_id);
            writer.Write((byte)m_type);
            writer.Write(m_value);
            writer.Write(m_max_value);
            writer.Write(m_default);
        }

        public override void Deserialize(NetworkReader reader)
        {
            var version = reader.ReadByte();
            m_player_id = reader.ReadNetworkId();
            m_type = (ValueType)reader.ReadByte();
            m_value = reader.ReadSingle();
            m_max_value = reader.ReadSingle();
            m_default = reader.ReadBoolean();
        }

        public NetworkInstanceId m_player_id;
        public ValueType m_type;
        public float m_value;
        public float m_max_value;

        /// <summary>
        /// Default is TRUE when the player is refueling.  This member is used to supress the "ENERGY INCREASED TO" message that appears for other sources of adding a resource.
        /// </summary>
        public bool m_default;
    }

    /// <summary>
    /// This message allows for the client to tell the server - and for the server to subsequently tell other clients - how much of a resource a player has.
    /// TODO: I'm not 100% sure how necessary it is for other clients to know about each others' resource counts.  It may be worth looking into to see what this is needed for on other clients.
    /// </summary>
    public class PlayerSyncResourceMessage : MessageBase
    {
        public enum ValueType
        {
            ENERGY,
            AMMO,
            FALCON,
            MISSILE_POD,
            HUNTER,
            CREEPER,
            NOVA,
            DEVASTATOR,
            TIME_BOMB,
            VORTEX
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.Write((byte)MPSniperPackets.NET_VERSION_SNIPER_PACKETS);
            writer.Write(m_player_id);
            writer.Write((byte)m_type);
            writer.Write(m_value);
        }

        public override void Deserialize(NetworkReader reader)
        {
            var version = reader.ReadByte();
            m_player_id = reader.ReadNetworkId();
            m_type = (ValueType)reader.ReadByte();
            m_value = reader.ReadSingle();
        }

        public NetworkInstanceId m_player_id;
        public ValueType m_type;
        public float m_value;
    }

    /// <summary>
    /// This message allows for the client to tell the server what its missile inventory was at the time of death.
    /// </summary>
    public class PlayerSyncAllMissilesMessage : MessageBase
    {
        public override void Serialize(NetworkWriter writer)
        {
            writer.Write((byte)MPSniperPackets.NET_VERSION_SNIPER_PACKETS);
            writer.Write(m_player_id);
            writer.Write(m_missile_ammo[0]);
            writer.Write(m_missile_ammo[1]);
            writer.Write(m_missile_ammo[2]);
            writer.Write(m_missile_ammo[3]);
            writer.Write(m_missile_ammo[4]);
            writer.Write(m_missile_ammo[5]);
            writer.Write(m_missile_ammo[6]);
            writer.Write(m_missile_ammo[7]);
        }

        public override void Deserialize(NetworkReader reader)
        {
            var version = reader.ReadByte();
            m_player_id = reader.ReadNetworkId();
            m_missile_ammo = new int[]
            {
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32()
            };
        }

        public NetworkInstanceId m_player_id;
        public int[] m_missile_ammo;
    }

    /// <summary>
    /// This message allows for the client to tell the server it wants to explode all detonators.
    /// </summary>
    public class DetonateMessage : MessageBase
    {
        public override void Serialize(NetworkWriter writer)
        {
            writer.Write((byte)MPSniperPackets.NET_VERSION_SNIPER_PACKETS);
            writer.Write(m_player_id);
        }

        public override void Deserialize(NetworkReader reader)
        {
            var version = reader.ReadByte();
            m_player_id = reader.ReadNetworkId();
        }

        public NetworkInstanceId m_player_id;
    }

    /// <summary>
    /// Handles sniper packet related messages on the client.
    /// </summary>
    [HarmonyPatch(typeof(Client), "RegisterHandlers")]
    class MPSniperPacketsClientHandlers
    {
        /// <summary>
        /// Handles the PlayerWeaponSynchronizationMessage on the client.  This allows for other clients to know when a client has changed their primary or secondary weapon.
        /// </summary>
        /// <param name="rawMsg"></param>
        private static void OnPlayerWeaponSynchronization(NetworkMessage rawMsg)
        {
            if (!MPSniperPackets.enabled) return;

            var msg = rawMsg.ReadMessage<PlayerWeaponSynchronizationMessage>();
            var player = Overload.NetworkManager.m_Players.Find(p => p.netId == msg.m_player_id);

            if (player == null || player.isLocalPlayer || player.c_player_ship.m_dead || player.c_player_ship.m_dying)
            {
                return;
            }

            MPSniperPackets.SetWeapon(player, msg);

            // Workaround for invisible players in JIP.  If they are invisible, make them visible if they sent any kind of sniper packet.
            MPJoinInProgress.SetReady(player, true);
        }

        /// <summary>
        /// Handles the PlayerAddResourceMessage on the client.  This allows for all clients to know when a client has gained some quantity of resource.  For the player getting the resource added and as long as it's not via refueling, this function will then synchronize the player's resource count with the server.
        /// </summary>
        /// <param name="rawMsg"></param>
        private static void OnPlayerAddResource(NetworkMessage rawMsg)
        {
            if (!MPSniperPackets.enabled) return;

            var msg = rawMsg.ReadMessage<PlayerAddResourceMessage>();
            var player = Overload.NetworkManager.m_Players.Find(p => p.netId == msg.m_player_id);

            if (player == null || NetworkServer.active || player.c_player_ship.m_dead || player.c_player_ship.m_dying)
            {
                return;
            }

            switch (msg.m_type)
            {
                case PlayerAddResourceMessage.ValueType.ENERGY:
                    if (msg.m_value < 0 || msg.m_value > 20)
                    {
                        // Drop packet if value is out of range.
                        Debug.Log($"*** WARNING *** Client {player.m_mp_name} received a value out of range! Tried to add {msg.m_value} of {msg.m_type}");
                        return;
                    }

                    player.m_energy = Mathf.Min(player.m_energy + msg.m_value, msg.m_max_value);
                    if (!msg.m_default)
                    {
                        if (player.isLocalPlayer)
                        {
                            Client.GetClient().Send(MessageTypes.MsgPlayerSyncResource, new PlayerSyncResourceMessage
                            {
                                m_player_id = player.netId,
                                m_type = PlayerSyncResourceMessage.ValueType.ENERGY,
                                m_value = player.m_energy
                            });
                        }
                        if (player.isLocalPlayer)
                        {
                            GameplayManager.AddHUDMessage(Loc.LS("ENERGY INCREASED TO") + " " + ((uint)player.m_energy).ToString(), -1, true);
                        }
                    }
                    break;
                case PlayerAddResourceMessage.ValueType.AMMO:
                    if (msg.m_value < 0 || msg.m_value > 200)
                    {
                        // Drop packet if value is out of range.
                        Debug.Log($"*** WARNING *** Client {player.m_mp_name} received a value out of range! Tried to add {msg.m_value} of {msg.m_type}");
                        return;
                    }

                    player.m_ammo = Mathf.Min(player.m_ammo + (int)msg.m_value, (int)msg.m_max_value);
                    if (player.isLocalPlayer)
                    {
                        Client.GetClient().Send(MessageTypes.MsgPlayerSyncResource, new PlayerSyncResourceMessage
                        {
                            m_player_id = player.netId,
                            m_type = PlayerSyncResourceMessage.ValueType.AMMO,
                            m_value = player.m_ammo
                        });
                    }
                    if (player.isLocalPlayer)
                    {
                        GameplayManager.AddHUDMessage(Loc.LS("AMMO INCREASED TO") + " " + ((uint)(int)player.m_ammo).ToString(), -1, true);
                    }
                    break;
                case PlayerAddResourceMessage.ValueType.FALCON:
                case PlayerAddResourceMessage.ValueType.MISSLE_POD:
                case PlayerAddResourceMessage.ValueType.HUNTER:
                case PlayerAddResourceMessage.ValueType.CREEPER:
                case PlayerAddResourceMessage.ValueType.NOVA:
                case PlayerAddResourceMessage.ValueType.DEVASTATOR:
                case PlayerAddResourceMessage.ValueType.TIME_BOMB:
                case PlayerAddResourceMessage.ValueType.VORTEX:
                    if (msg.m_value < 0 || msg.m_value > 80)
                    {
                        // Drop packet if value is out of range.
                        Debug.Log($"*** WARNING *** Client {player.m_mp_name} received a value out of range! Tried to add {msg.m_value} of {msg.m_type}");
                        return;
                    }
                    var hasMissiles = player.NumUnlockedMissilesWithAmmo() > 0;
                    var missileType = (MissileType)(msg.m_type - PlayerAddResourceMessage.ValueType.FALCON);
                    var oldAmt = player.m_missile_ammo[(int)missileType];
                    player.m_missile_ammo[(int)missileType] = Mathf.Min(player.m_missile_ammo[(int)missileType] + (int)msg.m_value, (int)msg.m_max_value);
                    var oldMissileType = player.m_missile_type;
                    var amt = player.m_missile_ammo[(int)missileType] - oldAmt;

                    if (amt > 0 && player.isLocalPlayer)
                    {
                        Client.GetClient().Send(MessageTypes.MsgPlayerSyncResource, new PlayerSyncResourceMessage
                        {
                            m_player_id = player.netId,
                            m_type = (PlayerSyncResourceMessage.ValueType)((int)PlayerSyncResourceMessage.ValueType.FALCON + (int)missileType),
                            m_value = player.m_missile_ammo[(int)missileType]
                        });
                    }

                    if (!hasMissiles && player.isLocalPlayer)
                    {
                        player.m_missile_level[(int)missileType] = ((GameplayManager.IsMission && missileType != MissileType.VORTEX) ? WeaponUnlock.LEVEL_0 : WeaponUnlock.LEVEL_1);
                        if (player.m_missile_type != missileType)
                        {
                            player.Networkm_missile_type = missileType;
                            player.c_player_ship.MissileSelectFX();
                            player.UpdateCurrentMissileName();
                        }
                    }
                    
                    if (oldAmt == 0 && MPAutoSelection.secondarySwapFlag)
                    {
                        if (GameplayManager.IsMultiplayerActive && NetworkMatch.InGameplay() && player.isLocalPlayer)
                        {
                            if (MPAutoSelection.areThereAllowedSecondaries())
                            {
                                int new_missile = MPAutoSelection.getMissilePriority(missileType);
                                int current_missile = MPAutoSelection.getMissilePriority(GameManager.m_local_player.m_missile_type);

                                if (new_missile < current_missile && !MPAutoSelection.SecondaryNeverSelect[new_missile])
                                {
                                    MPAutoSelection.swapToMissile((int)missileType);
                                }

                                //if (player.m_missile_type != MissileType.NUM && MPWeapons.secondaries[(int)player.m_missile_type].WarnSelect && player.m_old_missile_type != player.m_missile_type)
                                //if (player.m_missile_type != MissileType.NUM && MPWeapons.secondaries[(int)player.m_missile_type].WarnSelect && !MPWeapons.secondaries[(int)oldMissileType].WarnSelect)
                                Weapon currmissile = MPWeapons.secondaries[(int)player.m_missile_type];
                                Weapon oldmissile = MPWeapons.secondaries[(int)oldMissileType];
                                if (currmissile != null && currmissile.WarnSelect && (oldmissile == null || !oldmissile.WarnSelect))
                                {
                                    if (MPAutoSelection.zorc)
                                    {
                                        SFXCueManager.PlayCue2D(SFXCue.enemy_boss1_alert, 1f, 0f, 0f, false);
                                        GameplayManager.AlertPopup(string.Format(Loc.LS("{0} SELECTED"), Player.MissileNames[player.m_missile_type]), string.Empty, 5f);
                                    }
                                }
                                /*
                                if (GameManager.m_local_player.m_missile_type == MissileType.DEVASTATOR && oldMissileType != MissileType.DEVASTATOR)
                                {
                                    if (MPAutoSelection.zorc)
                                    {
                                        SFXCueManager.PlayCue2D(SFXCue.enemy_boss1_alert, 1f, 0f, 0f, false);
                                        GameplayManager.AlertPopup(Loc.LS("DEVASTATOR SELECTED"), string.Empty, 5f);
                                    }
                                }
                                */
                            }
                        }
                    }
                    
                    break;
            }

            // Workaround for invisible players in JIP.  If they are invisible, make them visible if they sent any kind of sniper packet.
            MPJoinInProgress.SetReady(player, true);
        }

        /// <summary>
        /// Handles the PlayerSyncResourceMessage on the client.  This allows for other clients to know what the resource counts for a client are.
        /// </summary>
        /// <param name="rawMsg"></param>
        private static void OnPlayerSyncResource(NetworkMessage rawMsg)
        {
            if (!MPSniperPackets.enabled) return;

            var msg = rawMsg.ReadMessage<PlayerSyncResourceMessage>();
            var player = Overload.NetworkManager.m_Players.Find(p => p.netId == msg.m_player_id);

            if (player == null || player.isLocalPlayer || player.c_player_ship.m_dead || player.c_player_ship.m_dying)
            {
                return;
            }

            switch (msg.m_type)
            {
                case PlayerSyncResourceMessage.ValueType.ENERGY:
                    player.m_energy = msg.m_value;
                    break;
                case PlayerSyncResourceMessage.ValueType.AMMO:
                    player.m_ammo = (int)msg.m_value;
                    break;
                case PlayerSyncResourceMessage.ValueType.FALCON:
                case PlayerSyncResourceMessage.ValueType.MISSILE_POD:
                case PlayerSyncResourceMessage.ValueType.HUNTER:
                case PlayerSyncResourceMessage.ValueType.CREEPER:
                case PlayerSyncResourceMessage.ValueType.NOVA:
                case PlayerSyncResourceMessage.ValueType.DEVASTATOR:
                case PlayerSyncResourceMessage.ValueType.TIME_BOMB:
                case PlayerSyncResourceMessage.ValueType.VORTEX:
                    var missileType = (MissileType)(msg.m_type - PlayerSyncResourceMessage.ValueType.FALCON);
                    player.m_missile_ammo[(int)missileType] = (int)msg.m_value;
                    break;
            }

            // Workaround for invisible players in JIP.  If they are invisible, make them visible if they sent any kind of sniper packet.
            MPJoinInProgress.SetReady(player, true);
        }

        /// <summary>
        /// Handles the PlayerSyncAllMissilesMessage on the client.  This allows for clients to know another player's missile counts when they were killed.
        /// </summary>
        /// <param name="rawMsg"></param>
        private static void OnPlayerSyncAllMissiles(NetworkMessage rawMsg)
        {
            if (!MPSniperPackets.enabled) return;

            var msg = rawMsg.ReadMessage<PlayerSyncAllMissilesMessage>();
            var player = Overload.NetworkManager.m_Players.Find(p => p.netId == msg.m_player_id);

            if (player == null || player.isLocalPlayer)
            {
                return;
            }

            for (int i = 0; i < 8; i++)
            {
                player.m_missile_ammo[i] = msg.m_missile_ammo[i];
            }

            // Workaround for invisible players in JIP.  If they are invisible, make them visible if they sent any kind of sniper packet.
            MPJoinInProgress.SetReady(player, true);
        }

        /// <summary>
        /// Explode Devastators on the server.
        /// </summary>
        /// <param name="msg"></param>
        static void OnDetonate(NetworkMessage rawMsg)
        {
            if (!MPSniperPackets.enabled) return;

            var msg = rawMsg.ReadMessage<DetonateMessage>();
            var player = Overload.NetworkManager.m_Players.Find(p => p.netId == msg.m_player_id);

            if (player == null)
            {
                return;
            }

            CreeperSyncExplode.m_allow_explosions = true;
            ProjectileManager.ExplodePlayerDetonators(player);
            CreeperSyncExplode.m_allow_explosions = false;

            // Workaround for invisible players in JIP.  If they are invisible, make them visible if they sent any kind of sniper packet.
            MPJoinInProgress.SetReady(player, true);
        }

        /// <summary>
        /// Harmony call to register the above handlers.
        /// </summary>
        static void Postfix()
        {
            if (Client.GetClient() == null)
                return;
            Client.GetClient().RegisterHandler(MessageTypes.MsgPlayerWeaponSynchronization, OnPlayerWeaponSynchronization);
            Client.GetClient().RegisterHandler(MessageTypes.MsgPlayerAddResource, OnPlayerAddResource);
            Client.GetClient().RegisterHandler(MessageTypes.MsgPlayerSyncResource, OnPlayerSyncResource);
            Client.GetClient().RegisterHandler(MessageTypes.MsgPlayerSyncAllMissiles, OnPlayerSyncAllMissiles);
            Client.GetClient().RegisterHandler(MessageTypes.MsgDetonate, OnDetonate);
        }
    }

    /// <summary>
    /// Handles sniper packet related messages on the server.
    /// </summary>
    [HarmonyPatch(typeof(Server), "RegisterHandlers")]
    class MPSniperPacketsServerHandlers
    {
        public static Dictionary<string, float> _primaryFireBuffer = new Dictionary<string, float>();
        public static Dictionary<string, float> _secondaryFireBuffer = new Dictionary<string, float>();
        public static Dictionary<string, float> _flareFireBuffer = new Dictionary<string, float>();

        private static readonly ProjPrefab[] _primaries = new ProjPrefab[] { ProjPrefab.proj_impulse, ProjPrefab.proj_vortex, ProjPrefab.proj_reflex, ProjPrefab.proj_shotgun, ProjPrefab.proj_driller, ProjPrefab.proj_flak_cannon, ProjPrefab.proj_thunderbolt, ProjPrefab.proj_beam };
        private static readonly ProjPrefab[] _secondaries = new ProjPrefab[] { ProjPrefab.missile_falcon, ProjPrefab.missile_pod, ProjPrefab.missile_hunter, ProjPrefab.missile_creeper, ProjPrefab.missile_smart, ProjPrefab.missile_timebomb, ProjPrefab.missile_devastator, ProjPrefab.missile_vortex };

        /// <summary>
        /// Handles the SniperPacketMessage on the server.  This generates the weapon fire on the server as received by the client, as long as they are still alive.
        /// </summary>
        /// <param name="rawMsg"></param>
        private static void OnSniperPacket(NetworkMessage rawMsg)
        {
            if (!MPSniperPackets.enabled) return;

            var msg = rawMsg.ReadMessage<SniperPacketMessage>();
            var player = Overload.NetworkManager.m_Players.Find(p => p.netId == msg.m_player_id);

            if (player == null || player.c_player_ship.m_dead || player.c_player_ship.m_dying || player.m_spectator)
            {
                return;
            }

            if (NetworkMatch.m_match_state != MatchState.POSTGAME)
            {
                var now = NetworkMatch.m_match_elapsed_seconds;

                var key = msg.m_player_id.Value.ToString();

                if (!_primaryFireBuffer.ContainsKey(key))
                {
                    _primaryFireBuffer.Add(key, now);
                }
                else if (_primaryFireBuffer[key] < now)
                {
                    _primaryFireBuffer[key] = now;
                }

                if (!_secondaryFireBuffer.ContainsKey(key))
                {
                    _secondaryFireBuffer.Add(key, now);
                }
                else if (_secondaryFireBuffer[key] < now)
                {
                    _secondaryFireBuffer[key] = now;
                }

                if (!_flareFireBuffer.ContainsKey(key))
                {
                    _flareFireBuffer.Add(key, now);
                }
                else if (_flareFireBuffer[key] < now)
                {
                    _flareFireBuffer[key] = now;
                }

                // Defaults handle case for flares and vortexes.
                float refireTime = 0.5f;
                int projectileCount = 1;

                switch (msg.m_type)
                {
                    case ProjPrefab.proj_flare:
                    case ProjPrefab.missile_vortex:
                        break;
                    case ProjPrefab.proj_impulse:
                        refireTime = ((!MPClassic.matchEnabled || player.m_weapon_level[(int)WeaponType.IMPULSE] == WeaponUnlock.LEVEL_2A) ? 0.28f : 0.25f) / (player.m_overdrive ? 1.5f : 1f);
                        projectileCount = (!MPClassic.matchEnabled || player.m_weapon_level[(int)WeaponType.IMPULSE] == WeaponUnlock.LEVEL_2A) ? 4 : 2;
                        break;
                    case ProjPrefab.proj_vortex:
                        refireTime = 0.12f / (player.m_overdrive ? 1.5f : 1f);
                        projectileCount = 3;
                        break;
                    case ProjPrefab.proj_reflex:
                        refireTime = 0.1f / (player.m_overdrive ? 1.5f : 1f);
                        break;
                    case ProjPrefab.proj_driller:
                        refireTime = 0.10f / (player.m_overdrive ? 1.5f : 1f); // lowered to allow the burstfire to function
                        break;
                    case ProjPrefab.proj_shotgun:
                        refireTime = (player.m_overdrive ? 0.55f : 0.45f) / (player.m_overdrive ? 1.5f : 1f);
                        projectileCount = 16;
                        break;
                    case ProjPrefab.proj_flak_cannon:
                        refireTime = 0.105f / (player.m_overdrive ? 1.5f : 1f);
                        projectileCount = 2;
                        break;
                    case ProjPrefab.proj_thunderbolt:
                        refireTime = 0.5f / (player.m_overdrive ? 1.5f : 1f);
                        //projectileCount = 2;
                        projectileCount = (MPShips.GetShip(player.c_player_ship).triTB ? 3 : 2);
                        break;
                    case ProjPrefab.proj_beam:
                        refireTime = (player.m_overdrive ? 0.29f : 0.23f) / (player.m_overdrive ? 1.5f : 1f);
                        projectileCount = 2;
                        break;
                    case ProjPrefab.missile_falcon:
                        refireTime = 0.3f;
                        break;
                    case ProjPrefab.missile_pod:
                        refireTime = 0.11f;
                        break;
                    case ProjPrefab.missile_hunter:
                        refireTime = 0.35f;
                        projectileCount = 2;
                        break;
                    case ProjPrefab.missile_creeper:
                        refireTime = 0.12f;
                        break;
                    case ProjPrefab.missile_smart:
                        refireTime = 0.4f;
                        break;
                    case ProjPrefab.missile_timebomb:
                    case ProjPrefab.missile_devastator:
                        refireTime = 1f;
                        break;
                
                    // Don't fire what we don't know about.
                    default:
                        if ((int)msg.m_type < (int)ProjPrefab.num) // It's a stock projectile, but not a valid one. Let all extension projectiles through.
                        {
                            Debug.Log($"{DateTime.Now:MM/dd/yyyy hh:mm:ss.fff tt} - Fire packet dropped, invalid projectile: {player.m_mp_name} - {msg.m_type}");
                            return;
                        }
                        refireTime = 0.01f; // generic fast time
                        break;
                }
                
                if (_primaries.Contains(msg.m_type))
                {
                    _primaryFireBuffer[key] += refireTime / projectileCount;

                    if (_primaryFireBuffer[key] - now - 0.25f > refireTime)
                    {
                        Debug.Log($"{DateTime.Now:MM/dd/yyyy hh:mm:ss.fff tt} {now:N3} - Fire packet dropped, client is bursting: {player.m_mp_name} - {msg.m_type}");

                        if (_primaryFireBuffer[key] - now > 2 * refireTime + 0.25f)
                        {
                            _primaryFireBuffer[key] = now + 2 * refireTime + 0.25f;
                        }

                        return;
                    }
                }
                else if (_secondaries.Contains(msg.m_type))
                {
                    _secondaryFireBuffer[key] += refireTime / projectileCount;

                    if (_secondaryFireBuffer[key] - now - 0.25f > refireTime)
                    {
                        Debug.Log($"{DateTime.Now:MM/dd/yyyy hh:mm:ss.fff tt} - Fire packet dropped, client is bursting: {player.m_mp_name} - {msg.m_type}");

                        if (_secondaryFireBuffer[key] - now > 2 * refireTime + 0.25f)
                        {
                            _secondaryFireBuffer[key] = now + 2 * refireTime + 0.25f;
                        }

                        return;
                    }
                }
                else if (msg.m_type == ProjPrefab.proj_flare)
                {
                    _flareFireBuffer[key] += refireTime / projectileCount;

                    if (_flareFireBuffer[key] - now - 0.25f > refireTime)
                    {
                        Debug.Log($"{DateTime.Now:MM/dd/yyyy hh:mm:ss.fff tt} - Fire packet dropped, client is bursting: {player.m_mp_name} - {msg.m_type}");

                        if (_flareFireBuffer[key] - now > 2 * refireTime + 0.25f)
                        {
                            _flareFireBuffer[key] = now + 2 * refireTime + 0.25f;
                        }

                        return;
                    }
                }
            }
            ProjectileManager.PlayerFire(player, msg.m_type, msg.m_pos, msg.m_rot, msg.m_strength, msg.m_upgrade_lvl, msg.m_no_sound, msg.m_slot, msg.m_force_id);
        }

        /// <summary>
        /// Handles the PlayerWeaponSynchronizationMessage on the server.  This allows for the server to know what primary/secondary the client is using, and then forward that information to other clients.
        /// </summary>
        /// <param name="rawMsg"></param>
        private static void OnPlayerWeaponSynchronization(NetworkMessage rawMsg)
        {
            if (!MPSniperPackets.enabled) return;

            var msg = rawMsg.ReadMessage<PlayerWeaponSynchronizationMessage>();
            var player = Overload.NetworkManager.m_Players.Find(p => p.netId == msg.m_player_id);

            if (player == null || player.c_player_ship.m_dead || player.c_player_ship.m_dying)
            {
                return;
            }

            MPSniperPackets.SetWeapon(player, msg);

            foreach (Player remotePlayer in Overload.NetworkManager.m_Players)
            {
                if (player.connectionToClient.connectionId != remotePlayer.connectionToClient.connectionId && MPTweaks.ClientHasTweak(remotePlayer.connectionToClient.connectionId, "sniper"))
                {
                    NetworkServer.SendToClient(remotePlayer.connectionToClient.connectionId, MessageTypes.MsgPlayerWeaponSynchronization, msg);
                }
            }
        }

        /// <summary>
        /// Handles the PlayerSyncResourceMessage on the server.  This allows for the server to know the quantity of a resource the client is using, and forwards that information to other clients.
        /// </summary>
        /// <param name="rawMsg"></param>
        private static void OnPlayerSyncResource(NetworkMessage rawMsg)
        {
            if (!MPSniperPackets.enabled) return;

            var msg = rawMsg.ReadMessage<PlayerSyncResourceMessage>();
            var player = Overload.NetworkManager.m_Players.Find(p => p.netId == msg.m_player_id);

            if (player == null || player.c_player_ship.m_dead || player.c_player_ship.m_dying)
            {
                return;
            }

            switch (msg.m_type)
            {
                case PlayerSyncResourceMessage.ValueType.ENERGY:
                    player.m_energy = msg.m_value;
                    break;
                case PlayerSyncResourceMessage.ValueType.AMMO:
                    player.m_ammo = (int)msg.m_value;
                    break;
                case PlayerSyncResourceMessage.ValueType.FALCON:
                case PlayerSyncResourceMessage.ValueType.MISSILE_POD:
                case PlayerSyncResourceMessage.ValueType.HUNTER:
                case PlayerSyncResourceMessage.ValueType.CREEPER:
                case PlayerSyncResourceMessage.ValueType.NOVA:
                case PlayerSyncResourceMessage.ValueType.DEVASTATOR:
                case PlayerSyncResourceMessage.ValueType.TIME_BOMB:
                case PlayerSyncResourceMessage.ValueType.VORTEX:
                    var missileType = (MissileType)(msg.m_type - PlayerSyncResourceMessage.ValueType.FALCON);
                    player.m_missile_ammo[(int)missileType] = (int)msg.m_value;
                    break;
            }

            foreach (Player remotePlayer in Overload.NetworkManager.m_Players)
            {
                if (player.connectionToClient.connectionId != remotePlayer.connectionToClient.connectionId && MPTweaks.ClientHasTweak(remotePlayer.connectionToClient.connectionId, "sniper"))
                {
                    NetworkServer.SendToClient(remotePlayer.connectionToClient.connectionId, MessageTypes.MsgPlayerSyncResource, msg);
                }
            }
        }

        /// <summary>
        /// Handles the PlayerSyncAllMissilesMessage on the server.  This allows for the server to know another player's missile counts when they were killed, and distribute that information to other clients.
        /// </summary>
        /// <param name="rawMsg"></param>
        private static void OnPlayerSyncAllMissiles(NetworkMessage rawMsg)
        {
            if (!MPSniperPackets.enabled) return;

            var msg = rawMsg.ReadMessage<PlayerSyncAllMissilesMessage>();
            var player = Overload.NetworkManager.m_Players.Find(p => p.netId == msg.m_player_id);

            if (player == null || !NetworkServer.active)
            {
                return;
            }

            for (int i = 0; i < 8; i++)
            {
                player.m_missile_ammo[i] = msg.m_missile_ammo[i];
            }

            foreach (Player remotePlayer in Overload.NetworkManager.m_Players)
            {
                if (player.connectionToClient.connectionId != remotePlayer.connectionToClient.connectionId && MPTweaks.ClientHasTweak(remotePlayer.connectionToClient.connectionId, "sniper"))
                {
                    NetworkServer.SendToClient(remotePlayer.connectionToClient.connectionId, MessageTypes.MsgPlayerSyncAllMissiles, msg);
                }
            }
        }

        /// <summary>
        /// Explode Devastators on the server.
        /// </summary>
        /// <param name="msg"></param>
        static void OnDetonate(NetworkMessage rawMsg)
        {
            if (!MPSniperPackets.enabled) return;

            var msg = rawMsg.ReadMessage<DetonateMessage>();
            var player = Overload.NetworkManager.m_Players.Find(p => p.netId == msg.m_player_id);

            if (player == null || player.isLocalPlayer || player.c_player_ship.m_dead || player.c_player_ship.m_dying)
            {
                return;
            }

            CreeperSyncExplode.m_allow_explosions = true;
            MPSniperPackets.serverCanDetonate = true;
            ProjectileManager.ExplodePlayerDetonators(player);
            CreeperSyncExplode.m_allow_explosions = false;
            MPSniperPackets.serverCanDetonate = false;

            foreach (Player remotePlayer in Overload.NetworkManager.m_Players)
            {
                if (player.connectionToClient.connectionId != remotePlayer.connectionToClient.connectionId && MPTweaks.ClientHasTweak(remotePlayer.connectionToClient.connectionId, "sniper"))
                {
                    NetworkServer.SendToClient(remotePlayer.connectionToClient.connectionId, MessageTypes.MsgDetonate, msg);
                }
            }
        }

        /// <summary>
        /// Harmony call to register the above handlers.
        /// </summary>
        static void Postfix()
        {
            NetworkServer.RegisterHandler(MessageTypes.MsgSniperPacket, OnSniperPacket);
            NetworkServer.RegisterHandler(MessageTypes.MsgPlayerWeaponSynchronization, OnPlayerWeaponSynchronization);
            NetworkServer.RegisterHandler(MessageTypes.MsgPlayerSyncResource, OnPlayerSyncResource);
            NetworkServer.RegisterHandler(MessageTypes.MsgPlayerSyncAllMissiles, OnPlayerSyncAllMissiles);
            NetworkServer.RegisterHandler(MessageTypes.MsgDetonate, OnDetonate);
        }
    }

    /*
    /// <summary>
    /// In base Overload, energy is only deducted from the player's total on the server, and then it synchronizes that energy amount to the client.  Instead, we are going to keep track of the energy on the client and sync it to the server.  Since everywhere where energy is used in this function check Server.IsActive, we instead redirect to our own function MPSniperPackets.AlwaysUseEnergy, which always returns true, and thus always deducts energy regardless as to whether it's on the server or the client.
    /// 
    /// In base Overload, the server simulates the position/rotation of each player's weapon fire.  Instead, we are going to let players decide the position/rotation of the weapon fire.  We replace the call to ProjectileManager.PlayerFire with our own call to MPSniperPackets.MaybePlayerFire that ensures that this simulation does not happen server side, and that when the client fires a weapon that it is synced to the server as a sniper packet.
    /// </summary>
    [HarmonyPatch(typeof(PlayerShip), "MaybeFireWeapon")]
    class MPSniperPacketsMaybeFireWeapon
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "IsActive")
                {
                    code.operand = AccessTools.Method(typeof(MPSniperPackets), "AlwaysUseEnergy");
                }
                else if (code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "PlayerFire")
                {
                    code.operand = AccessTools.Method(typeof(MPSniperPackets), "MaybePlayerFire");
                }

                yield return code;
            }
        }
    }
    */

    /// <summary>
    /// We want the client to control what weapon they are using, so if this function is called by the server, we ignore the call.
    /// </summary>
    [HarmonyPatch(typeof(Player), "SwitchToEnergyWeapon")]
    class MPSniperPacketsSwitchToEnergyWeapon
    {
        static bool Prefix(Player __instance)
        {
            if (!MPSniperPackets.enabled) return true;
            if (!(GameplayManager.IsMultiplayerActive && NetworkServer.active)) return true;
            if (NetworkServer.active && !MPTweaks.ClientHasTweak(__instance.connectionToClient.connectionId, "sniper")) return true;

            return false;
        }
    }

    /// <summary>
    /// We want the client to control what weapon they are using, so if this function is called by the server, we return the result that is expected, but otherwise ignore the call.
    /// </summary>
    [HarmonyPatch(typeof(Player), "SwitchToAmmoWeapon")]
    class MPSniperPacketsSwitchToAmmoWeapon
    {
        static bool Prefix(Player __instance, ref bool __result)
        {
            if (!MPSniperPackets.enabled) return true;
            if (!(GameplayManager.IsMultiplayerActive && NetworkServer.active)) return true;
            if (NetworkServer.active && !MPTweaks.ClientHasTweak(__instance.connectionToClient.connectionId, "sniper")) return true;

            __result = __instance.m_weapon_level[4] != WeaponUnlock.LOCKED || __instance.m_weapon_level[3] != WeaponUnlock.LOCKED || __instance.m_weapon_level[5] != WeaponUnlock.LOCKED;

            return false;
        }
    }

    /*
    /// <summary>
    /// Here, we are attaching to the end of PlayerShip.ProcessFiringControls to synchronize a player's resource when they release the primary fire key and the boost key, two sources of frequent resource use.
    /// 
    /// We also use this method to ensure mark when we have released the secondary fire key so that devastators don't explode in the player's face unless they purposely triggered them to.
    /// </summary>
    [HarmonyPatch(typeof(PlayerShip), "ProcessFiringControls")]
    class MPSniperPacketsProcessFiringControls
    {
        static void Postfix(PlayerShip __instance)
        {
            if (!MPSniperPackets.enabled) return;

            if (__instance.c_player.isLocalPlayer)
            {
                if (__instance.c_player.JustReleased(CCInput.FIRE_WEAPON))
                {
                    switch (__instance.c_player.m_weapon_type)
                    {
                        case WeaponType.IMPULSE:
                        case WeaponType.CYCLONE:
                        case WeaponType.REFLEX:
                        case WeaponType.THUNDERBOLT:
                        case WeaponType.LANCER:
                            Client.GetClient().Send(MessageTypes.MsgPlayerSyncResource, new PlayerSyncResourceMessage
                            {
                                m_player_id = __instance.c_player.netId,
                                m_type = PlayerSyncResourceMessage.ValueType.ENERGY,
                                m_value = __instance.c_player.m_energy
                            });
                            break;
                        case WeaponType.CRUSHER:
                        case WeaponType.DRILLER:
                        case WeaponType.FLAK:
                            Client.GetClient().Send(MessageTypes.MsgPlayerSyncResource, new PlayerSyncResourceMessage
                            {
                                m_player_id = __instance.c_player.netId,
                                m_type = PlayerSyncResourceMessage.ValueType.AMMO,
                                m_value = __instance.c_player.m_ammo
                            });
                            break;
                    }
                }

                if (__instance.c_player.JustReleased(CCInput.USE_BOOST))
                {
                    Client.GetClient().Send(MessageTypes.MsgPlayerSyncResource, new PlayerSyncResourceMessage
                    {
                        m_player_id = __instance.c_player.netId,
                        m_type = PlayerSyncResourceMessage.ValueType.ENERGY,
                        m_value = __instance.c_player.m_energy
                    });
                }

                if (__instance.c_player.isLocalPlayer && __instance.c_player.JustReleased(CCInput.FIRE_MISSILE))
                {
                    MPSniperPackets.justFiredDev = false;
                }
            }
        }
    }
    */

    /// <summary>
    /// Similar to MaybeFireWeapon, we redirect Projectile.PlayerFire to MPSniperPackets.MaybePlayerFire in order for the client to control where the flare gets fired from.
    /// </summary>
    [HarmonyPatch(typeof(PlayerShip), "FireFlare")]
    class MPSniperPacketsFireFlare
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "PlayerFire")
                {
                    code.operand = AccessTools.Method(typeof(MPSniperPackets), "MaybePlayerFire");
                }

                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "CallRpcFireFlare")]
    class MPSniperPacketsCallRpcFireFlare
    {
        private static bool Prefix(PlayerShip __instance)
        {
            return !MPTweaks.ClientHasTweak(__instance.c_player.connectionToClient.connectionId, "sniper");
        }
    }

    /// <summary>
    /// Similar to MaybeFireWeapon, we redirect Server.IsActive to MPSniperPackets.AlwaysUseEnergy to instruct the clients to deduct energy for a flare, and not wait for the server to synchronize the energy count.
    /// </summary>
    [HarmonyPatch(typeof(PlayerShip), "ProcessFlareAndHeadlightControls")]
    class MPSniperPacketsProcessFlareAndHeadlightControls
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "IsActive")
                {
                    code.operand = AccessTools.Method(typeof(MPSniperPackets), "AlwaysUseEnergy");
                }

                yield return code;
            }
        }
    }

    /// <summary>
    /// Similar to MaybeFireWeapon, we redirect Server.IsActive to MPSniperPackets.AlwaysUseEnergy to instruct the clients to deduct energy for boosting, and not wait for the server to synchronize the energy count.
    /// </summary>
    [HarmonyPatch(typeof(PlayerShip), "FixedUpdateProcessControlsInternal")]
    class MPSniperPacketsFixedUpdateProcessControlsInternal
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && code.operand is MethodInfo method && method.Name == "IsActive")
                {
                    code.operand = AccessTools.Method(typeof(MPSniperPackets), "AlwaysUseEnergy");
                }

                yield return code;
            }
        }
    }

    /// <summary>
    /// This prevents the server from synchronizing the weapon type being used to the client whose weapon type it wants to set.  Eliminates the use of the Unity SyncVar setup.
    /// </summary>
    [HarmonyPatch(typeof(Player), "Networkm_weapon_type", MethodType.Setter)]
    class MPSniperPacketsPlayerSynchronizeWeapon
    {
        static bool Prefix(Player __instance, WeaponType value)
        {
            if (!MPSniperPackets.enabled) return true;
            if (!GameplayManager.IsMultiplayerActive) return true;
            if (NetworkServer.active && !MPTweaks.ClientHasTweak(__instance.connectionToClient.connectionId, "sniper")) return true;

            if (__instance.m_weapon_type != value)
            {
                if (!NetworkServer.active)
                {
                    __instance.m_weapon_type = value;

                    if (__instance.isLocalPlayer)
                    {
                        Client.GetClient().Send(MessageTypes.MsgPlayerWeaponSynchronization, new PlayerWeaponSynchronizationMessage
                        {
                            m_player_id = __instance.netId,
                            m_type = PlayerWeaponSynchronizationMessage.ValueType.WEAPON,
                            m_value = (int)value
                        });
                    }
                }
            }
            return false;
        }
    }

    /// <summary>
    /// This prevents the server from synchronizing the previous weapon type being used to the client whose weapon type it wants to set.  Eliminates the use of the Unity SyncVar setup.
    /// </summary>
    [HarmonyPatch(typeof(Player), "Networkm_weapon_type_prev", MethodType.Setter)]
    class MPSniperPacketsPlayerSynchronizeWeaponPrev
    {
        static bool Prefix(Player __instance, WeaponType value)
        {
            if (!MPSniperPackets.enabled) return true;
            if (!GameplayManager.IsMultiplayerActive) return true;
            if (NetworkServer.active && !MPTweaks.ClientHasTweak(__instance.connectionToClient.connectionId, "sniper")) return true;

            if (__instance.m_weapon_type_prev != value)
            {
                if (!NetworkServer.active)
                {
                    __instance.m_weapon_type_prev = value;

                    if (__instance.isLocalPlayer)
                    {
                        Client.GetClient().Send(MessageTypes.MsgPlayerWeaponSynchronization, new PlayerWeaponSynchronizationMessage
                        {
                            m_player_id = __instance.netId,
                            m_type = PlayerWeaponSynchronizationMessage.ValueType.WEAPON_PREV,
                            m_value = (int)value
                        });
                    }
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Prevents the server from setting a client's energy.
    /// </summary>
    [HarmonyPatch(typeof(Player), "RpcSetEnergy")]
    class MPSniperPacketsDisableRpcSetEnergy
    {
        static bool Prefix(Player __instance)
        {
            if (!MPSniperPackets.enabled) return true;
            if (!GameplayManager.IsMultiplayerActive) return true;
            if (NetworkServer.active && !MPTweaks.ClientHasTweak(__instance.connectionToClient.connectionId, "sniper")) return true;

            return false;
        }
    }

    /// <summary>
    /// Prevents the server from setting a client's ammo.
    /// </summary>
    [HarmonyPatch(typeof(Player), "RpcSetAmmo")]
    class MPSniperPacketsDisableRpcSetAmmo
    {
        static bool Prefix(Player __instance)
        {
            if (!MPSniperPackets.enabled) return true;
            if (!GameplayManager.IsMultiplayerActive) return true;
            if (NetworkServer.active && !MPTweaks.ClientHasTweak(__instance.connectionToClient.connectionId, "sniper")) return true;

            return false;
        }
    }

    /// <summary>
    /// Instructs the server to tell all the clients that energy has been added to a player.
    /// </summary>
    [HarmonyPatch(typeof(Player), "AddEnergy")]
    class MPSniperPacketsAddEnergy
    {
        static void Postfix(bool __result, Player __instance, float energy)
        {
            if (!MPSniperPackets.enabled) return;

            if (__result && NetworkServer.active)
            {
                foreach (Player remotePlayer in Overload.NetworkManager.m_Players)
                {
                    if (MPTweaks.ClientHasTweak(remotePlayer.connectionToClient.connectionId, "sniper"))
                    {
                        NetworkServer.SendToClient(remotePlayer.connectionToClient.connectionId, MessageTypes.MsgPlayerAddResource, new PlayerAddResourceMessage
                        {
                            m_player_id = __instance.netId,
                            m_type = PlayerAddResourceMessage.ValueType.ENERGY,
                            m_value = energy,
                            m_max_value = Player.MAX_ENERGY,
                            m_default = false
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// Instructs the server to tell all the clients that energy has been added to a player.  This is the function Overload calls when refueling happens.
    /// </summary>
    [HarmonyPatch(typeof(Player), "AddEnergyDefault")]
    class MPSniperPacketsAddEnergyDefault
    {
        static void Postfix(bool __result, Player __instance, float energy)
        {
            if (!MPSniperPackets.enabled) return;

            if (__result && NetworkServer.active)
            {
                foreach (Player remotePlayer in Overload.NetworkManager.m_Players)
                {
                    if (MPTweaks.ClientHasTweak(remotePlayer.connectionToClient.connectionId, "sniper"))
                    {
                        NetworkServer.SendToClient(remotePlayer.connectionToClient.connectionId, MessageTypes.MsgPlayerAddResource, new PlayerAddResourceMessage
                        {
                            m_player_id = __instance.netId,
                            m_type = PlayerAddResourceMessage.ValueType.ENERGY,
                            m_value = energy,
                            m_max_value = 100,
                            m_default = true
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// Instructs the server to tell all the clients that energy has been added to a player.
    /// </summary>
    [HarmonyPatch(typeof(Player), "AddWeakEnergy")]
    class MPSniperPacketsAddWeakEnergy
    {
        static void Postfix(Player __instance, float energy, float max_energy)
        {
            if (!MPSniperPackets.enabled) return;

            if (NetworkServer.active)
            {
                foreach (Player remotePlayer in Overload.NetworkManager.m_Players)
                {
                    if (MPTweaks.ClientHasTweak(remotePlayer.connectionToClient.connectionId, "sniper"))
                    {
                        NetworkServer.SendToClient(remotePlayer.connectionToClient.connectionId, MessageTypes.MsgPlayerAddResource, new PlayerAddResourceMessage
                        {
                            m_player_id = __instance.netId,
                            m_type = PlayerAddResourceMessage.ValueType.ENERGY,
                            m_value = energy,
                            m_max_value = max_energy,
                            m_default = false
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// Prevents the server from sending a client a HUD message when energy is increased.
    /// </summary>
    [HarmonyPatch(typeof(Player), "TargetAddHUDMessageEnergyIncreased")]
    class MPSniperPacketsTargetAddHUDMessageEnergyIncreased
    {
        static bool Prefix(Player __instance)
        {
            if (!MPSniperPackets.enabled) return true;
            if (!GameplayManager.IsMultiplayerActive) return true;
            if (NetworkServer.active && !MPTweaks.ClientHasTweak(__instance.connectionToClient.connectionId, "sniper")) return true;

            return false;
        }
    }

    /// <summary>
    /// Allows UseEnergy to be called on both server and client.
    /// </summary>
    [HarmonyPatch(typeof(Player), "UseEnergy")]
    class MPSniperPacketsUseEnergy
    {
        static bool Prefix(Player __instance, float amount)
        {
            if (!MPSniperPackets.enabled) return true;
            if (!GameplayManager.IsMultiplayerActive) return true;
            if (NetworkServer.active && !MPTweaks.ClientHasTweak(__instance.connectionToClient.connectionId, "sniper")) return true;

            if (__instance.m_overdrive || Player.CheatUnlimited)
            {
                return false;
            }

            __instance.m_energy = Mathf.Max(0f, __instance.m_energy - amount * Player.ENERGY_USAGE[__instance.m_upgrade_level[1]]);
            return false;
        }
    }

    /// <summary>
    /// Instructs the server to tell all the clients that ammo has been added to a player.
    /// </summary>
    [HarmonyPatch(typeof(Player), "AddAmmo")]
    class MPSniperPacketsAddAmmo
    {
        static void Postfix(bool __result, Player __instance, int ammo)
        {
            if (!MPSniperPackets.enabled) return;

            if (__result && NetworkServer.active)
            {
                foreach (Player remotePlayer in Overload.NetworkManager.m_Players)
                {
                    if (MPTweaks.ClientHasTweak(remotePlayer.connectionToClient.connectionId, "sniper"))
                    {
                        NetworkServer.SendToClient(remotePlayer.connectionToClient.connectionId, MessageTypes.MsgPlayerAddResource, new PlayerAddResourceMessage
                        {
                            m_player_id = __instance.netId,
                            m_type = PlayerAddResourceMessage.ValueType.AMMO,
                            m_value = ammo,
                            m_max_value = Player.MAX_AMMO[__instance.m_upgrade_level[2]],
                            m_default = false
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// Prevents the server from sending a client a HUD message when ammo is increased.
    /// </summary>
    [HarmonyPatch(typeof(Player), "TargetAddHUDMessageAmmoIncreased")]
    class MPSniperPacketsTargetAddHUDMessageAmmoIncreased
    {
        static bool Prefix(Player __instance)
        {
            if (!MPSniperPackets.enabled) return true;
            if (!GameplayManager.IsMultiplayerActive) return true;
            if (NetworkServer.active && !MPTweaks.ClientHasTweak(__instance.connectionToClient.connectionId, "sniper")) return true;

            return false;
        }
    }

    /// <summary>
    /// Allows UseAmmo to be called on both server and client.
    /// </summary>
    [HarmonyPatch(typeof(Player), "UseAmmo")]
    class MPSniperPacketsUseAmmo
    {
        static bool Prefix(Player __instance, int amount)
        {
            if (!MPSniperPackets.enabled) return true;
            if (!GameplayManager.IsMultiplayerActive) return true;
            if (NetworkServer.active && !MPTweaks.ClientHasTweak(__instance.connectionToClient.connectionId, "sniper")) return true;

            if (__instance.m_overdrive || Player.CheatUnlimited)
            {
                return false;
            }

            __instance.m_ammo = Mathf.Max(0, __instance.m_ammo - amount);
            return false;
        }
    }

    /// <summary>
    /// Similar to MaybeFireWeapon, we redirect Projectile.PlayerFire to MPSniperPackets.MaybePlayerFire in order for the client to control where the missile gets fired from.
    /// 
    /// We also want to try to switch to a new secondary on the client no matter what at the end of MaybeFireMissile.
    /// </summary>
    [HarmonyPatch(typeof(PlayerShip), "MaybeFireMissile")]
    class MPSniperPacketsMaybeFireMissile
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "PlayerFire")
                {
                    code.operand = AccessTools.Method(typeof(MPSniperPackets), "MaybePlayerFire");
                }

                yield return code;
            }
        }
    }

    /// <summary>
    /// We want the client to control what missile they are using, so if this function is called by the server, we ignore the call.
    /// </summary>
    [HarmonyPatch(typeof(Player), "SwitchToNextMissileWithAmmo")]
    class MPSniperPacketsSwitchToNextMissileWithAmmo
    {
        static bool Prefix(Player __instance)
        {
            if (!MPSniperPackets.enabled) return true;
            if (!(GameplayManager.IsMultiplayerActive && NetworkServer.active)) return true;
            if (NetworkServer.active && !MPTweaks.ClientHasTweak(__instance.connectionToClient.connectionId, "sniper")) return true;

            return false;
        }
    }

    /// <summary>
    /// This prevents the server from synchronizing the missile type being used to the client whose missile type it wants to set.  Eliminates the use of the Unity SyncVar setup.
    /// </summary>
    [HarmonyPatch(typeof(Player), "Networkm_missile_type", MethodType.Setter)]
    class MPSniperPacketsPlayerSynchronizeMissile
    {
        static bool Prefix(Player __instance, MissileType value)
        {
            if (!MPSniperPackets.enabled) return true;
            if (!GameplayManager.IsMultiplayerActive) return true;
            if (NetworkServer.active && !MPTweaks.ClientHasTweak(__instance.connectionToClient.connectionId, "sniper")) return true;

            if (!NetworkServer.active)
            {
                __instance.m_missile_type = value;

                if (__instance.isLocalPlayer && value != MissileType.NUM && __instance.m_missile_ammo[(int)value] > 0)
                {
                    Client.GetClient().Send(MessageTypes.MsgPlayerWeaponSynchronization, new PlayerWeaponSynchronizationMessage
                    {
                        m_player_id = __instance.netId,
                        m_type = PlayerWeaponSynchronizationMessage.ValueType.MISSILE,
                        m_value = (int)value
                    });
                }
            }
            return false;
        }
    }

    /// <summary>
    /// This prevents the server from synchronizing the previous missile type being used to the client whose missile type it wants to set.  Eliminates the use of the Unity SyncVar setup.
    /// </summary>
    [HarmonyPatch(typeof(Player), "Networkm_missile_type_prev", MethodType.Setter)]
    class MPSniperPacketsPlayerSynchronizeMissilePrev
    {
        static bool Prefix(Player __instance, MissileType value)
        {
            if (!MPSniperPackets.enabled) return true;
            if (!GameplayManager.IsMultiplayerActive) return true;
            if (NetworkServer.active && !MPTweaks.ClientHasTweak(__instance.connectionToClient.connectionId, "sniper")) return true;

            if (__instance.m_missile_type_prev != value)
            {
                if (!NetworkServer.active)
                {
                    __instance.m_missile_type_prev = value;

                    if (__instance.isLocalPlayer)
                    {
                        Client.GetClient().Send(MessageTypes.MsgPlayerWeaponSynchronization, new PlayerWeaponSynchronizationMessage
                        {
                            m_player_id = __instance.netId,
                            m_type = PlayerWeaponSynchronizationMessage.ValueType.MISSILE_PREV,
                            m_value = (int)value
                        });
                    }
                }
            }
            return false;
        }
    }

    /// <summary>
    /// This prevents the server from telling the client what missile to use.
    /// </summary>
    [HarmonyPatch(typeof(Player), "RpcSetMissileType")]
    class MPSniperPacketsRpcSetMissileType
    {
        static bool Prefix(Player __instance)
        {
            if (!MPSniperPackets.enabled) return true;
            if (!GameplayManager.IsMultiplayerActive) return true;
            if (NetworkServer.active && !MPTweaks.ClientHasTweak(__instance.connectionToClient.connectionId, "sniper")) return true;

            return false;
        }
    }

    /// <summary>
    /// This prevents the server from telling the client how many missiles it has.
    /// </summary>
    [HarmonyPatch(typeof(Player), "RpcSetMissileAmmo")]
    class MPSniperPacketsRpcSetMissileAmmo
    {
        static bool Prefix(Player __instance)
        {
            if (!MPSniperPackets.enabled) return true;
            if (!GameplayManager.IsMultiplayerActive) return true;
            if (NetworkServer.active && !MPTweaks.ClientHasTweak(__instance.connectionToClient.connectionId, "sniper")) return true;

            return false;
        }
    }

    /// <summary>
    /// This will send the amount of ammo added to the player to all of the clients.
    /// </summary>
    [HarmonyPatch(typeof(Player), "AddMissileAmmo")]
    class MPSniperPacketsAddMissileAmmo
    {
        static void Prefix(Player __instance, int amt, MissileType mt, bool silent = false, bool super = false)
        {
            if (!MPSniperPackets.enabled) return;

            if (GameplayManager.IsMultiplayerActive && NetworkServer.active && __instance.CanAddMissileAmmo(mt, super))
            {
                int max = 999;
                if (super)
                {
                    if (GameplayManager.IsMultiplayerActive)
                    {
                        amt = Player.SUPER_MISSILE_AMMO_MP[(int)mt];
                    }
                    else
                    {
                        amt = __instance.GetMaxMissileAmmo(mt);
                    }
                }
                else
                {
                    max = __instance.GetMaxMissileAmmo(mt);
                }

                foreach (Player remotePlayer in Overload.NetworkManager.m_Players)
                {
                    if (MPTweaks.ClientHasTweak(remotePlayer.connectionToClient.connectionId, "sniper"))
                    {
                        NetworkServer.SendToClient(remotePlayer.connectionToClient.connectionId, MessageTypes.MsgPlayerAddResource, new PlayerAddResourceMessage
                        {
                            m_player_id = __instance.netId,
                            m_type = (PlayerAddResourceMessage.ValueType)((int)PlayerAddResourceMessage.ValueType.FALCON + (int)mt),
                            m_value = amt,
                            m_max_value = max,
                            m_default = false
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// When the player dies, we need to sync our missile inventory with the server so it knows what to spew.
    /// </summary>
    [HarmonyPatch(typeof(PlayerShip), "StartDying")]
    class MPSniperPacketsStartDying
    {
        static void Postfix(PlayerShip __instance)
        {
            if (!MPSniperPackets.enabled) return;

            if (Overload.NetworkManager.IsMultiplayerSceneLoaded() && !NetworkMatch.InGameplay())
            {
                return;
            }

            if (__instance.c_player.isLocalPlayer)
            {
                var missiles = __instance.c_player.m_missile_ammo.Select(a => (int)a).ToArray();
                Client.GetClient().Send(MessageTypes.MsgPlayerSyncAllMissiles, new PlayerSyncAllMissilesMessage
                {
                    m_player_id = __instance.c_player.netId,
                    m_missile_ammo = missiles
                });
            }
        }
    }

    /// <summary>
    /// This disables the ability of the server to control detonation of a devastator.
    /// </summary>
    [HarmonyPatch(typeof(PlayerShip), "DetonatorInFlight")]
    class MPSniperPacketsDetonatorInFlight
    {
        static bool Prefix(PlayerShip __instance)
        {
            if (!MPSniperPackets.enabled) return true;
            if (!(GameplayManager.IsMultiplayerActive && NetworkServer.active)) return true;
            if (NetworkServer.active && !MPTweaks.ClientHasTweak(__instance.c_player.connectionToClient.connectionId, "sniper")) return true;

            return false;
        }
    }

    /// <summary>
    /// This tells the server to explode any devastators in flight.
    /// </summary>
    [HarmonyPatch(typeof(ProjectileManager), "ExplodePlayerDetonators")]
    class MPSniperPacketsExplodePlayerDetonators
    {
        static bool Prefix(Player p)
        {
            if (!MPSniperPackets.enabled) return true;
            if (!GameplayManager.IsMultiplayerActive) return true;
            if (NetworkServer.active && !MPTweaks.ClientHasTweak(p.connectionToClient.connectionId, "sniper")) return true;

            if (NetworkServer.active && !MPSniperPackets.serverCanDetonate)
            {
                return false;
            }

            if (!NetworkServer.active && p.isLocalPlayer)
            {
                if (MPSniperPackets.justFiredDev)
                {
                    return false;
                }

                Client.GetClient().Send(MessageTypes.MsgDetonate, new DetonateMessage
                {
                    m_player_id = p.netId
                });
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Server), "ProjectileTypeHasLaunchDataSynced")]
    class MPSniperPacketsProjectileTypeHasLaunchDataSynced
    {
        static bool Prefix(ref bool __result)
        {
            if (!MPSniperPackets.enabled) return true;

            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Server), "SendJustPressedOrJustReleasedMessage")]
    class MPSniperPacketsSendJustPressedOrJustReleasedMessage
    {
        static bool Prefix(Player player, CCInput button)
        {
            if (!MPSniperPackets.enabled) return true;

            // This is necessary for charge effects to be played across all clients.
            if (button == CCInput.FIRE_WEAPON && player.m_weapon_type == WeaponType.THUNDERBOLT) return true;

            if (player.m_input_count[(int)button] == 1)
            {
                ButtonJustPressedMessage msg = new ButtonJustPressedMessage(player.netId, button);
                foreach (Player remotePlayer in Overload.NetworkManager.m_Players)
                {
                    if (MPTweaks.ClientHasTweak(remotePlayer.connectionToClient.connectionId, "sniper"))
                    {
                        NetworkServer.SendToClient(remotePlayer.connectionToClient.connectionId, 66, msg);
                    }
                }
            }
            else if (player.m_input_count[(int)button] == -1)
            {
                ButtonJustReleasedMessage msg2 = new ButtonJustReleasedMessage(player.netId, button);
                foreach (Player remotePlayer in Overload.NetworkManager.m_Players)
                {
                    if (MPTweaks.ClientHasTweak(remotePlayer.connectionToClient.connectionId, "sniper"))
                    {
                        NetworkServer.SendToClient(remotePlayer.connectionToClient.connectionId, 67, msg2);
                    }
                }
            }

            return false;
        }
    }
}
