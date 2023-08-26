using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace GameMod
{
    public static class Assets
    {
        public static Dictionary<string, Material> materials = new Dictionary<string, Material>();

        public static void LoadAssets()
        {
            LoadMaterials();
        }

        public static void LoadMaterials()
        {
            foreach (Material m in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (!materials.ContainsKey(m.name))
                {
                    materials[m.name] = m;
                }
            }
        }
    }
}
