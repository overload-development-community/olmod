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
    // Handles asset replacement for ships (well, -ship-, currently)
    public static class MPShips
    {
        // public static Dictionary<NetworkInstanceId, int> Selected = new Dictionary<NetworkInstanceId, int>(); // not needed yet, but will be for multiple ships in a single round

        public static List<Ship> Ships = new List<Ship>();
        public static Ship selected; // which prefab is currently active
        private static int _idx = 0;

        public static int selected_idx
        {
            get { return _idx; }
            set
            {
                _idx = value;
                selected = Ships[value];
            }
        }

        public static GameObject m_blank; // a single tiny untextured triangle for blanking out unused prefab components

        public static bool loading = false; // set to true during the loading process

        public static void AddShips()
        {
            // ship prefabs are explicitly added here
            Debug.Log("Adding Kodachi ship definition");
            Ships.Add(new Kodachi()); // Don't mess with this one or you're gonna break stuff.
            Debug.Log("Adding Pyro ship definition");
            Ships.Add(new PyroGX());
            Debug.Log("Adding Phoenix ship definition");
            Ships.Add(new Phoenix());
            Debug.Log("Adding Pyro (Slow) ship definition");
            Ships.Add(new PyroGXSlow());
            Debug.Log("Adding Pyro (Cosmetic) ship definition");
            Ships.Add(new PyroGXCosmetic());

            SwitchPrefab(0); // early game stuff goes weird otherwise, it needs one of them to be referenced
        }

        public static void SwitchPrefab(int idx)
        {
            if (idx < Ships.Count)
            {
                selected_idx = idx;
                selected = Ships[idx];
                Debug.Log("Switching ship Prefabs to " + selected.displayName);
            }
            else
            {
                selected_idx = 0;
                selected = Ships[0];
                Debug.Log("Attempted to switch to a non-existent ship Prefab, using " + selected.displayName + " instead");
            }
        }

        public static void LoadResources()
        {
            loading = true;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GameMod.Resources.meshes"))
            {
                var ab = AssetBundle.LoadFromStream(stream);

                m_blank = Object.Instantiate(ab.LoadAsset<GameObject>("blankmesh")); // used for substituting sections of the ship that shouldn't be present but are expected to be in the effects array
                Object.DontDestroyOnLoad(m_blank); // they keep despawning and causing NullReferences unless this is here. Grrr.
                Debug.Log("Blank replacement mesh loaded");

                foreach (Ship s in Ships)
                {
                    if (s.meshName != null) // if it's null, we're planning to use using the Kodachi default mesh
                    {
                        s.mesh = Object.Instantiate(ab.LoadAsset<GameObject>(s.meshName));
                        Object.DontDestroyOnLoad(s.mesh);
                        Debug.Log("Prefab replacement mesh loaded: " + s.mesh.name);
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        s.collider[i] = Object.Instantiate(ab.LoadAsset<GameObject>(s.colliderNames[i]));
                        Object.DontDestroyOnLoad(s.collider[i]);
                        Debug.Log("MeshCollider loaded: " + s.collider[i].name);
                    }
                    for (int i = 0; i < s.extras.Length; i++)
                    {
                        s.extras[i] = Object.Instantiate(ab.LoadAsset<GameObject>(s.extraNames[i]));
                        Object.DontDestroyOnLoad(s.extras[i]);
                        Debug.Log("Extra mesh loaded: " + s.extras[i].name);
                    }
                }
                ab.Unload(false);
            }
            loading = false;
        }
    }

    // ====================================================================
    //
    //
    // ====================================================================
    // Ship Template
    // ====================================================================
    //
    //
    // ====================================================================

    public abstract class Ship
    {
        public string displayName; // what it will show up as in menus and whatnot
        public string name; // the GameObject's new name string (if needed)
        public string meshName; // the name of the mesh prefab -- if null, will be skipped (Kodachi comes to mind)
        public string[] colliderNames; // the 3 MeshCollider names
        public string[] extraNames; // names of extra meshes to load

        public GameObject mesh; // the replacement mesh (if needed)
        public GameObject[] collider = new GameObject[3];
        public GameObject[] extras = new GameObject[0]; // only used if needed

        public bool customizations; // whether or not to use the Kodachi's custom wing and body meshes.

        public Vector3[] FIRING_POINTS; // defines the LocalPosition of each primary weapon firepoint for this ship type. Must be 9 items in the array (see the Kodachi definition for an example)

        // quad impulse adjusted firepoints
        public float QdiffRightX;
        public float QdiffLeftX;
        public float QdiffY;
        public float QdiffZ;

        // ship scale factor (from Kodachi base of 1f)
        public float shipScale;

        // shield scaling factor -- inversely applied to shield pickups
        public float ShieldMultiplier;

        // movement speed restrictor
        public float[] m_slide_force_mp; // size 4 - We only care about the first 2 (1st is regular speed, 2nd is All-Way mod speed)

        // turn speed restrictors
        public float[] m_turn_speed_limit_acc; // size 5
        public float[] m_turn_speed_limit_rb; // size 5

        public float boostMulti; // speed multiplier for regular boost
        public float boostMod; // speed multiplier for the Enhanced Boost mod

        // This is where the ship customizations in each definition get applied to the instantiated GameObject.
        // Override this in each Ship and call base.ApplyParameters() at the end of the method.
        public virtual void ApplyParameters(GameObject go)
        {
            Debug.Log("Switching ship Prefabs to " + displayName);
            go.name = name;
            PlayerShip ps = go.GetComponent<PlayerShip>();

            //SetScale(go);
            SetQuadFirepoints(ps); // should really only fire once instead of this... but if the scale is changed, this needs an object to pull it from.
        }

        // finish and test this -- don't use yet. -- Functional, but ship turning is still whacky. Why?? Rigidbody should not be scaled with this the way it is currently. Something weird is going on.
        protected void SetScale(PlayerShip ps)
        {
            if (shipScale != 1f) // don't bother if we're not scaling
            {
                ps.c_main_ship_go.transform.localScale *= shipScale;
                ((SphereCollider)ps.c_level_collider).radius *= shipScale;

                for (int i = 0; i < 4; i++) // bring down the movement speeds too
                {
                    m_slide_force_mp[i] *= shipScale;
                }
            }
        }

        // translates the firepoint coordinates to world-scale adjustments like the game expects
        protected void SetQuadFirepoints(PlayerShip ps)
        {
            float scale = ps.m_muzzle_right.parent.lossyScale.x; // 0.4f, unless some interesting stuff is happening -- this is different from the shipScale field

            QdiffRightX = (FIRING_POINTS[8].x - FIRING_POINTS[0].x) * scale;
            QdiffLeftX = -1 * QdiffRightX;
            QdiffY = (FIRING_POINTS[8].y - FIRING_POINTS[0].y) * scale;
            QdiffZ = (FIRING_POINTS[8].z - FIRING_POINTS[0].z) * scale;
        }
    }

    // ====================================================================
    //
    //
    // ====================================================================
    // Ship Definitions
    // ====================================================================
    //
    //
    // ====================================================================

    // The Kodachi.
    public class Kodachi : Ship
    {
        public Kodachi()
        {
            displayName = "Kodachi Gunship";
            name = "entity_special_player_ship";
            meshName = null; // don't replace anything, we want the original
            colliderNames = new string[3] { "PlayershipCollider-100", "PlayershipCollider-105", "PlayershipCollider-110" };

            customizations = true;

            FIRING_POINTS = new Vector3[9]
            {
                new Vector3(1.675f, -1.13f, 2.38f),      // Impulse
                new Vector3(0f, -2.08f, 2.08f),          // Cyclone
                new Vector3(1.675f, -1.06f, 2.27f),      // Reflex
                new Vector3(1.675f, -0.79f, 2.1f),       // Crusher
                new Vector3(0f, -1.88f, 2.48f),          // Driller
                new Vector3(1.675f, -0.79f, 2.18f),      // Flak
                //new Vector3(1.675f, -0.99f, 2.35f),    // Thunderbolt -- original spacing
                new Vector3(1.175f, -0.99f, 2.35f),      // Thunderbolt -- tighter spacing (0.2f world space adjustment, scaled at 0.4 for the ship local coordinates)
                new Vector3(1.675f, -0.93f, 2.6f),       // Lancer
                new Vector3(2.3f, -1.505f, 1.63f)        // Quad Impulse firepoint
            };

            ShieldMultiplier = 1f;

            m_slide_force_mp = new float[4] { 25f, 26.25f, 26.25f, 26.25f };
            m_turn_speed_limit_acc = new float[5] { 2.3f, 3.2f, 4.5f, 6f, 100f };
            m_turn_speed_limit_rb = new float[5] { 2.5f, 3.2f, 4f, 5.2f, 100f };

            boostMulti = 1.65f;
            boostMod = 1.85f;
        }

        // nothing needs to actually get created here, obviously, we handle creating the quad firepoints in base.ApplyParameters()
        /*
        public override void ApplyParameters()
        {
            base.ApplyParameters();
        }
        */
    }

    // ====================================================================
    //
    // ====================================================================
    // ====================================================================
    //
    // ====================================================================

    // in all its wingy glory
    public class PyroGX : Ship
    {
        public PyroGX()
        {
            displayName = "Pyro GX";
            name = "entity_special_player_pyro";
            meshName = "Pyro";
            colliderNames = new string[3] { "PyroCollider-100", "PyroCollider-105", "PyroCollider-110" };

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

            ShieldMultiplier = 1f;

            m_slide_force_mp = new float[4] { 25f, 26.25f, 26.25f, 26.25f };
            m_turn_speed_limit_acc = new float[5] { 2.3f, 3.2f, 4.5f, 6f, 100f };
            m_turn_speed_limit_rb = new float[5] { 2.5f, 3.2f, 4f, 5.2f, 100f };

            boostMulti = 1.65f;
            boostMod = 1.85f;
        }

        public override void ApplyParameters(GameObject go)
        {
            PlayerShip ps = go.GetComponent<PlayerShip>();

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
            ps.m_thruster_trans[0].localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ps.m_thruster_trans[0].localPosition = new Vector3(0f, 0.18f, 0.43f);

            // front thruster
            ps.m_thruster_trans[2].localRotation = Quaternion.Euler(0f, 0f, 0f);
            ps.m_thruster_trans[2].localPosition = new Vector3(0f, -0.09f, 1.25f);

            // bottom thruster
            ps.m_thruster_trans[3].localRotation = Quaternion.Euler(90f, 0f, 0f);
            ps.m_thruster_trans[3].localPosition = new Vector3(0f, -0.28f, 0.84f);

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

    // Slower version of the Pyro
    public class PyroGXSlow : PyroGX
    {
        public PyroGXSlow()
        {
            displayName = "Pyro GX (Slow)";
            name = "entity_special_player_pyro_slow";
            meshName = "Pyro";
            colliderNames = new string[3] { "PyroCollider-100", "PyroCollider-105", "PyroCollider-110" };

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

            m_slide_force_mp = new float[4] { 22.5f, 23.6f, 23.6f, 23.6f };
            m_turn_speed_limit_acc = new float[5] { 1.84f, 2.6f, 3.6f, 4.8f, 100f };
            m_turn_speed_limit_rb = new float[5] { 2f, 2.6f, 3.2f, 4.2f, 100f };

            boostMulti = 1.8f;
            boostMod = 2.0f;
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

            m_slide_force_mp = new float[4] { 25f, 26.25f, 26.25f, 26.25f };
            m_turn_speed_limit_acc = new float[5] { 2.3f, 3.2f, 4.5f, 6f, 100f };
            m_turn_speed_limit_rb = new float[5] { 2.5f, 3.2f, 4f, 5.2f, 100f };

            boostMulti = 1.65f;
            boostMod = 1.85f;
        }

        public override void ApplyParameters(GameObject go)
        {
            base.ApplyParameters(go);

            PlayerShip ps = go.GetComponent<PlayerShip>();

            ps.m_muzzle_center.localPosition = new Vector3(0f, -1.926f, 2.485f); // center weapon firepoint back to the Kodachi's spot
            ps.m_muzzle_center2.localPosition = new Vector3(0f, -1.873f, 0f); // center missile firepoint back to the Kodachi's spot

            ps.m_muzzle_left.localPosition = new Vector3(-1.675f, -1.105f, 2.611f); // left weapon firepoint back to the Kodachi's spot
            ps.m_muzzle_right.localPosition = new Vector3(1.675f, -1.105f, 2.611f); // right weapon firepoint back to the Kodachi's spot

            // This is a little crude since we're 2 levels deep. Oh well.
            go.name = name;
            SetQuadFirepoints(ps); // gotta call it again because the first set of adjustments was for the full Pyro
        }
    }

    // ====================================================================
    //
    // ====================================================================
    // ====================================================================
    //
    // ====================================================================

    // gotta go fast
    public class Phoenix : Ship
    {
        public Phoenix()
        {
            displayName = "Phoenix Interceptor";
            name = "entity_special_player_phoenix";
            meshName = "Phoenix";
            colliderNames = new string[3] { "PhoenixCollider-100", "PhoenixCollider-105", "PhoenixCollider-110" };
            
            extras = new GameObject[1];
            extraNames = new string[] { "PhoenixThruster" };

            customizations = false;

            FIRING_POINTS = new Vector3[9]
            {
            new Vector3(1.62f, -0.14f, -0.03f),      // Impulse
            new Vector3(0f, -1.15f, 0.26f),           // Cyclone
            new Vector3(1.62f, -0.14f, -0.03f),      // Reflex
            new Vector3(2.51f, -1.35f, 0.44f),       // Crusher
            new Vector3(0f, -1.0f, 0.57f),            // Driller
            new Vector3(1.62f, -0.14f, -0.03f),      // Flak
            new Vector3(1.62f, -0.14f, -0.03f),      // Thunderbolt
            new Vector3(2.51f, -1.35f, 0.44f),       // Lancer
            new Vector3(2.51f, -1.35f, 0.44f)        // Quad Impulse firepoint
            };

            ShieldMultiplier = 0.90f;
            // still playing with these, needs some work.
            m_slide_force_mp = new float[4] { 26f, 27.83f, 27.83f, 27.83f };
            m_turn_speed_limit_acc = new float[5] { 2.4f, 3.35f, 4.65f, 6.2f, 100f };
            m_turn_speed_limit_rb = new float[5] { 2.5f, 3.2f, 4f, 5.2f, 100f };

            boostMulti = 1.75f;
            boostMod = 1.95f;
        }

        public override void ApplyParameters(GameObject go)
        {
            PlayerShip ps = go.GetComponent<PlayerShip>();

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
            ps.m_thruster_trans[0].localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ps.m_thruster_trans[0].localPosition = new Vector3(0f, 0.313f, 0.025f);

            // front thruster
            ps.m_thruster_trans[2].localRotation = Quaternion.Euler(0f, 0f, 0f);
            ps.m_thruster_trans[2].localPosition = new Vector3(0f, 0.29f, 0.46f);

            // bottom thruster
            ps.m_thruster_trans[3].localRotation = Quaternion.Euler(90f, 0f, 0f);
            ps.m_thruster_trans[3].localPosition = new Vector3(0f, -0.145f, 1f);

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
            ts.localPosition = new Vector3(-0.95f, .77f, 0.68f);
            ts.gameObject.SetActive(false);

            // Same with the right mesh locations
            ts = ps.c_cockpit.transform.GetChild(2); // right weapon mount
            ts.localRotation = Quaternion.Euler(0f, 0f, -36.6f);
            ts.localPosition = new Vector3(0.95f, .77f, 0.68f);
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

            // move the headlights
            ts = ps.c_lights[0].transform; // right light
            ts.localPosition = new Vector3(0.42f, 1.6f, -2.45f);
            ts = ps.c_lights[1].transform; // left light
            ts.localPosition = new Vector3(-0.42f, 1.6f, -2.45f);

            base.ApplyParameters(go);
        }
    }


    // ====================================================================
    //
    //
    // ====================================================================
    // Utility Functions
    // ====================================================================
    //
    //
    // ====================================================================



    // Loads the new Prefabs
    [HarmonyPatch(typeof(NetworkSpawnPlayer), "Init")]
    static class MPShips_NetworkSpawnPlayer_Init
    {
        public static void Postfix()
        {
            MPShips.AddShips();
            MPShips.LoadResources();
        }
    }

    // Assigns the chosen Ship definition's properties to the ship prefab at instantiation
    [HarmonyPatch(typeof(NetworkSpawnPlayer), "InstantiatePlayer", new System.Type[] { typeof(GameObject), typeof(Vector3), typeof(Quaternion) })]
    static class MPShips_NetworkSpawnPlayer_InstantiatePlayer
    {
        static void Postfix(GameObject __result)
        {
            //MPShips.Selected.Add(__result.GetComponent<PlayerShip>().netId, selected); ---- this needs to go in a network message, not here, and only if multiship rounds are implemented
            MPShips.selected.ApplyParameters(__result);
        }
    }


    // Hides the custom body panels if we're using a non-standard playership
    [HarmonyPatch(typeof(PlayerShip), "SetCustomBody")]
    static class MPShips_PlayerShip_SetCustomBody
    {
        static bool Prefix()
        {
            if (!MPShips.selected.customizations)
            {
                return false;
            }
            return true;
        }
    }

    // Hides the custom wings if we're using a non-standard playership
    [HarmonyPatch(typeof(PlayerShip), "SetCustomWings")]
    static class MPShips_PlayerShip_SetCustomWings
    {
        static bool Prefix()
        {
            if (!MPShips.selected.customizations)
            {
                return false;
            }
            return true;
        }
    }

    // moves the quad shot positions in the PlayerShip firing code.
    // Currently this pulls from the selected ship definition. If multiples are to exist in the -same- round, this will need updating again.
    [HarmonyPatch(typeof(PlayerShip), "MaybeFireWeapon")]
    static class MPShips_PlayerShip_MaybeFireWeapon
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldc_R4 && state < 6)
                {
                    switch ((float)code.operand)
                    {
                        case 0.25f: // right quad
                            yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MPShips), "selected"));
                            yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Ship), "QdiffRightX"));
                            state++;
                            break;
                        case -0.25f: // left quad
                            yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MPShips), "selected"));
                            yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Ship), "QdiffLeftX"));
                            state++;
                            break;
                        case -0.15f:
                            yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MPShips), "selected"));
                            yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Ship), "QdiffY"));
                            state++;
                            break;
                        case -0.3f:
                            yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MPShips), "selected"));
                            yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Ship), "QdiffZ"));
                            state++;
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
        }
    }

    // Moved from Debugging.cs and modified - replaces the SwitchVisibleWeapon method with one that uses Vector3 for positioning instead of Vector2.
    // I am unsure if the exception catching is necessary or not anymore.
    [HarmonyPatch(typeof(PlayerShip), "SwitchVisibleWeapon")]
    static class MPShips_PlayerShip_SwitchVisibleWeapon
    {
        static bool Prefix(PlayerShip __instance, bool force_visible = false, WeaponType wt = WeaponType.NUM)
        {
            if (wt == WeaponType.NUM)
            {
                wt = __instance.c_player.m_weapon_type;
            }
            if (__instance.IsCockpitVisible || force_visible || !__instance.isLocalPlayer)
            {
                for (int i = 0; i < 8; i++)
                {
                    try
                    {
                        __instance.m_weapon_mounts1[i].SetActive(i == (int)wt);
                    }
                    catch (System.Exception e)
                    {
                        Debug.Log("Exception setting the first weapon mount's active state.");
                        Debug.LogException(e);
                    }
                    if (__instance.c_player.m_cloaked && i == (int)wt)
                    {
                        MeshRenderer[] componentsInChildren = null;
                        try
                        {
                            componentsInChildren = __instance.m_weapon_mounts1[i].GetComponentsInChildren<MeshRenderer>(includeInactive: true);
                        }
                        catch (System.Exception e)
                        {
                            Debug.Log("Exception getting the first weapon mount's components.");
                            Debug.LogException(e);
                        }
                        if (componentsInChildren != null)
                        {
                            foreach (MeshRenderer meshRenderer in componentsInChildren)
                            {
                                meshRenderer.enabled = false;
                            }
                        }
                    }
                    if (i == 4 || i == 1) // Cyclone or Driller
                    {
                        continue;
                    }

                    try
                    {
                        __instance.m_weapon_mounts2[i].SetActive(i == (int)wt);
                    }
                    catch (System.Exception e)
                    {
                        Debug.Log("Exception setting the second weapon mount's active state.");
                        Debug.LogException(e);
                    }
                    if (__instance.c_player.m_cloaked && i == (int)wt)
                    {
                        MeshRenderer[] componentsInChildren2 = null;
                        try
                        {
                            componentsInChildren2 = __instance.m_weapon_mounts2[i].GetComponentsInChildren<MeshRenderer>(includeInactive: true);
                        }
                        catch (System.Exception e)
                        {
                            Debug.Log("Exception getting the second weapon mount's components.");
                            Debug.LogException(e);
                        }
                        if (componentsInChildren2 != null)
                        {
                            foreach (MeshRenderer meshRenderer2 in componentsInChildren2)
                            {
                                meshRenderer2.enabled = false;
                            }
                        }
                    }
                }
            }
            if (__instance.c_player.m_weapon_type == WeaponType.DRILLER || __instance.c_player.m_weapon_type == WeaponType.CYCLONE)
            {
                Vector3 localPosition = __instance.m_muzzle_center.localPosition;
                try
                {
                    localPosition.x = MPShips.selected.FIRING_POINTS[(int)__instance.c_player.m_weapon_type].x;
                    localPosition.y = MPShips.selected.FIRING_POINTS[(int)__instance.c_player.m_weapon_type].y;
                    localPosition.z = MPShips.selected.FIRING_POINTS[(int)__instance.c_player.m_weapon_type].z;
                }
                catch (System.Exception e)
                {
                    Debug.Log("Exception getting the firing points for the driller or cyclone.");
                    Debug.LogException(e);
                }
                __instance.m_muzzle_center.localPosition = localPosition;
            }
            else
            {
                Vector3 localPosition2 = __instance.m_muzzle_right.localPosition;
                try
                {
                    localPosition2.x = MPShips.selected.FIRING_POINTS[(int)__instance.c_player.m_weapon_type].x;
                    localPosition2.y = MPShips.selected.FIRING_POINTS[(int)__instance.c_player.m_weapon_type].y;
                    localPosition2.z = MPShips.selected.FIRING_POINTS[(int)__instance.c_player.m_weapon_type].z;
                }
                catch (System.Exception e)
                {
                    Debug.Log("Exception getting the firing points for other weapons.");
                    Debug.LogException(e);
                }
                __instance.m_muzzle_right.localPosition = localPosition2;
                localPosition2.x *= -1;
                __instance.m_muzzle_left.localPosition = localPosition2;
            }
            return false;
        }
    }

    // substitutes the PlayerShip's regular ship handling references with references to the Ship definition classes instead
    [HarmonyPatch(typeof(PlayerShip), "FixedUpdateProcessControlsInternal")]
    static class MPShips_PlayerShip_FixedUpdateProcessControlsInternal
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            bool boost_done = false;
            CodeInstruction ci;

            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldfld && code.operand == AccessTools.Field(typeof(PlayerShip), "m_slide_force_mp"))
                {
                    yield return new CodeInstruction(OpCodes.Pop); // there's a PlayerShip instance reference to clear out first
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MPShips), "selected"));
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Ship), "m_slide_force_mp"));
                }
                else if (code.opcode == OpCodes.Ldsfld && code.operand == AccessTools.Field(typeof(PlayerShip), "m_turn_speed_limit_acc"))
                {
                    ci = new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MPShips), "selected")); // original had a label on it
                    ci.labels = code.labels;
                    yield return ci;
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Ship), "m_turn_speed_limit_acc"));
                }
                else if (code.opcode == OpCodes.Ldsfld && code.operand == AccessTools.Field(typeof(PlayerShip), "m_turn_speed_limit_rb"))
                {
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MPShips), "selected"));
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Ship), "m_turn_speed_limit_rb"));
                }
                else if (code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 1.85f && !boost_done)
                {
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MPShips), "selected"));
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Ship), "boostMod"));
                }
                else if (code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 1.65f && !boost_done)
                {
                    ci = new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MPShips), "selected")); // more labels
                    ci.labels = code.labels;
                    yield return ci;
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Ship), "boostMulti"));
                    boost_done = true;
                }
                else
                {
                    yield return code;
                }
            }
        }
    }

    // handles shield strength scaling at the damage side
    [HarmonyPatch(typeof(PlayerShip), "ApplyDamage")]
    static class MPShips_PlayerShip_ApplyDamage
    {
        static void Prefix(ref DamageInfo di)
        {
            di.damage /= MPShips.selected.ShieldMultiplier;
        }
    }

    // handles shield strength scaling at the pickup side
    [HarmonyPatch(typeof(Player), "AddArmor")]
    static class MPShips_Player_AddArmor
    {
        static void Prefix(ref float armor)
        {
            armor /= MPShips.selected.ShieldMultiplier;
        }
    }
}
