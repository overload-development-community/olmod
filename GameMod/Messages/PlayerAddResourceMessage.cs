using GameMod.Metadata;
using GameMod.Objects;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod.Messages {
    /// <summary>
    /// This message allows for the server to permit adding energy or ammo to a player, whether it be through picking up an energy orb or ammo pack, picking up a primary, or refueling.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    public class PlayerAddResourceMessage : MessageBase {
        public NetworkInstanceId m_player_id;
        public ValueType m_type;
        public float m_value;
        public float m_max_value;

        public enum ValueType {
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

        /// <summary>
        /// Default is TRUE when the player is refueling.  This member is used to supress the "ENERGY INCREASED TO" message that appears for other sources of adding a resource.
        /// </summary>
        public bool m_default;

        public override void Serialize(NetworkWriter writer) {
            writer.Write((byte)1);
            writer.Write(m_player_id);
            writer.Write((byte)m_type);
            writer.Write(m_value);
            writer.Write(m_max_value);
            writer.Write(m_default);
        }

        public override void Deserialize(NetworkReader reader) {
            var version = reader.ReadByte();
            m_player_id = reader.ReadNetworkId();
            m_type = (ValueType)reader.ReadByte();
            m_value = reader.ReadSingle();
            m_max_value = reader.ReadSingle();
            m_default = reader.ReadBoolean();
        }

        /// <summary>
        /// Handles the PlayerAddResourceMessage on the client.  This allows for all clients to know when a client has gained some quantity of resource.  For the player getting the resource added and as long as it's not via refueling, this function will then synchronize the player's resource count with the server.
        /// </summary>
        /// <param name="rawMsg"></param>
        public static void ClientHandler(NetworkMessage rawMsg) {
            if (!SniperPackets.enabled) return;

            var msg = rawMsg.ReadMessage<PlayerAddResourceMessage>();
            var player = Overload.NetworkManager.m_Players.Find(p => p.netId == msg.m_player_id);

            if (player == null || NetworkServer.active || player.c_player_ship.m_dead || player.c_player_ship.m_dying) {
                return;
            }

            switch (msg.m_type) {
                case ValueType.ENERGY:
                    if (msg.m_value < 0 || msg.m_value > 20) {
                        // Drop packet if value is out of range.
                        Debug.Log($"*** WARNING *** Client {player.m_mp_name} received a value out of range! Tried to add {msg.m_value} of {msg.m_type}");
                        return;
                    }

                    player.m_energy = Mathf.Min(player.m_energy + msg.m_value, msg.m_max_value);
                    if (!msg.m_default) {
                        if (player.isLocalPlayer) {
                            Client.GetClient().Send(MessageTypes.MsgPlayerSyncResource, new PlayerSyncResourceMessage {
                                m_player_id = player.netId,
                                m_type = PlayerSyncResourceMessage.ValueType.ENERGY,
                                m_value = player.m_energy
                            });
                        }
                        if (player.isLocalPlayer) {
                            GameplayManager.AddHUDMessage(Loc.LS("ENERGY INCREASED TO") + " " + ((uint)player.m_energy).ToString(), -1, true);
                        }
                    }
                    break;
                case ValueType.AMMO:
                    if (msg.m_value < 0 || msg.m_value > 200) {
                        // Drop packet if value is out of range.
                        Debug.Log($"*** WARNING *** Client {player.m_mp_name} received a value out of range! Tried to add {msg.m_value} of {msg.m_type}");
                        return;
                    }

                    player.m_ammo = Mathf.Min(player.m_ammo + (int)msg.m_value, (int)msg.m_max_value);
                    if (player.isLocalPlayer) {
                        Client.GetClient().Send(MessageTypes.MsgPlayerSyncResource, new PlayerSyncResourceMessage {
                            m_player_id = player.netId,
                            m_type = PlayerSyncResourceMessage.ValueType.AMMO,
                            m_value = player.m_ammo
                        });
                    }
                    if (player.isLocalPlayer) {
                        GameplayManager.AddHUDMessage(Loc.LS("AMMO INCREASED TO") + " " + ((uint)(int)player.m_ammo).ToString(), -1, true);
                    }
                    break;
                case ValueType.FALCON:
                case ValueType.MISSLE_POD:
                case ValueType.HUNTER:
                case ValueType.CREEPER:
                case ValueType.NOVA:
                case ValueType.DEVASTATOR:
                case ValueType.TIME_BOMB:
                case ValueType.VORTEX:
                    if (msg.m_value < 0 || msg.m_value > 80) {
                        // Drop packet if value is out of range.
                        Debug.Log($"*** WARNING *** Client {player.m_mp_name} received a value out of range! Tried to add {msg.m_value} of {msg.m_type}");
                        return;
                    }
                    var hasMissiles = player.NumUnlockedMissilesWithAmmo() > 0;
                    var missileType = (MissileType)(msg.m_type - ValueType.FALCON);
                    var oldAmt = player.m_missile_ammo[(int)missileType];
                    player.m_missile_ammo[(int)missileType] = Mathf.Min(player.m_missile_ammo[(int)missileType] + (int)msg.m_value, (int)msg.m_max_value);
                    var oldMissileType = player.m_missile_type;
                    var amt = player.m_missile_ammo[(int)missileType] - oldAmt;

                    if (amt > 0 && player.isLocalPlayer) {
                        Client.GetClient().Send(MessageTypes.MsgPlayerSyncResource, new PlayerSyncResourceMessage {
                            m_player_id = player.netId,
                            m_type = (PlayerSyncResourceMessage.ValueType)((int)PlayerSyncResourceMessage.ValueType.FALCON + (int)missileType),
                            m_value = player.m_missile_ammo[(int)missileType]
                        });
                    }

                    if (!hasMissiles && player.isLocalPlayer) {
                        player.m_missile_level[(int)missileType] = ((GameplayManager.IsMission && missileType != MissileType.VORTEX) ? WeaponUnlock.LEVEL_0 : WeaponUnlock.LEVEL_1);
                        if (player.m_missile_type != missileType) {
                            player.Networkm_missile_type = missileType;
                            player.c_player_ship.MissileSelectFX();
                            player.UpdateCurrentMissileName();
                        }
                    }

                    if (oldAmt == 0 && MPAutoSelection.secondarySwapFlag) {
                        if (GameplayManager.IsMultiplayerActive && NetworkMatch.InGameplay() && player.isLocalPlayer) {
                            if (MPAutoSelection.areThereAllowedSecondaries()) {
                                int new_missile = MPAutoSelection.getMissilePriority(missileType);
                                int current_missile = MPAutoSelection.getMissilePriority(GameManager.m_local_player.m_missile_type);

                                if (new_missile < current_missile && !MPAutoSelection.SecondaryNeverSelect[new_missile]) {
                                    MPAutoSelection.swapToMissile((int)missileType);
                                }

                                if (GameManager.m_local_player.m_missile_type == MissileType.DEVASTATOR && oldMissileType != MissileType.DEVASTATOR) {
                                    if (MPAutoSelection.zorc) {
                                        SFXCueManager.PlayCue2D(SFXCue.enemy_boss1_alert, 1f, 0f, 0f, false);
                                        GameplayManager.AlertPopup(Loc.LS("DEVASTATOR SELECTED"), string.Empty, 5f);
                                    }
                                }
                            }
                        }
                    }

                    break;
            }

            // Workaround for invisible players in JIP.  If they are invisible, make them visible if they sent any kind of sniper packet.
            MPJoinInProgress.SetReady(player, true);
        }
    }
}
