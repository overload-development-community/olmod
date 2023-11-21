using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod
{
    // gotta go fast
    public class Phoenix : Ship
    {
        public Phoenix()
        {
            displayName = "Phoenix Interceptor";
            name = "entity_special_player_phoenix";
            description = new string[3]
            {
                "The Phoenix is a relatively new design intended to be able to reliably chase down (or out-run)",
                "any of its competitors. This engine performance comes at a cost - the Phoenix relies upon",
                "pilot reflexes to avoid taking crippling damage rather than reinforced shielding."
            };

            meshName = "Phoenix";
            colliderNames = new string[3] { "PhoenixCollider-100", "PhoenixCollider-105", "PhoenixCollider-110" };

            extras = new GameObject[4];
            extraNames = new string[] { "PhoenixThruster", "PhoenixTopThruster", "PhoenixFrontThruster", "PhoenixBottomThruster" };

            customizations = false;

            FIRING_POINTS = new Vector3[9]
            {
                new Vector3(1.62f, -0.14f, -0.03f),      // Impulse
                new Vector3(0f, -1.15f, 0.26f),          // Cyclone
                new Vector3(1.62f, -0.14f, -0.03f),      // Reflex
                new Vector3(2.51f, -1.35f, 0.44f),       // Crusher
                new Vector3(0f, -1.0f, 0.57f),           // Driller
                new Vector3(1.62f, -0.14f, -0.03f),      // Flak
                new Vector3(1.62f, -0.14f, -0.03f),      // Thunderbolt
                new Vector3(2.51f, -1.35f, 0.44f),       // Lancer
                new Vector3(2.51f, -1.35f, 0.44f)        // Quad Impulse firepoint
            };

            ShieldMultiplier = 0.9f;

            AccelMulti = 0.85f;
            MoveMulti = 1.05f;
            TurnMulti = 0.8f;

            //boostMulti = 1.75f;
            //boostMod = 1.95f;

            boostMulti = 1.85f;
            boostMod = 2.15f;

            boostBurst = 0.3f;

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

            // Swap the rear thruster mesh with our custom one
            MeshFilter mf = ps.m_thruster_trans[1].gameObject.GetComponent<MeshFilter>();
            mf.mesh = extras[0].GetComponent<MeshFilter>().sharedMesh;
            ps.m_thruster_trans[1].localRotation = Quaternion.Euler(180f, 0f, 0f);
            ps.m_thruster_trans[1].localScale = new Vector3(1.6f, 1.6f, 1.6f);
            ps.m_thruster_trans[1].localPosition = new Vector3(0f, 0.463f, -0.465f);

            // top thruster
            mf = ps.m_thruster_trans[0].gameObject.GetComponent<MeshFilter>();
            mf.mesh = extras[1].GetComponent<MeshFilter>().sharedMesh;
            ps.m_thruster_trans[0].localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ps.m_thruster_trans[0].localScale = new Vector3(1.6f, 1.6f, 1.6f);
            ps.m_thruster_trans[0].localPosition = new Vector3(0f, 0.34f, 0.14f);

            // front thruster
            mf = ps.m_thruster_trans[2].gameObject.GetComponent<MeshFilter>();
            mf.mesh = extras[2].GetComponent<MeshFilter>().sharedMesh;
            ps.m_thruster_trans[2].localRotation = Quaternion.Euler(0f, 0f, 0f);
            ps.m_thruster_trans[2].localScale = new Vector3(1.6f, 1.6f, 1.6f);
            ps.m_thruster_trans[2].localPosition = new Vector3(0f, 0.38f, 0.33f);

            // bottom thruster
            mf = ps.m_thruster_trans[3].gameObject.GetComponent<MeshFilter>();
            mf.mesh = extras[3].GetComponent<MeshFilter>().sharedMesh;
            ps.m_thruster_trans[3].localRotation = Quaternion.Euler(90f, 0f, 0f);
            ps.m_thruster_trans[3].localScale = new Vector3(1.6f, 1.6f, 1.6f);
            ps.m_thruster_trans[3].localPosition = new Vector3(0f, -0.21f, 0.471f);

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
            ts.localPosition = new Vector3(-0.95f, 0.77f, -0.68f);
            ts.gameObject.SetActive(false);

            // Same with the right mesh locations
            ts = ps.c_cockpit.transform.GetChild(2); // right weapon mount
            ts.localRotation = Quaternion.Euler(0f, 0f, -36.6f);
            ts.localPosition = new Vector3(0.95f, 0.77f, -0.68f);
            ts.gameObject.SetActive(false);

            // Move but don't hide the center weapon mesh locations so they stay correctly underslung
            ts = ps.c_cockpit.transform.GetChild(3); // center weapon mount
            ts.localRotation = Quaternion.Euler(0f, 0f, 180f);
            ts.localPosition = new Vector3(0f, 0.46f, 0.52f);

            // move the actual firepoints as well. Should not be used on a stock server.
            ps.m_muzzle_center.localPosition = new Vector3(0f, -1.12f, 1.2f); // center weapon firepoint
            ps.m_muzzle_center2.localPosition = new Vector3(0f, -1.12f, 1.2f); // center missile firepoint

            ps.m_muzzle_left.localPosition = new Vector3(-1.62f, -0.14f, -0.03f); // left weapon firepoint
            ps.m_muzzle_right.localPosition = new Vector3(1.62f, -0.14f, -0.03f); // right weapon firepoint

            ps.m_muzzle_left2.localPosition = new Vector3(-1.25f, -1.04f, 1f); // left missile firepoint
            ps.m_muzzle_right2.localPosition = new Vector3(1.25f, -1.04f, 1f); // right missile firepoint

            // move the lights
            ts = ps.c_lights[0].transform.parent; // root lights object, which for some reason is like 10 units off-center
            ts.localPosition = Vector3.zero;
            ts.localRotation = Quaternion.Euler(0f, 0f, 0f);
            ts = ps.c_lights[0].transform; // right light
            ts.localPosition = new Vector3(0.56f, -0.19f, 0.52f);
            ts.localRotation = Quaternion.Euler(9.7f, 18.8f, 0f);
            ts = ps.c_lights[1].transform; // left light
            ts.localPosition = new Vector3(-0.56f, -0.19f, 0.52f);
            ts.localRotation = Quaternion.Euler(9.7f, -18.8f, 0f);
            ts = ps.c_lights[2].transform; // far light
            ts.localPosition = new Vector3(0f, -0.31f, 0.95f);
            ts.localRotation = Quaternion.Euler(0f, 0f, 0f);
            ts = ps.c_lights[3].transform; // boost light
            ts.localPosition = new Vector3(0f, 0.036f, -0.92f);
            ts.localRotation = Quaternion.Euler(0f, 0f, 0f);
            ts = ps.c_lights[4].transform; // thunderbolt light
            ts.localPosition = new Vector3(0f, 0.18f, -0.25f);
            ts.localRotation = Quaternion.Euler(0f, 0f, 0f);

            base.ApplyParameters(go);
        }
    }
}
