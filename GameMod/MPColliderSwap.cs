using HarmonyLib;
using Overload;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod
{
    public static class MPColliderSwap
    {
        public static int selectedCollider = 0;

        // there are 3 scales available in the assetbundle, 100 is same as ship, 105 and 110 are scaled up copies of the mesh
        public static string[] meshName = { "PlayershipCollider-100", "PlayershipCollider-105", "PlayershipCollider-110" };
        public static string subName = "Pyro"; // :D Someday... It's *sorta* working.

        public static bool visualizeMe = false;

        public static GameObject[] m_prefabs = new GameObject[3];
        public static GameObject m_sub;
        public static GameObject OriginalPrefab;
        public static GameObject SubPrefab;

        // whether resources have been loaded yet
        public static bool loaded = false;

        // loads the prefabs from the embedded resource file
        public static void loadResources()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GameMod.Resources.playershipmeshcollider"))
            {
                var ab = AssetBundle.LoadFromStream(stream);
                for (int i = 0; i < 3; i++)
                {
                    m_prefabs[i] = Object.Instantiate(ab.LoadAsset<GameObject>(meshName[i]));
                    m_prefabs[i].GetComponent<MeshRenderer>().enabled = false;
                }
                m_sub = Object.Instantiate(ab.LoadAsset<GameObject>(subName));
                MakeSubPrefab();
                ab.Unload(false);
            }
            loaded = true;
        }

        // because apparently this is the *efficient* way to do this rather than just, you know... replacing it in the asset files. Sure. Thanks, Unity.
        public static void MakeSubPrefab()
        {
            SubPrefab = (GameObject)Resources.Load("entity_special_player_ship"); // apparently instantiating it doesn't do the trick.
            if (SubPrefab == null)
            {
                Debug.LogErrorFormat("Could not create duplicate player prefab from {0}", "entity_special_player_ship");
            }

            OriginalPrefab = NetworkSpawnPlayer.m_player_prefab;

            PlayerShip ps = SubPrefab.GetComponent<PlayerShip>();
            Transform ts = ps.c_external_ship.transform;
            for (int i = 1; i < ts.childCount; i++)
            {
                Debug.Log("CCF disabling renderer for " + ts.GetChild(i).gameObject.name);
                ts.GetChild(i).gameObject.SetActive(false);
                ts.GetChild(i).gameObject.GetComponent<MeshRenderer>().enabled = false;
            }
            GameObject body = ts.GetChild(0).gameObject;
            ts = ps.c_cockpit.transform.GetChild(0);
            for (int i = 1; i < ts.childCount; i++)
            {
                //ts.GetChild(i).gameObject.SetActive(false);
                Debug.Log("CCF disabling renderer for " + ts.GetChild(i).gameObject.name);
                ts.GetChild(i).gameObject.SetActive(false);
                ts.GetChild(i).gameObject.GetComponent<MeshRenderer>().enabled = false;
            }
            ts = ps.c_cockpit_light.transform;
            for (int i = 0; i < ts.childCount; i++)
            {
                Debug.Log("CCF disabling " + ts.GetChild(i).gameObject.name);
                ts.GetChild(i).gameObject.SetActive(false);
            }
            //body.GetComponent<MeshFilter>().sharedMesh.
            body.GetComponent<MeshFilter>().sharedMesh = m_sub.GetComponent<MeshFilter>().sharedMesh;
            //body.transform.parent = ts;
            //body.transform.
            body.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            body.transform.localPosition = new Vector3(0f, -0.08f, 1f);
            body.transform.localScale = new Vector3(1.6f, 1.6f, 1.6f);
            //temporary shader
            //Material mat = body.GetComponent<MeshRenderer>().sharedMaterial;
            //mat.shader = Shader.Find("Standard");
            //mat.enableInstancing = true;
            //mat.SetFloat("_Metallic", 0.2f);
            //mat.SetFloat("_internal_brightness", 0.1f);
            //body.GetComponent<MeshRenderer>().sharedMaterial = ts.GetChild(0).gameObject.GetComponent<MeshRenderer>().sharedMaterial;

            MeshFilter mf = ps.m_thruster_trans[1].gameObject.GetComponent<MeshFilter>();
            mf.sharedMesh = ps.m_thruster_trans[0].gameObject.GetComponent<MeshFilter>().sharedMesh;
            ps.m_thruster_trans[1].localRotation = Quaternion.Euler(180f, 0f, 90f);
            ps.m_thruster_trans[1].localScale = new Vector3(2.5f, 2.3f, 2.5f);
            ps.m_thruster_trans[1].localPosition = new Vector3(0f, 0.08f, -0.3f);

            ps.m_thruster_trans[0].localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ps.m_thruster_trans[0].localPosition = new Vector3(0f, 0.18f, 0.43f);

            ps.m_thruster_trans[2].localRotation = Quaternion.Euler(0f, 0f, 0f);
            ps.m_thruster_trans[2].localPosition = new Vector3(0f, -0.09f, 1.25f);

            ps.m_thruster_trans[3].localRotation = Quaternion.Euler(90f, 0f, 0f);
            ps.m_thruster_trans[3].localPosition = new Vector3(0f, -0.28f, 0.84f);

            ps.c_spawn_effect_mf = new MeshFilter[1];
            ps.c_spawn_effect_mf[0] = body.GetComponent<MeshFilter>();

            ps.m_weapon_mounts1[0].transform.parent.gameObject.SetActive(false); // ugh
            ps.m_weapon_mounts2[0].transform.parent.gameObject.SetActive(false);

            ts = ps.c_cockpit.transform.GetChild(1); // left weapon mount
            ts.localRotation = Quaternion.Euler(0f, 0f, 36.6f);
            ts.localPosition = new Vector3(-0.88f, 0.056f, 0.5f);
            ts.gameObject.SetActive(false);

            ts = ps.c_cockpit.transform.GetChild(2); // right weapon mount
            ts.localRotation = Quaternion.Euler(0f, 0f, -36.6f);
            ts.localPosition = new Vector3(0.88f, 0.056f, 0.5f);
            ts.gameObject.SetActive(false);

            ts = ps.c_cockpit.transform.GetChild(3); // center weapon mount
            ts.localRotation = Quaternion.Euler(0f, 0f, 180f);
            ts.localPosition = new Vector3(0f, 0.37f, 1.35f);

            Debug.Log("CCF setting prefab");
            Debug.Log("CCF " + NetworkSpawnPlayer.m_player_prefab.name);
            NetworkSpawnPlayer.m_player_prefab = SubPrefab;
            Debug.Log("CCF " + NetworkSpawnPlayer.m_player_prefab.name);
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "SetCustomBody")]
    internal class MPColliderSwap_PlayerShip_SetCustomBody
    {
        static bool Prefix()
        {
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "SetCustomWings")]
    internal class MPColliderSwap_PlayerShip_SetCustomWings
    {
        static bool Prefix()
        {
            return false;
        }
    }

    // replaces the mesh when the PlayerShip object is started, provided that an accurate mesh has been selected instead of the default
    [HarmonyPatch(typeof(PlayerShip), "Start")]
    internal class MPColliderSwap_PlayerShip_Start
    {
        static void Postfix(PlayerShip __instance)
        {
            if (!GameplayManager.IsMultiplayer)
                return;

            if (!MPColliderSwap.loaded) // this only needs to happen once
            {
                MPColliderSwap.loadResources();
            }

            if (MPColliderSwap.selectedCollider != 0 && __instance.c_mesh_collider.GetComponent<MeshCollider>() == null)
            {
                /*if (!MPColliderSwap.loaded) // this only needs to happen once
                {
                    MPColliderSwap.loadResources();
                }*/

                var mat_no_friction = __instance.c_mesh_collider.sharedMaterial;
                Object.Destroy(__instance.c_mesh_collider);
                GameObject go = Object.Instantiate(MPColliderSwap.m_prefabs[MPColliderSwap.selectedCollider - 1]); // -1 since 0 is "Stock"

                if (!__instance.c_player.m_spectator && __instance.netId != GameManager.m_local_player.c_player_ship.netId)
                {
                    if (MPColliderSwap.visualizeMe)
                    {
                        go.GetComponent<MeshRenderer>().sharedMaterial = UIManager.gm.m_energy_material;
                        go.GetComponent<MeshRenderer>().enabled = true;
                    }
                    else
                    {
                        go.GetComponent<MeshRenderer>().enabled = false;
                    }
                }
                else
                {
                    go.GetComponent<MeshRenderer>().enabled = false;
                }

                go.layer = 16;
                var coll = go.GetComponent<MeshCollider>();
                coll.sharedMaterial = mat_no_friction;
                go.AddComponent<PlayerMeshCollider>().c_player = __instance.c_player;

                __instance.c_mesh_collider = coll;
                __instance.c_mesh_collider_trans = __instance.c_mesh_collider.transform;
            }
        }
    }

    [HarmonyPatch(typeof(NetworkSpawnPlayer), "Init")]
    internal class NetworkSpawnPlayer_Init_ColliderSwap
    {
        public static void Postfix()
        {
            MPColliderSwap.loadResources();
        }
    }

    // replaces the original "PrepareForMP" method with one that can handle mesh vs. sphere colliders
    [HarmonyPatch(typeof(Player), "PrepareForMP")]
    internal class MPPlayer_PrepareForMP_ColliderSwap
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

                //Debug.Log("CCF - lol nope still a SphereCollider");
            }

            return false;
        }
    }

    // adds rotation syncing to the collider since it didn't matter with the sphere previously
    // this also happens once below and twice over in MPClientExtrapolation (look for c_transform.localRotation)
    [HarmonyPatch(typeof(Player), "LerpRemotePlayer")]
    internal class MPPlayer_LerpRemotePlayer_ColliderSwap
    {
        static void Postfix(Player __instance)
        {
            __instance.c_player_ship.c_mesh_collider_trans.localRotation = __instance.c_player_ship.c_transform.localRotation;
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "FixedUpdateProcessControlsInternal")]
    internal class MPPlayerShip_FixedUpdateProcessControlsInternal_ColliderSwap
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
