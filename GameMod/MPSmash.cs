using System.Reflection;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod {
    public class MPSmash {
        public static bool Enabled = false;
    }

    /// <summary>
    /// Allow smash on server.
    /// </summary>
    [HarmonyPatch(typeof(PlayerShip), "TryChargeAttackStart")]
    class MPSmash_TryChargeAttackStart {
        private static FieldInfo _PlayerShip_m_refire_charge_time_Field = typeof(PlayerShip).GetField("m_refire_charge_time", BindingFlags.NonPublic | BindingFlags.Instance);
        private static MethodInfo _PlayerShip_FindEnemyTarget_Method = AccessTools.Method(typeof(PlayerShip), "FindEnemyTarget");

        private static bool Prefix(PlayerShip __instance) {
            if (!MPSmash.Enabled) {
                return true;
            }

            if ((float)_PlayerShip_m_refire_charge_time_Field.GetValue(__instance) <= 0f && __instance.m_wheel_select_state == WheelSelectState.NONE && __instance.c_player.m_energy >= 10f && __instance.m_boost_overheat_timer <= 0f) {
                _PlayerShip_FindEnemyTarget_Method.Invoke(__instance, null);
                var chargeTime = __instance.m_charge_target ? 0.45f : 0.1f;
                __instance.CallCmdDoChargeAttackStart(chargeTime);
                __instance.m_charge_timer = chargeTime;
                __instance.c_player.UseEnergy(10f);
                __instance.m_boost_heat += 0.3f;
                Client.GetClient().Send(MessageTypes.MsgPlayerSyncResource, new PlayerSyncResourceMessage {
                    m_player_id = __instance.c_player.netId,
                    m_type = PlayerSyncResourceMessage.ValueType.ENERGY,
                    m_value = __instance.c_player.m_energy
                });
            }

            return false;
        }
    }

    /// <summary>
    /// Check for collision and do damage if necessary.
    /// </summary>
    [HarmonyPatch(typeof(PlayerShip), "ProcessCollision")]
    class MPSmash_ProcessCollision {
        private static MethodInfo _PlayerShip_CalculateChargeAttackDamage_Method = AccessTools.Method(typeof(PlayerShip), "CalculateChargeAttackDamage");

        private static void Prefix(PlayerShip __instance, Collision collision) {
            if (!MPSmash.Enabled) {
                return;
            }

            if (collision.collider.gameObject == null || NetworkSim.m_resimulating || !GameplayManager.IsMultiplayer || !NetworkManager.IsServer()) {
                return;
            }

            switch (collision.collider.gameObject.layer) {
                case 9:
                    var opponent = collision.collider.GetComponent<PlayerShip>();
                    if (opponent != null && __instance.m_charge_timer > 0f && __instance.c_rigidbody.velocity.magnitude > 2f && __instance.m_boosting) {
                        var di = new DamageInfo {
                            damage = (float)_PlayerShip_CalculateChargeAttackDamage_Method.Invoke(__instance, null),
                            owner = __instance.gameObject,
                            pos = __instance.transform.position,
                            type = DamageType.PLAYER_CHARGE,
                            weapon = ProjPrefab.none
                        };
                        opponent.ApplyDamage(di);

                        __instance.CallRpcDoChargeAttackFinish();
                        __instance.m_charge_timer = 0f;
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Don't run this.
    /// </summary>
    [HarmonyPatch(typeof(PlayerShip), "MaybeDoChargeAttackFinish")]
    class MPSmash_MaybeDoChargeAttackFinish {
        private static bool Prefix() {
            return !MPSmash.Enabled;
        }
    }
}
