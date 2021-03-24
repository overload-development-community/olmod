using System.Reflection;
using Harmony;
using Overload;
using UnityEngine;

namespace GameMod {
    public class MPSmash {
        public static bool Enabled = false;
    }

    [HarmonyPatch(typeof(PlayerShip), "TryChargeAttackStart")]
    class MPSmash_TryChargeAttackStart {
        private static FieldInfo _PlayerShip_m_refire_charge_time_Field = typeof(PlayerShip).GetField("m_refire_charge_time", BindingFlags.NonPublic | BindingFlags.Instance);
        private static MethodInfo _PlayerShip_FindEnemyTarget_Method = AccessTools.Method(typeof(PlayerShip), "FindEnemyTarget");

        private static bool Prefix(PlayerShip __instance) {
            if (!MPSmash.Enabled) {
                return true;
            }

            if ((float)_PlayerShip_m_refire_charge_time_Field.GetValue(__instance) <= 0f && __instance.m_wheel_select_state == WheelSelectState.NONE && __instance.c_player.m_energy >= 10f) {
                _PlayerShip_FindEnemyTarget_Method.Invoke(__instance, null);
                var chargeTime = (!__instance.m_charge_target) ? 0.1f : 0.45f;
                __instance.CallCmdDoChargeAttackStart(chargeTime);
                __instance.m_charge_timer = chargeTime;
                __instance.c_player.UseEnergy(10f);
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "ProcessCollision")]
    class MPSmash_ProcessCollision {
        private static MethodInfo _PlayerShip_CalculateChargeAttackDamage_Method = AccessTools.Method(typeof(PlayerShip), "CalculateChargeAttackDamage");
        private static MethodInfo _PlayerShip_MaybeDoChargeAttackFinish_Method = AccessTools.Method(typeof(PlayerShip), "MaybeDoChargeAttackFinish");

        private static void Prefix(PlayerShip __instance, Collision collision) {
            if (!MPSmash.Enabled) {
                return;
            }

            if (collision.collider.gameObject == null || NetworkSim.m_resimulating || !GameplayManager.IsMultiplayer || !NetworkManager.IsServer()) {
                return;
            }

            Debug.Log($"{collision.collider.gameObject.layer} - {__instance.m_charge_timer} - {__instance.c_rigidbody.velocity.magnitude} - {__instance.m_boosting}");

            switch (collision.collider.gameObject.layer) {
                case 9:
                    var opponent = collision.collider.GetComponent<PlayerShip>();
                    Debug.Log($"{(opponent == null ? "NULL" : "not null")}");
                    if (opponent != null) {
                        if (__instance.m_charge_timer > 0f && __instance.c_rigidbody.velocity.magnitude > 2f && __instance.m_boosting) {
                            var di = new DamageInfo {
                                damage = (float)_PlayerShip_CalculateChargeAttackDamage_Method.Invoke(__instance, null),
                                owner = __instance.gameObject,
                                pos = __instance.transform.position,
                                type = DamageType.PLAYER_CHARGE,
                                weapon = ProjPrefab.none
                            };
                            opponent.ApplyDamage(di);
                            Debug.Log($"Sent {di.damage} damage");
                        }
                        _PlayerShip_MaybeDoChargeAttackFinish_Method.Invoke(__instance, null);
                    }
                    break;
            }
        }
    }
}
