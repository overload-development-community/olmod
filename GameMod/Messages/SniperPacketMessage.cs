using System;
using System.Collections.Generic;
using System.Linq;
using GameMod.Metadata;
using GameMod.Objects;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod.Messages {
    /// <summary>
    /// This message allows for communication of sniper packets between the client and the server.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    public class SniperPacketMessage : MessageBase {
        public NetworkInstanceId m_player_id;
        public ProjPrefab m_type;
        public Vector3 m_pos;
        public Quaternion m_rot;
        public float m_strength;
        public WeaponUnlock m_upgrade_lvl;
        public bool m_no_sound;
        public int m_slot;
        public int m_force_id;

        public static Dictionary<string, float> _primaryFireBuffer = new Dictionary<string, float>();
        public static Dictionary<string, float> _secondaryFireBuffer = new Dictionary<string, float>();
        public static Dictionary<string, float> _flareFireBuffer = new Dictionary<string, float>();

        private static readonly ProjPrefab[] _primaries = new ProjPrefab[] { ProjPrefab.proj_impulse, ProjPrefab.proj_vortex, ProjPrefab.proj_reflex, ProjPrefab.proj_shotgun, ProjPrefab.proj_driller, ProjPrefab.proj_flak_cannon, ProjPrefab.proj_thunderbolt, ProjPrefab.proj_beam };
        private static readonly ProjPrefab[] _secondaries = new ProjPrefab[] { ProjPrefab.missile_falcon, ProjPrefab.missile_pod, ProjPrefab.missile_hunter, ProjPrefab.missile_creeper, ProjPrefab.missile_smart, ProjPrefab.missile_timebomb, ProjPrefab.missile_devastator, ProjPrefab.missile_vortex };

        public override void Serialize(NetworkWriter writer) {
            writer.Write((byte)1);
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

        public override void Deserialize(NetworkReader reader) {
            var version = reader.ReadByte();
            m_player_id = reader.ReadNetworkId();
            m_type = (ProjPrefab)reader.ReadByte();
            m_pos = new Vector3 {
                x = reader.ReadSingle(),
                y = reader.ReadSingle(),
                z = reader.ReadSingle()
            };
            m_rot = new Quaternion {
                w = reader.ReadSingle(),
                x = reader.ReadSingle(),
                y = reader.ReadSingle(),
                z = reader.ReadSingle()
            };
            m_strength = reader.ReadSingle();
            m_upgrade_lvl = (WeaponUnlock)reader.ReadByte();
            m_no_sound = reader.ReadBoolean();
            m_slot = reader.ReadInt32();
            m_force_id = reader.ReadInt32();
        }

        /// <summary>
        /// Handles the SniperPacketMessage on the server.  This generates the weapon fire on the server as received by the client, as long as they are still alive.
        /// </summary>
        /// <param name="rawMsg"></param>
        public static void ServerHandler(NetworkMessage rawMsg) {
            if (!SniperPackets.enabled) return;

            var msg = rawMsg.ReadMessage<SniperPacketMessage>();
            var player = Overload.NetworkManager.m_Players.Find(p => p.netId == msg.m_player_id);

            if (player == null || player.c_player_ship.m_dead || player.c_player_ship.m_dying || player.m_spectator) {
                return;
            }

            if (NetworkMatch.m_match_state != MatchState.POSTGAME) {
                var now = NetworkMatch.m_match_elapsed_seconds;

                var key = msg.m_player_id.Value.ToString();

                if (!_primaryFireBuffer.ContainsKey(key)) {
                    _primaryFireBuffer.Add(key, now);
                } else if (_primaryFireBuffer[key] < now) {
                    _primaryFireBuffer[key] = now;
                }

                if (!_secondaryFireBuffer.ContainsKey(key)) {
                    _secondaryFireBuffer.Add(key, now);
                } else if (_secondaryFireBuffer[key] < now) {
                    _secondaryFireBuffer[key] = now;
                }

                if (!_flareFireBuffer.ContainsKey(key)) {
                    _flareFireBuffer.Add(key, now);
                } else if (_flareFireBuffer[key] < now) {
                    _flareFireBuffer[key] = now;
                }

                // Defaults handle case for flares and vortexes.
                float refireTime = 0.5f;
                int projectileCount = 1;

                switch (msg.m_type) {
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
                        refireTime = 0.22f / (player.m_overdrive ? 1.5f : 1f);
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
                        projectileCount = 2;
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
                        Debug.Log($"{DateTime.Now:MM/dd/yyyy hh:mm:ss.fff tt} - Fire packet dropped, invalid projectile: {player.m_mp_name} - {msg.m_type}");
                        return;
                }

                if (_primaries.Contains(msg.m_type)) {
                    _primaryFireBuffer[key] += refireTime / projectileCount;

                    if (_primaryFireBuffer[key] - now - 0.25f > refireTime) {
                        Debug.Log($"{DateTime.Now:MM/dd/yyyy hh:mm:ss.fff tt} {now:N3} - Fire packet dropped, client is bursting: {player.m_mp_name} - {msg.m_type}");

                        if (_primaryFireBuffer[key] - now > 2 * refireTime + 0.25f) {
                            _primaryFireBuffer[key] = now + 2 * refireTime + 0.25f;
                        }

                        return;
                    }
                } else if (_secondaries.Contains(msg.m_type)) {
                    _secondaryFireBuffer[key] += refireTime / projectileCount;

                    if (_secondaryFireBuffer[key] - now - 0.25f > refireTime) {
                        Debug.Log($"{DateTime.Now:MM/dd/yyyy hh:mm:ss.fff tt} - Fire packet dropped, client is bursting: {player.m_mp_name} - {msg.m_type}");

                        if (_secondaryFireBuffer[key] - now > 2 * refireTime + 0.25f) {
                            _secondaryFireBuffer[key] = now + 2 * refireTime + 0.25f;
                        }

                        return;
                    }
                } else if (msg.m_type == ProjPrefab.proj_flare) {
                    _flareFireBuffer[key] += refireTime / projectileCount;

                    if (_flareFireBuffer[key] - now - 0.25f > refireTime) {
                        Debug.Log($"{DateTime.Now:MM/dd/yyyy hh:mm:ss.fff tt} - Fire packet dropped, client is bursting: {player.m_mp_name} - {msg.m_type}");

                        if (_flareFireBuffer[key] - now > 2 * refireTime + 0.25f) {
                            _flareFireBuffer[key] = now + 2 * refireTime + 0.25f;
                        }

                        return;
                    }
                }
            }

            ProjectileManager.PlayerFire(player, msg.m_type, msg.m_pos, msg.m_rot, msg.m_strength, msg.m_upgrade_lvl, msg.m_no_sound, msg.m_slot, msg.m_force_id);
        }
    }
}
