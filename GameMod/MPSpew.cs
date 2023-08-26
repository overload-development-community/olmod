using System.Reflection;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod {
    /// <summary>
    /// Reduce the number of spawns of lesser missiles.
    /// </summary>
    [HarmonyPatch(typeof(Item), "OnTriggerEnter")]
    class MPSpew_Item_OnTriggerEnter {
        private static MethodInfo _Item_ItemIsReachable_Method = typeof(Item).GetMethod("ItemIsReachable", BindingFlags.NonPublic | BindingFlags.Instance);
        private static MethodInfo _Item_PlayItemPickupFX_Method = typeof(Item).GetMethod("PlayItemPickupFX", BindingFlags.NonPublic | BindingFlags.Instance);

        static bool Prefix(Item __instance, Collider other) {
            //Debug.Log("CCF ITEM COLLIDED WITH - " + __instance.m_type.ToString() + " - m_amount = " + __instance.m_amount + " - on " + (NetworkManager.IsServer() ? "server" : "client"));
            
            // Needs to be multiplayer, on the server, not on a spawn point, and not a super pickup.  Bail otherwise.
            if (!GameplayManager.IsMultiplayerActive || !NetworkManager.IsServer() || __instance.m_spawn_point != -1 || __instance.m_super) {
                return true;
            }

            if (other.attachedRigidbody == null) {
                return true;
            }
            PlayerShip component = other.attachedRigidbody.GetComponent<PlayerShip>();
            if (component == null) {
                return true;
            }
            Player c_player = component.c_player;
            if (!(bool)_Item_ItemIsReachable_Method.Invoke(__instance, new object[] { other }) || component.m_dying) {
                return true;
            }

            bool flag = false;

            switch (__instance.m_type) {
                case ItemType.MISSILE_FALCON:
                    c_player.UnlockMissile(MissileType.FALCON);
                    flag = c_player.AddMissileAmmo(3, MissileType.FALCON, false, false);
                    break;
                case ItemType.MISSILE_HUNTER:
                    c_player.UnlockMissile(MissileType.HUNTER);
                    flag = c_player.AddMissileAmmo(2, MissileType.HUNTER, false, false);
                    break;
                case ItemType.MISSILE_CREEPER:
                    c_player.UnlockMissile(MissileType.CREEPER);
                    flag = c_player.AddMissileAmmo(6, MissileType.CREEPER, false, false);
                    break;
                case ItemType.MISSILE_POD:
                    c_player.UnlockMissile(MissileType.MISSILE_POD);
                    flag = c_player.AddMissileAmmo(10, MissileType.MISSILE_POD, false, false);
                    break;
                default:
                    return true;
            }

            GameplayManager.AddStatsPowerup(__instance.m_type, __instance.m_secret);

            if (flag) {
                if (__instance.m_secret) {
                    c_player.AddXP(2);
                }
                if (GameplayManager.IsMultiplayerActive) {
                    c_player.CallRpcPlayItemPickupFX(__instance.m_type, false);
                } else {
                    _Item_PlayItemPickupFX_Method.Invoke(__instance, new object[] { c_player });
                }
                foreach (ScriptBase item in __instance.triggered_on_pickup) {
                    item.SendMessage("ActivateScriptLink", null, SendMessageOptions.DontRequireReceiver);
                }
                RobotManager.RemoveItemFromList(__instance);
                Object.Destroy(__instance.c_go);
            } else {
                __instance.m_secret = false;
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "SpewItemsOnDeath")]
    class MPSpew_PlayerShip_SpewItemsOnDeath {
        static void Prefix(PlayerShip __instance) {
            if (!NetworkManager.IsServer() || MPShips.allowed != 0 && Random.value > 0.333f) // Lancer will only drop 2 out of 3 possible times
            //if (!NetworkManager.IsServer())
            {
                return;
            }

            __instance.c_player.m_weapon_picked_up[(int)WeaponType.LANCER] = false;
        }
    }
}
