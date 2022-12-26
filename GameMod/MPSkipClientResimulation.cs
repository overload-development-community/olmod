using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod
{
    class MPSkipClientResimulation
    {
        // Client.ReconcileServerPlayerState is called at every fixed physics tick
        // it will replay the local simulation from the last packet we've seen
        // from the server.
        //
        // This means that we get a ping-dependent amount of physics resimulation steps
        // at each single physics tick, which can be be quite expensive.
        //
        // However, the resimulation is only necessary if the ship's state seen by the
        // server differs from our client's view (for example by collisions with
        // other ships or being hit by projectiles, which aren't accurate on the client
        // due to the lag).
        //
        // This patch skips the client side resimulation if we detect that our positon
        // and rotation we had in the past at some tick does not differ from the
        // position and rotation the server sent back to us for that particular tick.
        //
        // NOTE: we do not compare any other members of the PlayerState here.
        // The rationale behind this is that in practice, the server never really
        // changes those without also affecting position and rotation, and even if
        // it does, it will lead to a positional or rotational error during one of the
        // next ticks, so the sync will happen anyway, just one or two ticks later.
        [HarmonyPatch(typeof(Client), "ReconcileServerPlayerState")]
        private class MPSkipClientResimIfNotNecessary_ReconcileServerPlayerState
        {
            private static bool Prefix(Player player, PlayerState[] ___m_player_state_history)
            {
                if (Client.m_PendingPlayerStateMessages.Count < 1) {
                    // nothing to do, we can skip the original as it doesn't do anything anyway
                    return false;
                }
                // the original ReconcileServerPlayerState removes all elements from the queue
                // and uses the last one, if there is one.
                // We remove all but the last one, and only peek at that
                while(Client.m_PendingPlayerStateMessages.Count > 1) {
                    Client.m_PendingPlayerStateMessages.Dequeue();
                }
                PlayerStateToClientMessage msg = Client.m_PendingPlayerStateMessages.Peek();
                if (msg.m_tick < Client.m_tick)
                {
                    PlayerState s = ___m_player_state_history[msg.m_tick & 1023];
                    if (s != null) {
                        float err_distsqr = (msg.m_player_pos - s.m_pos).sqrMagnitude;
                        float err_angle = Mathf.Abs(Quaternion.Angle(msg.m_player_rot, s.m_rot));
                        bool skip = (err_distsqr < 0.0004f) && (err_angle < 0.5f);
                        if (skip) {
                            // we are skipping the resimulation, consume the message right here
                            if (Client.m_last_acknowledged_tick < msg.m_tick) {
                                Client.m_last_acknowledged_tick = msg.m_tick;
                            }

                            player.c_player_ship.m_boost_heat = msg.m_boost_heat;
                            player.c_player_ship.m_boost_overheat_timer = msg.m_boost_overheat_timer;

                            Client.m_PendingPlayerStateMessages.Dequeue();
                            return false;
                        }
                    }
                }
                return true;
            }
        }
    }
}
