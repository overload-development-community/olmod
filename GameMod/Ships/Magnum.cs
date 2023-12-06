using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod
{
    // gotta go slow and bulky
    public class Magnum : Ship
    {
        Material detailMat; // Magnum looks dirtier than the others. Need some vents and grime that aren't on the Kodachi texture map.

        public Magnum()
        {
            displayName = "Magnum";
            name = "entity_special_player_magnum";
            description = new string[3]
            {
                "The Magnum sets aside the frivolities of stealth, speed, and aesthetics for the sole purpose of dishing",
                "out staggering amounts of damage on its prey. Its lack of maneuverabilty is made up for by it having",
                "the strongest shielding available and the unique (and highly inefficient) triple Thunderbolt configuration."
            };

            meshName = "Magnum";
            colliderNames = new string[3] { "MagnumCollider-100", "MagnumCollider-105", "MagnumCollider-110" };

            extras = new GameObject[5];
            extraNames = new string[] { "MagnumDetail", "MagnumThruster", "MagnumTopThruster", "MagnumFrontThruster", "MagnumBottomThruster" };

            //detailMat = Resources.Load<GameObject>("entity_enemy_HulkB").transform.GetChild(0).GetChild(0).GetChild(0).GetChild(2).GetChild(0).gameObject.GetComponent<MeshRenderer>().sharedMaterial; // yeesh, there *must* be a better way
            detailMat = Assets.materials["mat_hulkB_1"]; // THERE IS!
            //Debug.Log("CCF mat name is " + detailMat.name);

            customizations = false;

            FIRING_POINTS = new Vector3[9]
            {
                new Vector3(1.54f, -0.85f, 1.99f),      // Impulse
                new Vector3(0f, -1.55f, 2.49f),         // Cyclone
                new Vector3(1.54f, -0.85f, 1.99f),      // Reflex
                new Vector3(1.54f, -0.85f, 1.99f),      // Crusher
                new Vector3(0f, -1.46f, 2.82f),         // Driller
                new Vector3(1.54f, -0.85f, 1.99f),      // Flak
                new Vector3(1.37f, -0.27f, 1.65f),      // Thunderbolt
                new Vector3(1.54f, -0.85f, 1.99f),      // Lancer
                new Vector3(1.37f, -0.27f, 1.65f)       // Quad Impulse firepoint
            };

            // temporary triTB firepoint method
            TRIFIRE_POINT = new Vector3(0f, -1.6f, 2.93f);
            triTB = true;

            ShieldMultiplier = 1.25f;

            //MoveMulti = 0.85f;
            AccelMulti = 1.12f;
            MoveMulti = 0.82f;
            TurnMulti = 0.68f;

            //boostMulti = 1.8f;
            //boostMod = 2f;
            boostMulti = 2f;
            boostMod = 2.2f;

            boostBurst = 0.8f;
            //boostBurst = 0.9f;

            SetHandling();
        }

        public override void ApplyParameters(GameObject go)
        {
            ps = go.GetComponent<PlayerShip>();

            // Hide everything but the main body GameObject (and an addition object for the detail mesh)
            Transform ts = ps.c_external_ship.transform;
            for (int i = 2; i < ts.childCount; i++)
            {
                ts.GetChild(i).gameObject.SetActive(false);
                ts.GetChild(i).gameObject.GetComponent<MeshRenderer>().enabled = false;
            }
            GameObject body = ts.GetChild(0).gameObject;
            GameObject detail = ts.GetChild(1).gameObject;
            GameObject blank = ts.GetChild(2).gameObject;

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

            // Replace the detail mesh with our substitute and reposition it
            detail.GetComponent<MeshFilter>().mesh = extras[0].GetComponent<MeshFilter>().sharedMesh;
            detail.GetComponent<MeshRenderer>().material = detailMat;
            detail.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            detail.transform.localPosition = new Vector3(0f, -0.08f, 1f);
            detail.transform.localScale = new Vector3(1.6f, 1.6f, 1.6f);

            // Replace the third mesh with a single tiny triangle for use with the effects array
            // (otherwise the shader gets applied 8x over on the main mesh or shows up where it's not supposed to)
            blank.GetComponent<MeshFilter>().mesh = MPShips.m_blank.GetComponent<MeshFilter>().sharedMesh;
            blank.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            blank.transform.localPosition = new Vector3(0f, 0, 0f);
            blank.transform.localScale = new Vector3(1f, 1f, 1f);

            // Swap the rear thruster mesh with our custom one
            MeshFilter mf = ps.m_thruster_trans[1].gameObject.GetComponent<MeshFilter>();
            mf.mesh = extras[1].GetComponent<MeshFilter>().sharedMesh;
            ps.m_thruster_trans[1].localRotation = Quaternion.Euler(180f, 0f, 180f);
            ps.m_thruster_trans[1].localScale = new Vector3(1.6f, 1.6f, 1.6f);
            ps.m_thruster_trans[1].localPosition = new Vector3(0f, -0.038f, -1.09f);

            // top thruster
            mf = ps.m_thruster_trans[0].gameObject.GetComponent<MeshFilter>();
            mf.mesh = extras[2].GetComponent<MeshFilter>().sharedMesh;
            ps.m_thruster_trans[0].localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ps.m_thruster_trans[0].localScale = new Vector3(1.6f, 1.6f, 1.6f);
            ps.m_thruster_trans[0].localPosition = new Vector3(0f, 0.547f, 0.324f);

            // front thruster
            mf = ps.m_thruster_trans[2].gameObject.GetComponent<MeshFilter>();
            mf.mesh = extras[3].GetComponent<MeshFilter>().sharedMesh;
            ps.m_thruster_trans[2].localRotation = Quaternion.Euler(0f, 0f, 0f);
            ps.m_thruster_trans[2].localScale = new Vector3(1.6f, 1.6f, 1.6f);
            ps.m_thruster_trans[2].localPosition = new Vector3(0f, -0.233f, 3f);

            // bottom thruster
            mf = ps.m_thruster_trans[3].gameObject.GetComponent<MeshFilter>();
            mf.mesh = extras[4].GetComponent<MeshFilter>().sharedMesh;
            ps.m_thruster_trans[3].localRotation = Quaternion.Euler(90f, 0f, 0f);
            ps.m_thruster_trans[3].localScale = new Vector3(1.6f, 1.6f, 1.6f);
            ps.m_thruster_trans[3].localPosition = new Vector3(0f, -0.65f, 0.492f);

            // Empty and repopulate the effects array with the only meshes we care about
            ps.c_spawn_effect_mf = new MeshFilter[9];
            ps.c_spawn_effect_mf[7] = body.GetComponent<MeshFilter>();
            ps.c_spawn_effect_mf[8] = detail.GetComponent<MeshFilter>();

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
            ts.localRotation = Quaternion.Euler(0f, 0f, 72f);
            ts.localPosition = new Vector3(-0.72f, 0.79f, 0.7f);
            ts.gameObject.SetActive(false);

            // Same with the right mesh locations
            ts = ps.c_cockpit.transform.GetChild(2); // right weapon mount
            ts.localRotation = Quaternion.Euler(0f, 0f, -72f);
            ts.localPosition = new Vector3(0.72f, 0.79f, 0.7f);
            ts.gameObject.SetActive(false);

            // Move but don't hide the center weapon mesh locations so they stay correctly underslung
            ts = ps.c_cockpit.transform.GetChild(3); // center weapon mount
            ts.localRotation = Quaternion.Euler(0f, 0f, 180f);
            ts.localPosition = new Vector3(0f, 0.175f, 2.02f);

            // Add the center Thunderbolt cannon
            GameObject cTB = Object.Instantiate(ps.m_weapon_mounts1[(int)WeaponType.THUNDERBOLT], ts);
            ps.m_weapon_mounts1 = ps.m_weapon_mounts1.AddToArray(cTB);
            cTB.transform.localPosition = new Vector3(0f, 0f, -0.33f);

            // move the actual firepoints as well. Should not be used on a stock server.
            ps.m_muzzle_center.localPosition = new Vector3(0f, -1.3f, 3.55f); // center weapon firepoint
            ps.m_muzzle_center2.localPosition = new Vector3(0f, -1.76f, 1.68f); // center missile firepoint

            ps.m_muzzle_left.localPosition = new Vector3(-1.54f, -0.85f, 1.99f); // left weapon firepoint
            ps.m_muzzle_right.localPosition = new Vector3(1.54f, -0.85f, 1.99f); // right weapon firepoint

            ps.m_muzzle_left2.localPosition = new Vector3(-1.96f, -0.39f, 0.43f); // left missile firepoint
            ps.m_muzzle_right2.localPosition = new Vector3(1.96f, -0.39f, 0.43f); // right missile firepoint

            // move the lights
            ts = ps.c_lights[0].transform.parent; // root lights GameObject, which for some reason is like 10 units off-center
            ts.localPosition = Vector3.zero;
            ts.localRotation = Quaternion.Euler(0f, 0f, 0f);
            ts = ps.c_lights[1].transform; // right light
            ts.localPosition = new Vector3(0.42f, -0.2f, 0.65f);
            ts.localRotation = Quaternion.Euler(9.74f, 18.8f, 0f);
            ts = ps.c_lights[0].transform; // left light
            ts.localPosition = new Vector3(-0.42f, -0.2f, 0.65f);
            ts.localRotation = Quaternion.Euler(9.74f, -18.8f, 0f);
            ts = ps.c_lights[2].transform; // far light
            ts.localPosition = new Vector3(0.5f, -0.44f, 0.66f);
            ts.localRotation = Quaternion.Euler(-0.05f, -2.9f, 0.1f);
            ts = ps.c_lights[3].transform; // boost light
            ts.localPosition = new Vector3(0f, -0.18f, -1.1f);
            ts.localRotation = Quaternion.Euler(0f, 0f, 0f);
            ts = ps.c_lights[4].transform; // thunderbolt light
            ts.localPosition = new Vector3(0f, -0.95f, 0.52f);
            ts.localRotation = Quaternion.Euler(0f, 0f, 0f);

            base.ApplyParameters(go);
        }
    }
}
