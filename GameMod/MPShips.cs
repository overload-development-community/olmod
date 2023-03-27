using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace GameMod
{
    // Handles asset replacement for ships (well, -ship-, currently)
    public static class MPShips
    {
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

        public static bool loading = false; // set to true during the loading process

        public static void AddShips()
        {
            // ship prefabs are explicitly added here
            //Debug.Log("Adding Kodachi ship definition");
            Ships.Add(new Kodachi()); // Don't mess with this one or you're gonna break stuff.

            selected = Ships[selected_idx];
        }

        public static void LoadResources()
        {
            loading = true;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GameMod.Resources.meshes"))
            {
                var ab = AssetBundle.LoadFromStream(stream);

                foreach (Ship s in Ships)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        s.collider[i] = Object.Instantiate(ab.LoadAsset<GameObject>(s.colliderNames[i]));
                        Object.DontDestroyOnLoad(s.collider[i]);
                        Debug.Log("MeshCollider loaded: " + s.collider[i].name);
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
        public string[] colliderNames; // the 3 MeshCollider names

        public GameObject[] collider = new GameObject[3];
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
            colliderNames = new string[3] { "PlayershipCollider-100", "PlayershipCollider-105", "PlayershipCollider-110" };
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
}