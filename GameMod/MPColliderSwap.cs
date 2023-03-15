using HarmonyLib;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod
{
    public static class MPColliderSwap
    {
        public static int selectedCollider = 0;

        public static bool visualizeMe = false; // For debugging. Turn this on to render the collider mesh.\

        public static void SwapCollider(PlayerShip ps)
        {
            if (!GameplayManager.IsMultiplayer)
                return;

            Debug.Log("CCF Swapping collider");

            if (selectedCollider != 0 && ps.c_mesh_collider.GetComponent<MeshCollider>() == null)
            {
                var mat_no_friction = ps.c_mesh_collider.sharedMaterial;
                Object.Destroy(ps.c_mesh_collider);
                //int id = GameplayManager.IsDedicatedServer() ? __instance.connectionToClient.connectionId : __instance.connectionToServer.connectionId;
                GameObject go = Object.Instantiate(MPShips.GetShip(ps).collider[selectedCollider - 1]); // -1 since 0 is the "Stock" sphere
                //GameObject go = Object.Instantiate(MPShips.SelectedShips[id].collider[MPColliderSwap.selectedCollider - 1]); // -1 since 0 is the "Stock" sphere

                if (visualizeMe && !ps.c_player.m_spectator && ps.netId != GameManager.m_local_player.c_player_ship.netId)
                {
                    MeshRenderer mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = UIManager.gm.m_energy_material;
                    mr.enabled = true;
                }

                go.layer = 16;
                var coll = go.GetComponent<MeshCollider>();
                coll.sharedMaterial = mat_no_friction;
                go.AddComponent<PlayerMeshCollider>().c_player = ps.c_player;

                ps.c_mesh_collider = coll;
                ps.c_mesh_collider_trans = ps.c_mesh_collider.transform;
                ps.c_flak_range_go.GetComponent<TriggerFlakRange>().player_collider = coll;
            }
        }
    }


    // replaces the original "PrepareForMP" method with one that can handle mesh vs. sphere colliders
    [HarmonyPatch(typeof(Player), "PrepareForMP")]
    static class MPPlayer_PrepareForMP_ColliderSwap
    {
        static bool Prefix(Player __instance)
        {
            if (__instance.c_player_ship != null)
            {
                __instance.c_player_ship.c_level_collider.enabled = false;
                __instance.c_player_ship.c_mesh_collider.enabled = false;
            }
            __instance.m_remote_player = true;
            __instance.m_remote_thrusters_active = false;
            __instance.m_server_tick = -1;
            NetworkIdentity component = __instance.GetComponent<NetworkIdentity>();
            if (component != null)
            {
                component.localPlayerAuthority = false;
            }
            if (__instance.c_player_ship.c_mesh_collider.GetType() == typeof(SphereCollider))
            {
                SphereCollider sphereCollider1 = (SphereCollider)__instance.c_player_ship.c_mesh_collider;
                sphereCollider1.radius = 1f;
            }

            return false;
        }
    }

    // adds rotation syncing to the collider since it didn't matter with the sphere previously
    // this also happens once below and twice over in MPClientExtrapolation (look for c_transform.localRotation)
    [HarmonyPatch(typeof(Player), "LerpRemotePlayer")]
    static class MPPlayer_LerpRemotePlayer_ColliderSwap
    {
        static void Postfix(Player __instance)
        {
            __instance.c_player_ship.c_mesh_collider_trans.localRotation = __instance.c_player_ship.c_transform.localRotation;
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "FixedUpdateProcessControlsInternal")]
    static class MPPlayerShip_FixedUpdateProcessControlsInternal_ColliderSwap
    {
        static void Postfix(PlayerShip __instance)
        {
            if (GameplayManager.IsMultiplayerActive && __instance.c_mesh_collider_trans != null && __instance.c_transform != null)
            {
                __instance.c_mesh_collider_trans.localRotation = __instance.c_transform.localRotation;
            }
        }
    }
}
