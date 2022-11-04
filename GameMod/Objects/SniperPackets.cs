using GameMod.Messages;
using GameMod.Metadata;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod.Objects {
    /// <summary>
    /// Revamps multiplayer networking in olmod to better trust the client when it comes to things like weapon firing position/rotation, selected primary/secondary, and resource amounts (except armor, that is still handled server-side).  The goal is to provide a more consistent game experience for the pilot when it comes to what they see firing on their screen and what actually happens with their fire on the server.  This also fixes several super annoying synchronization bugs that have been brought up by the community.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    public static class SniperPackets {
        /// <summary>
        /// Determines whether sniper packets are enabled for the current game.
        /// </summary>
        public static bool enabled = true;

        /// <summary>
        /// Indicates whether a dev was just fired and should not be exploded.
        /// </summary>
        public static bool justFiredDev = false;

        /// <summary>
        /// Indicates whether the server is allowed to detonate devs.
        /// </summary>
        public static bool serverCanDetonate = false;

        /// <summary>
        /// Simple function to set a player's weapon, previous weapon, missile, or previous missile.  Used in PlayerWeaponSynchronizationMessage handlers.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="msg"></param>
        public static void SetWeapon(Player player, PlayerWeaponSynchronizationMessage msg) {
            if (!enabled) return;

            switch (msg.m_type) {
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
        public static bool AlwaysUseEnergy() {
            if (!enabled) return Server.IsActive();

            return true;
        }

        /// <summary>
        /// Replacement function for ProjectileManager.PlayerFire in MaybeFireWeapon, MaybeFireMissile, and other places where we don't want weapon fire getting simulated on the server.  Also creates devastators, novas, creepers, and time bombs without collision on the client, so that they don't seemingly bounce off ships without actually hitting them.
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
        public static ParticleElement MaybePlayerFire(Player player, ProjPrefab type, Vector3 pos, Quaternion rot, float strength = 0, WeaponUnlock upgrade_lvl = WeaponUnlock.LEVEL_0, bool no_sound = false, int slot = -1, int force_id = -1) {
            if (!enabled) return ProjectileManager.PlayerFire(player, type, pos, rot, strength, upgrade_lvl, no_sound, slot, force_id);
            if (!GameplayManager.IsMultiplayerActive) return ProjectileManager.PlayerFire(player, type, pos, rot, strength, upgrade_lvl, no_sound, slot, force_id);
            if (NetworkServer.active && !Tweaks.ClientHasMod(player.connectionToClient.connectionId)) return ProjectileManager.PlayerFire(player, type, pos, rot, strength, upgrade_lvl, no_sound, slot, force_id);

            // Set this to false so that creepers and time bombs do not explode unless the server tells us.
            CreeperSyncExplode.m_allow_explosions = false;

            if (player.isLocalPlayer && type == ProjPrefab.missile_devastator) {
                SniperPackets.justFiredDev = true;
            }

            if (NetworkServer.active) {
                return null;
            }

            if (player.isLocalPlayer && type != ProjPrefab.missile_devastator_mini && type != ProjPrefab.missile_smart_mini) {
                Client.GetClient().Send(MessageTypes.MsgSniperPacket, new SniperPacketMessage {
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

            if (type == ProjPrefab.missile_devastator || type == ProjPrefab.missile_smart || type == ProjPrefab.missile_timebomb || type == ProjPrefab.missile_creeper) {
                foreach (var proj in ProjectileManager.proj_list[(int)type]) {
                    proj.c_go.GetComponent<Collider>().enabled = false;
                }
            }

            return result;
        }
    }
}
