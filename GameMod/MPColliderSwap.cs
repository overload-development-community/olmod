using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod
{
    public static class MPWeaponFirePoints
    {
        public static Vector2[] FIRING_POINTS;

        public static Vector2[] PYRO_POINTS = new Vector2[8]
        {
            // Final Z-component comes from Vector2.x, final X-component comes from Vector2.y

            new Vector2(-1.23f, 1.55f),     // Impulse
            new Vector2(-1.275f, 1.5f),     // Cyclone
            new Vector2(-1.23f, 1.55f),     // Reflex
            new Vector2(-1.23f, 1.55f),     // Crusher
            new Vector2(-1.17f, 1.8f),      // Driller
            new Vector2(-1.23f, 1.55f),     // Flak
            new Vector2(-1.23f, 1.55f),     // Thunderbolt
            new Vector2(-1.23f, 1.55f)      // Lancer
        };

        public static Vector3 muzzle_leftQ = new Vector3(-2.5f, -1.68f, 2.3f);
        public static Vector3 muzzle_rightQ = new Vector3(2.5f, -1.68f, 2.3f);

        public static float leftQdiffX;
        public static float leftQdiffY;
        public static float leftQdiffZ;

        public static float rightQdiffX;
        public static float rightQdiffY;
        public static float rightQdiffZ;
    }

    public static class MPColliderSwap
    {
        public static int selectedCollider = 0;

        // there are 3 scales available in the assetbundle, 100 is same as ship, 105 and 110 are scaled up copies of the mesh
        //public static string[] meshName = { "PlayershipCollider-100", "PlayershipCollider-105", "PlayershipCollider-110" };
        public static string[] meshName = { "PyroCollider-100", "PyroCollider-105", "PyroCollider-110" };
        public static string subName = "Pyro";
        public static string blankName = "blankmesh";

        public static bool FullPyro = false;
        public static bool visualizeMe = false;

        public static GameObject[] m_prefabs = new GameObject[3];
        public static GameObject m_sub;
        public static GameObject m_blank;
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
                    Debug.Log("CCF collider loaded: " + m_prefabs[i].name);
                    m_prefabs[i].GetComponent<MeshRenderer>().enabled = false;
                    Object.DontDestroyOnLoad(m_prefabs[i]); // they keep despawning and causing NullReferences unless this is here. Grrr.
                }
                m_sub = Object.Instantiate(ab.LoadAsset<GameObject>(subName));
                m_blank = Object.Instantiate(ab.LoadAsset<GameObject>(blankName));
                //Object.DontDestroyOnLoad(m_sub); don't want it actually appearing...
                MakeSubPrefab();
                ab.Unload(false);
            }
            loaded = true;
        }

        // because apparently this is the *efficient* way to do this rather than just, you know... replacing it in the asset files. Sure. Thanks, Unity.
        public static void MakeSubPrefab()
        {
            /*SubPrefab = (GameObject)Resources.Load("entity_special_player_ship"); // apparently instantiating it doesn't do the trick.
            if (SubPrefab == null)
            {
                Debug.LogErrorFormat("Could not create duplicate player prefab from {0}", "entity_special_player_ship");
            }

            OriginalPrefab = NetworkSpawnPlayer.m_player_prefab;

            PlayerShip ps = SubPrefab.GetComponent<PlayerShip>();*/
            PlayerShip ps = NetworkSpawnPlayer.m_player_prefab.GetComponent<PlayerShip>();
            Transform ts = ps.c_external_ship.transform;
            for (int i = 1; i < ts.childCount; i++)
            {
                Debug.Log("CCF disabling renderer for " + ts.GetChild(i).gameObject.name);
                ts.GetChild(i).gameObject.SetActive(false);
                ts.GetChild(i).gameObject.GetComponent<MeshRenderer>().enabled = false;
            }
            GameObject body = ts.GetChild(0).gameObject;
            GameObject blank = ts.GetChild(1).gameObject;

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

            body.GetComponent<MeshFilter>().sharedMesh = m_sub.GetComponent<MeshFilter>().sharedMesh;
            body.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            body.transform.localPosition = new Vector3(0f, -0.08f, 1f);
            body.transform.localScale = new Vector3(1.6f, 1.6f, 1.6f);

            blank.GetComponent<MeshFilter>().sharedMesh = m_blank.GetComponent<MeshFilter>().sharedMesh;
            blank.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            blank.transform.localPosition = new Vector3(0f, 0, 0f);
            blank.transform.localScale = new Vector3(1f, 1f, 1f);

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

            ps.c_spawn_effect_mf = new MeshFilter[8];
            ps.c_spawn_effect_mf[7] = body.GetComponent<MeshFilter>();
            // important ones start at 7, apparently, and rather than transpiling, this is easier.
            ps.c_spawn_effect_mf[0] = blank.GetComponent<MeshFilter>();
            ps.c_spawn_effect_mf[1] = ps.c_spawn_effect_mf[0];
            ps.c_spawn_effect_mf[2] = ps.c_spawn_effect_mf[0];
            ps.c_spawn_effect_mf[3] = ps.c_spawn_effect_mf[0];
            ps.c_spawn_effect_mf[4] = ps.c_spawn_effect_mf[0];
            ps.c_spawn_effect_mf[5] = ps.c_spawn_effect_mf[0];
            ps.c_spawn_effect_mf[6] = ps.c_spawn_effect_mf[0];
            //Debug.Log("CCF Effect is " + ps.c_spawn_effect_mf[7].gameObject.name);

            ps.m_weapon_mounts1[0].transform.parent.gameObject.SetActive(false); // ugh
            Debug.Log("CCF disabling object for " + ps.m_weapon_mounts1[0].transform.parent.gameObject.name);
            ps.m_weapon_mounts2[0].transform.parent.gameObject.SetActive(false);
            Debug.Log("CCF disabling object for " + ps.m_weapon_mounts2[0].transform.parent.gameObject.name);

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

            if (FullPyro) // move firepoints. Should not be used on a stock server.
            {
                MPWeaponFirePoints.FIRING_POINTS = MPWeaponFirePoints.PYRO_POINTS;

                ps.m_muzzle_center.localPosition = new Vector3(0f, -1.25f, 2.5f); // center weapon firepoint
                ps.m_muzzle_center2.localPosition = new Vector3(0f, -1.25f, 2.5f); // center missile firepoint

                ps.m_muzzle_left.localPosition = new Vector3(-1.512f, -1.23f, 1.55f); // left weapon firepoint
                ps.m_muzzle_right.localPosition = new Vector3(1.512f, -1.23f, 1.55f); // right weapon firepoint

                // and now... the quadfire points, which are way more difficult for Reasons.
                MPWeaponFirePoints.leftQdiffX = (MPWeaponFirePoints.muzzle_leftQ.x - ps.m_muzzle_left.localPosition.x) * ps.m_muzzle_left.parent.lossyScale.x; // -2.5 - -1.512 = -0.988  --> * 0.4f;
                MPWeaponFirePoints.leftQdiffY = (MPWeaponFirePoints.muzzle_leftQ.y - ps.m_muzzle_left.localPosition.y) * ps.m_muzzle_left.parent.lossyScale.y; // -1.23 - -1.68 = -0.45
                MPWeaponFirePoints.leftQdiffZ = (MPWeaponFirePoints.muzzle_leftQ.z - ps.m_muzzle_left.localPosition.z) * ps.m_muzzle_left.parent.lossyScale.z;

                MPWeaponFirePoints.rightQdiffX = (MPWeaponFirePoints.muzzle_rightQ.x - ps.m_muzzle_right.localPosition.x) * ps.m_muzzle_right.parent.lossyScale.x; // 2.5 - 1.512 = 0.988
                MPWeaponFirePoints.rightQdiffY = (MPWeaponFirePoints.muzzle_rightQ.y - ps.m_muzzle_right.localPosition.y) * ps.m_muzzle_right.parent.lossyScale.y;
                MPWeaponFirePoints.rightQdiffZ = (MPWeaponFirePoints.muzzle_rightQ.z - ps.m_muzzle_right.localPosition.z) * ps.m_muzzle_right.parent.lossyScale.z;
            }
            else
            {
                // keep the stock firepoints. Doesn't mess with stock server for pubs.
                MPWeaponFirePoints.FIRING_POINTS = PlayerShip.FIRING_POINTS;

                // Thunderbolt readjust -- doesn't work here unfortunately, since the Vector2 x is actually y
                //MPWeaponFirePoints.FIRING_POINTS[6].x = ps.m_muzzle_right.localPosition.x - ( MPWeaponBehavior.Thunderbolt.m_muzzle_adjust / ps.m_muzzle_left.parent.lossyScale.x);

                MPWeaponFirePoints.leftQdiffX = -0.25f;
                MPWeaponFirePoints.leftQdiffY = -0.15f;
                MPWeaponFirePoints.leftQdiffZ = -0.3f;

                MPWeaponFirePoints.rightQdiffX = 0.25f;
                MPWeaponFirePoints.rightQdiffY = -0.15f;
                MPWeaponFirePoints.rightQdiffZ = -0.3f;
            }

            //Debug.Log("CCF setting prefab");
            //Debug.Log("CCF " + NetworkSpawnPlayer.m_player_prefab.name);
            //NetworkSpawnPlayer.m_player_prefab = SubPrefab;
            //Debug.Log("CCF " + NetworkSpawnPlayer.m_player_prefab.name);
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "SwitchVisibleWeapon")]
    internal class MPColliderSwap_PlayerShip_SwitchVisibleWeapon
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldsfld && code.operand == AccessTools.Field(typeof(PlayerShip), "FIRING_POINTS"))
                {
                    code.operand = AccessTools.Field(typeof(MPWeaponFirePoints), "FIRING_POINTS");
                }
                yield return code;
            }
        }
    }

    // moves the quad shot positions slightly. Not worth the amount of code it took.
    [HarmonyPatch(typeof(PlayerShip), "MaybeFireWeapon")]
    internal class MPColliderSwap_PlayerShip_MaybeFireWeapon
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;
            int count = 0;
            foreach (var code in codes)
            {
                if (count == 2)
                {
                    if (code.opcode == OpCodes.Ldc_R4 && state < 3)
                    {
                        switch ((float)code.operand)
                        {
                            case 0.25f:
                                yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MPWeaponFirePoints), "rightQdiffX"));
                                state++;
                                break;
                            case -0.15f:
                                yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MPWeaponFirePoints), "rightQdiffY"));
                                state++;
                                break;
                            case -0.3f:
                                yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MPWeaponFirePoints), "rightQdiffZ"));
                                state++;
                                count++; //we're done here
                                break;
                            default:
                                yield return code;
                                break;
                        }
                    }
                    else
                    {
                        yield return code;
                    }
                }
                else if (count == 5)
                {
                    if (code.opcode == OpCodes.Ldc_R4 && state < 6)
                    {
                        switch ((float)code.operand)
                        {
                            case -0.25f:
                                yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MPWeaponFirePoints), "leftQdiffX"));
                                state++;
                                break;
                            case -0.15f:
                                yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MPWeaponFirePoints), "leftQdiffY"));
                                state++;
                                break;
                            case -0.3f:
                                yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MPWeaponFirePoints), "leftQdiffZ"));
                                state++;
                                count++; // we're done here
                                break;
                            default:
                                yield return code;
                                break;
                        }
                    }
                    else
                    {
                        yield return code;
                    }
                }
                else if (code.opcode == OpCodes.Ldfld && code.operand == AccessTools.Field(typeof(PlayerShip), "m_muzzle_right"))
                {
                    count++;
                    yield return code;
                }
                else if (code.opcode == OpCodes.Ldfld && code.operand == AccessTools.Field(typeof(PlayerShip), "m_muzzle_left"))
                {
                    count++;
                    yield return code;
                }
                else
                {
                    yield return code;
                }
            }
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

    /*[HarmonyPatch(typeof(PlayerShip), "InitMaterials")]
    internal class MPColliderSwap_PlayerShip_InitMaterials
    {
        static void Prefix()
        {
            Debug.Log("CCF InitMaterials called");
        }
    }

    [HarmonyPatch(typeof(Client), "OnRespawnMsg")]
    internal class MPColliderSwap_Client_OnRespawnMsg
    {
        static void Postfix(NetworkMessage msg)
        {
            msg.reader.SeekZero();
            RespawnMessage respawnMessage = msg.ReadMessage<RespawnMessage>();
            Debug.Log("CCF OnRespawnMsg called for LobbyID " + respawnMessage.lobby_id);
        }
    }

    [HarmonyPatch(typeof(Client), "OnSetLoadout")]
    internal class MPColliderSwap_Client_OnSetLoadout
    {
        static void Postfix(NetworkMessage msg)
        {
            msg.reader.SeekZero();
            LoadoutDataMessage loadoutDataMessage = msg.ReadMessage<LoadoutDataMessage>();
            Debug.Log("CCF OnSetLoadout called for LobbyID " + loadoutDataMessage.lobby_id + " setting custom decal ID to " + loadoutDataMessage.mpc_decal_pattern + ", color to " + loadoutDataMessage.mpc_decal_color);
        }
    }

    [HarmonyPatch(typeof(Client), "SendPlayerLoadoutToServer")]
    internal class MPColliderSwap_Client_SendPlayerLoadoutToServer
    {
        static void Postfix()
        {
            Debug.Log("CCF SendPlayerLoadoutToServer called for LobbyID " + NetworkMatch.m_my_lobby_id + " setting custom decal ID to " + MenuManager.mpc_decal_pattern + ", color to " + MenuManager.mpc_decal_color);
        }
    }

    [HarmonyPatch(typeof(NetworkSpawnPlayer), "SetMultiplayerCustomization")]
    internal class MPColliderSwap_NetworkSpawnPlayer_SetMultiplayerCustomization
    {
        static void Prefix(LoadoutDataMessage loadout_data)
        {
            Debug.Log("CCF LobbyID " + loadout_data.lobby_id + " setting custom decal ID to " + loadout_data.mpc_decal_pattern + ", color to " + loadout_data.mpc_decal_color);
        }
    }*/

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
                Debug.Log("CCF about to instantiate collider " + (MPColliderSwap.selectedCollider - 1) + ", assets are loaded: " + MPColliderSwap.loaded + ", assetname is " + MPColliderSwap.m_prefabs[MPColliderSwap.selectedCollider - 1].name);
                GameObject go = Object.Instantiate(MPColliderSwap.m_prefabs[MPColliderSwap.selectedCollider - 1]); // -1 since 0 is "Stock"
                Debug.Log("CCF collider instantiated");

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
