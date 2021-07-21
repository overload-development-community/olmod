using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameMod
{
    [HarmonyPatch()]
    class CustomLevelMaterials
    {
        static Dictionary<string, string> extra_materials = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase) {
            { "alien_cave_crystals_02a", "alien_cave_crystals_02a/alien_cave_crystals_02a" },
            { "alien_decals_01a_dark", "alien_decals_01a/alien_decals_01a_dark" },
            { "alien_decals_02a_dark", "alien_decals_02a/alien_decals_02a_dark" },
            { "alien_decals_02b_dark", "alien_decals_02b/alien_decals_02b_dark" },
            { "alien_decals_03g_dark", "alien_decals_03g/alien_decals_03g_dark" },
            { "om_signs_ambient_02b", "om_signs_ambient_02b/om_signs_ambient_02b" }
        };

        static MethodInfo TargetMethod()
        {
            return typeof(UserLevelLoader).Assembly.GetType("ResourceDatabase").GetMethod("LookupMaterial", BindingFlags.Static | BindingFlags.Public);
        }

        static void Postfix(string name, ref string __result)
        {
            if (__result != null)
                return;
            if (extra_materials.TryGetValue(name, out string value))
                __result = value;
        }
    }

    [HarmonyPatch()]
    class CustomLevelPrefabs
    {
        static Dictionary<string, string> extra_prefabs = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase) {
            { "entity_prop_alien_socket", "entity_prop_alien_socket" },
            { "entity_prop_monitor_secret1", "entity_prop_monitor_secret1" },
            { "entity_prop_monitor_secret2", "entity_prop_monitor_secret2" },
            { "entity_prop_monitor_tn1", "entity_prop_monitor_tn1" },
            { "entity_prop_monitor_tn2", "entity_prop_monitor_tn2" },
            { "entity_prop_monitor_tn3", "entity_prop_monitor_tn3" },
            { "entity_prop_fan_tn3", "entity_prop_fan_tn3" },
            { "entity_prop_fan_tn4", "entity_prop_fan_tn4" },
            { "entity_prop_fan_tn_corner", "entity_prop_fan_tn_corner" },
            { "entity_prop_reactor_om16", "entity_prop_reactor_om16" },
            { "entity_trigger_box_lava_alien", "entity_trigger_box_lava_alien" },
            { "entity_trigger_box_lava_normal", "entity_trigger_box_lava_normal" }
        };

        static MethodInfo TargetMethod()
        {
            return typeof(UserLevelLoader).Assembly.GetType("ResourceDatabase").GetMethod("LookupPrefab", BindingFlags.Static | BindingFlags.Public);
        }

        static void Postfix(string name, ref string __result)
        {
            if (__result != null)
                return;
            if (extra_prefabs.TryGetValue(name, out string value))
                __result = value;
        }
    }
}
