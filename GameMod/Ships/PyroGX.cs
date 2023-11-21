using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod
{
    public class PyroGX : Ship
    {
        // In all its wingy glory
        public PyroGX()
        {
            displayName = "Pyro GX";
            name = "entity_special_player_pyro";
            description = new string[3]
            {
                "The Pyro GX is widely regarded as the gold standard in all-purpose fighter design.",
                "It packs a punch, it can take a hit, and it's both nimble and fast enough to fight",
                "well in enclosed environments and open spaces alike."
            };

            meshName = "Pyro";
            colliderNames = new string[3] { "PyroCollider-100", "PyroCollider-105", "PyroCollider-110" };

            extras = new GameObject[3];
            extraNames = new string[] { "PyroTopThruster", "PyroFrontThruster", "PyroBottomThruster" };

            customizations = false;

            FIRING_POINTS = new Vector3[9]
            {
            new Vector3(1.512f, -1.23f, 1.55f),      // Impulse
            new Vector3(0f, -1.275f, 1.5f),          // Cyclone
            new Vector3(1.512f, -1.23f, 1.55f),      // Reflex
            new Vector3(1.512f, -1.23f, 1.55f),      // Crusher
            new Vector3(0f, -1.17f, 1.8f),           // Driller
            new Vector3(1.512f, -1.23f, 1.55f),      // Flak
            new Vector3(1.512f, -1.23f, 1.55f),      // Thunderbolt
            new Vector3(1.512f, -1.23f, 1.55f),      // Lancer
            new Vector3(2.5f, -1.68f, 2.3f)          // Quad Impulse firepoint
            };

            ShieldMultiplier = 1.1f;

            MoveMulti = 0.95f;
            TurnMulti = 0.9f;

            //boostMulti = 1.65f;
            //boostMod = 1.85f;

            //boostBurst = 0.2f;

            boostMulti = 1.8f;
            boostMod = 2f;

            boostBurst = 0.5f;

            SetHandling();
        }

        public override void ApplyParameters(GameObject go)
        {
            ps = go.GetComponent<PlayerShip>();

            // Hide everything but the main body GameObject
            Transform ts = ps.c_external_ship.transform;
            for (int i = 1; i < ts.childCount; i++)
            {
                ts.GetChild(i).gameObject.SetActive(false);
                ts.GetChild(i).gameObject.GetComponent<MeshRenderer>().enabled = false;
            }
            GameObject body = ts.GetChild(0).gameObject;
            GameObject blank = ts.GetChild(1).gameObject;

            // Hide all internal cockpit components in a way that keeps them hidden when enabling/disabling cockpit
            ts = ps.c_cockpit.transform.GetChild(0);
            for (int i = 1; i < ts.childCount; i++)
            {
                ts.GetChild(i).gameObject.SetActive(false);
                ts.GetChild(i).gameObject.GetComponent<MeshRenderer>().enabled = false;
            }

            // Hide all internal cockpit lights
            ts = ps.c_cockpit_light.transform;
            for (int i = 0; i < ts.childCount; i++)
            {
                ts.GetChild(i).gameObject.SetActive(false);
            }

            // Replace the body main mesh with our substitute and reposition it
            body.GetComponent<MeshFilter>().mesh = mesh.GetComponent<MeshFilter>().sharedMesh;
            body.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            body.transform.localPosition = new Vector3(0f, -0.08f, 1f);
            body.transform.localScale = new Vector3(1.6f, 1.6f, 1.6f);

            // Replace the second mesh with a single tiny triangle for use with the effects array
            // (otherwise the shader gets applied 8x over on the main mesh or shows up where it's not supposed to)
            blank.GetComponent<MeshFilter>().mesh = MPShips.m_blank.GetComponent<MeshFilter>().sharedMesh;
            blank.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            blank.transform.localPosition = new Vector3(0f, 0, 0f);
            blank.transform.localScale = new Vector3(1f, 1f, 1f);

            // Swap the rear thruster mesh with one that's better shaped and reposition them all appropriately
            MeshFilter mf = ps.m_thruster_trans[1].gameObject.GetComponent<MeshFilter>();
            mf.mesh = ps.m_thruster_trans[0].gameObject.GetComponent<MeshFilter>().sharedMesh;
            ps.m_thruster_trans[1].localRotation = Quaternion.Euler(180f, 0f, 90f);
            ps.m_thruster_trans[1].localScale = new Vector3(2.5f, 2.3f, 2.5f);
            ps.m_thruster_trans[1].localPosition = new Vector3(0f, 0.08f, -0.3f);

            // top thruster
            mf = ps.m_thruster_trans[0].gameObject.GetComponent<MeshFilter>();
            mf.mesh = extras[0].GetComponent<MeshFilter>().sharedMesh;
            ps.m_thruster_trans[0].localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ps.m_thruster_trans[0].localScale = new Vector3(1.6f, 1.6f, 1.6f);
            ps.m_thruster_trans[0].localPosition = new Vector3(0f, 0.205f, 0.473f);

            // front thruster
            mf = ps.m_thruster_trans[2].gameObject.GetComponent<MeshFilter>();
            mf.mesh = extras[1].GetComponent<MeshFilter>().sharedMesh;
            ps.m_thruster_trans[2].localRotation = Quaternion.Euler(0f, 0f, 0f);
            ps.m_thruster_trans[2].localScale = new Vector3(1.6f, 1.6f, 1.6f);
            ps.m_thruster_trans[2].localPosition = new Vector3(0f, -0.21f, 1.22f);

            // bottom thruster
            mf = ps.m_thruster_trans[3].gameObject.GetComponent<MeshFilter>();
            mf.mesh = extras[2].GetComponent<MeshFilter>().sharedMesh;
            ps.m_thruster_trans[3].localRotation = Quaternion.Euler(90f, 0f, 0f);
            ps.m_thruster_trans[3].localScale = new Vector3(1.6f, 1.6f, 1.6f);
            ps.m_thruster_trans[3].localPosition = new Vector3(0f, -0.3f, 1.025f);

            // Empty and repopulate the effects array with the only mesh we care about
            ps.c_spawn_effect_mf = new MeshFilter[8];
            ps.c_spawn_effect_mf[7] = body.GetComponent<MeshFilter>();

            // Add that tiny triangle from earlier to the first 7 slots since the game needs them filled.
            // The important ones start at 7, and transpiling gets ugly if we ever want to have a swappable prefab
            ps.c_spawn_effect_mf[0] = blank.GetComponent<MeshFilter>();
            ps.c_spawn_effect_mf[1] = ps.c_spawn_effect_mf[0];
            ps.c_spawn_effect_mf[2] = ps.c_spawn_effect_mf[0];
            ps.c_spawn_effect_mf[3] = ps.c_spawn_effect_mf[0];
            ps.c_spawn_effect_mf[4] = ps.c_spawn_effect_mf[0];
            ps.c_spawn_effect_mf[5] = ps.c_spawn_effect_mf[0];
            ps.c_spawn_effect_mf[6] = ps.c_spawn_effect_mf[0];

            // Hide all the weapon meshes on the side mounts
            ps.m_weapon_mounts1[0].transform.parent.gameObject.SetActive(false);
            ps.m_weapon_mounts2[0].transform.parent.gameObject.SetActive(false);

            // Move the left weapon mesh locations so the Thunderbolt glow lines up right
            // (also in case we ever reskin them and want to reenable them)
            ts = ps.c_cockpit.transform.GetChild(1); // left weapon mount
            ts.localRotation = Quaternion.Euler(0f, 0f, 36.6f);
            ts.localPosition = new Vector3(-0.88f, 0.056f, 0.5f);
            ts.gameObject.SetActive(false);

            // Same with the right mesh locations
            ts = ps.c_cockpit.transform.GetChild(2); // right weapon mount
            ts.localRotation = Quaternion.Euler(0f, 0f, -36.6f);
            ts.localPosition = new Vector3(0.88f, 0.056f, 0.5f);
            ts.gameObject.SetActive(false);

            // Move but don't hide the center weapon mesh locations so they stay correctly underslung
            ts = ps.c_cockpit.transform.GetChild(3); // center weapon mount
            ts.localRotation = Quaternion.Euler(0f, 0f, 180f);
            ts.localPosition = new Vector3(0f, 0.37f, 1.35f);

            // move the actual firepoints as well. Should not be used on a stock server.
            ps.m_muzzle_center.localPosition = new Vector3(0f, -1.25f, 2.5f); // center weapon firepoint
            ps.m_muzzle_center2.localPosition = new Vector3(0f, -1.25f, 2.5f); // center missile firepoint

            ps.m_muzzle_left.localPosition = new Vector3(-1.512f, -1.23f, 1.55f); // left weapon firepoint
            ps.m_muzzle_right.localPosition = new Vector3(1.512f, -1.23f, 1.55f); // right weapon firepoint

            base.ApplyParameters(go);
        }
    }


    // ====================================================================
    //
    // ====================================================================
    // ====================================================================
    //
    // ====================================================================


    // A cosmetic-only version of the Pyro
    public class PyroGXCosmetic : PyroGX
    {
        public PyroGXCosmetic()
        {
            displayName = "Pyro GX (Cosmetic)";
            name = "entity_special_player_pyro_cosmetic";
            meshName = "Pyro";
            colliderNames = new string[3] { "PlayershipCollider-100", "PlayershipCollider-105", "PlayershipCollider-110" };

            customizations = false;

            // these are the Kodachi FIRING_POINTS
            FIRING_POINTS = new Vector3[9]
            {
                new Vector3(1.675f, -1.13f, 2.38f),      // Impulse
                new Vector3(0f, -2.08f, 2.08f),          // Cyclone
                new Vector3(1.675f, -1.06f, 2.27f),      // Reflex
                new Vector3(1.675f, -0.79f, 2.1f),       // Crusher
                new Vector3(0f, -1.88f, 2.48f),          // Driller
                new Vector3(1.675f, -0.79f, 2.18f),      // Flak
                //new Vector3(1.675f, -0.99f, 2.35f),    // Thunderbolt -- original spacing
                new Vector3(1.175f, -0.99f, 2.35f),      // Thunderbolt -- tighter spacing
                new Vector3(1.675f, -0.93f, 2.6f),       // Lancer
                new Vector3(2.3f, -1.505f, 1.63f)        // Quad Impulse firepoint
            };

            ShieldMultiplier = 1f;

            m_slide_force_mp_nonscaled = new float[4] { 25f, 26.25f, 26.25f, 26.25f };
            m_turn_speed_limit_acc = new float[5] { 2.3f, 3.2f, 4.5f, 6f, 100f };
            m_turn_speed_limit_rb = new float[5] { 2.5f, 3.2f, 4f, 5.2f, 100f };

            boostMulti = 1.65f;
            boostMod = 1.85f;

            SetHandling();
        }

        public override void ApplyParameters(GameObject go)
        {
            base.ApplyParameters(go);

            //ps = go.GetComponent<PlayerShip>();

            ps.m_muzzle_center.localPosition = new Vector3(0f, -1.926f, 2.485f); // center weapon firepoint back to the Kodachi's spot
            ps.m_muzzle_center2.localPosition = new Vector3(0f, -1.873f, 0f); // center missile firepoint back to the Kodachi's spot

            ps.m_muzzle_left.localPosition = new Vector3(-1.675f, -1.105f, 2.611f); // left weapon firepoint back to the Kodachi's spot
            ps.m_muzzle_right.localPosition = new Vector3(1.675f, -1.105f, 2.611f); // right weapon firepoint back to the Kodachi's spot

            // This is a little crude since we're 2 levels deep. Oh well.
            go.name = name;
            SetQuadFirepoints(); // gotta call it again because the first set of adjustments was for the full Pyro
        }
    }
}
