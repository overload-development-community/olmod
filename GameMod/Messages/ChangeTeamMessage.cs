using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameMod.Metadata;
using GameMod.Objects;
using Overload;
using UnityEngine.Networking;

namespace GameMod.Messages {
    [Mod(Mods.Teams)]
    public class ChangeTeamMessage : MessageBase {
        public NetworkInstanceId netId;
        public MpTeam newTeam;

        public override void Serialize(NetworkWriter writer) {
            writer.Write((byte)1); // version
            writer.Write(netId);
            writer.WritePackedUInt32((uint)newTeam);
        }

        public override void Deserialize(NetworkReader reader) {
            var version = reader.ReadByte();
            netId = reader.ReadNetworkId();
            newTeam = (MpTeam)reader.ReadPackedUInt32();
        }

        public static void ClientHandler(NetworkMessage rawMsg) {
            var msg = rawMsg.ReadMessage<ChangeTeamMessage>();
            var player = Overload.NetworkManager.m_Players.FirstOrDefault(x => x.netId == msg.netId);

            if (player != null && msg.newTeam != player.m_mp_team) {
                player.m_mp_team = msg.newTeam;
                Teams.UpdateClientColors();

                GameplayManager.AddHUDMessage($"{player.m_mp_name} changed teams", -1, true);
                SFXCueManager.PlayRawSoundEffect2D(SoundEffect.hud_notify_message1);
            }
        }

        public static void ServerHandler(NetworkMessage rawMsg) {
            var msg = rawMsg.ReadMessage<ChangeTeamMessage>();
            DoTeamChange(msg);
        }

        public static void DoTeamChange(ChangeTeamMessage msg) {
            var targetPlayer = Overload.NetworkManager.m_Players.FirstOrDefault(x => x.netId == msg.netId);

            Tracker.AddTeamChange(targetPlayer, msg.newTeam);
            targetPlayer.Networkm_mp_team = msg.newTeam;

            // Also need to set the Lobby data as it gets used for things like tracker stats
            var targetLobbyData = NetworkMatch.m_players.FirstOrDefault(x => x.Value.m_name == targetPlayer.m_mp_name).Value;
            targetLobbyData.m_team = msg.newTeam;

            // CTF behavior, need to account for flag carrier switching
            if (CTF.IsActiveServer) {
                if (CTF.PlayerHasFlag.ContainsKey(targetPlayer.netId) && CTF.PlayerHasFlag.TryGetValue(targetPlayer.netId, out int flag)) {
                    CTF.SendCTFLose(-1, targetPlayer.netId, flag, FlagState.HOME, true);

                    if (!CTF.CarrierBoostEnabled) {
                        targetPlayer.c_player_ship.m_boost_overheat_timer = 0;
                        targetPlayer.c_player_ship.m_boost_heat = 0;
                    }

                    CTF.NotifyAll(CTFEvent.RETURN, $"{Teams.TeamName(targetPlayer.m_mp_team)} FLAG RETURNED AFTER {targetPlayer.m_mp_name} CHANGED TEAMS",
                        targetPlayer, flag);
                }
            }

            foreach (var player in Overload.NetworkManager.m_Players.Where(x => x.connectionToClient.connectionId > 0)) {
                // Send message to clients with 'changeteam' support to give them HUD message
                if (Tweaks.ClientHasMod(player.connectionToClient.connectionId))
                    NetworkServer.SendToClient(player.connectionToClient.connectionId, MessageTypes.MsgChangeTeam, msg);
            }
        }
    }
}
