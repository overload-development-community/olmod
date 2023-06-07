using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Newtonsoft.Json;
using Overload;
using UnityEngine;

namespace GameMod
{
    public static class MPSpawnExtension
    {
        // *****************************
        // INPUT
        // *****************************

        public static List<LevelData.SpawnPoint> LoadExtraSpawnpoints()
        {
            if (!GameplayManager.IsMultiplayer || !GameplayManager.Level.IsAddOn)
            {
                return null;
            }

            List<LevelData.SpawnPoint> ExtraSpawns;
            string payload = "";
            var filepath = Path.Combine(Path.GetDirectoryName(GameplayManager.Level.FilePath), $"{GameplayManager.Level.FileName}-spawnpoints.json");

            string text3 = null;
            byte[] array = Mission.LoadAddonData(GameplayManager.Level.ZipPath, filepath, ref text3, new string[] { ".json" });
            if (array != null)
            {
                payload = System.Text.Encoding.UTF8.GetString(array);
            }
            else
            {
                return null;
            }

            try
            {
                ExtraSpawns = JsonConvert.DeserializeObject<List<LevelData.SpawnPoint>>(payload);
                Debug.Log($"{ExtraSpawns.Count} additional spawnpoints parsed from the current level's embedded json file");
            }
            catch (Exception e)
            {
                Debug.LogError("ERROR: Unable to parse additional player spawns, continuing with originals (error message: " + e.Message + ")");
                return null;
            }

            return ExtraSpawns;
        }

        public static void AddPlayerSpawns(LevelData ld)
        {
            List<LevelData.SpawnPoint> ExtraSpawns = LoadExtraSpawnpoints();

            if (ExtraSpawns != null)
            {
                LevelData.SpawnPoint[] NewSpawnPoints = ld.m_player_spawn_points.Concat(ExtraSpawns).ToArray();
                ld.m_player_spawn_points = NewSpawnPoints;
            }
        }
    }

    [HarmonyPatch(typeof(LevelData), "Awake")]
    public static class MPSpawnExtension_LevelData_Awake
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int count = 0;
            foreach (var code in codes)
            {
                yield return code;

                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(LevelData), "SetSpawnPointSegments"))
                {
                    count++;
                }
                if (count == 2)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPSpawnExtension), "AddPlayerSpawns"));

                    count++;
                }
            }
        }
    }


    // *****************************
    // Output
    // *****************************

    public static class MPSpawnExtensionVis
    {
        public static bool visualizing = false;

        public static List<SpawnpointVis> ExtraSpawns = new List<SpawnpointVis>();

        public static void TriggerSpawnToggle()
        {
            bool adding = true;

            // if we're within 3U of an added spawn, remove it instead of adding a new one
            foreach (SpawnpointVis spv in ExtraSpawns)
            {
                if (Vector3.Distance(GameManager.m_player_ship.c_transform_position, spv.position) <= 3f)
                {
                    adding = false;
                    GameObject.Destroy(spv.visualizer);
                    ExtraSpawns.Remove(spv);
                    break;
                }
            }

            // if we're within 3U of an original spawn, don't add a new one
            foreach (LevelData.SpawnPoint sp in GameManager.m_level_data.m_player_spawn_points)
            {
                if (Vector3.Distance(GameManager.m_player_ship.c_transform_position, sp.position) <= 3f)
                {
                    adding = false;
                }
            }

            if (adding)
            {
                SpawnpointVis vis = new SpawnpointVis
                {
                    position = GameManager.m_player_ship.c_transform_position,
                    rotation = GameManager.m_player_ship.c_transform_rotation,
                    spawnpoint = new LevelData.SpawnPoint(),
                    visualizer = GameObject.Instantiate(MPShips.selected.collider[0], GameManager.m_player_ship.c_transform_position, GameManager.m_player_ship.c_transform_rotation)
                };

                vis.spawnpoint.position = vis.position;
                vis.spawnpoint.orientation = vis.rotation;

                MeshRenderer mr = vis.visualizer.AddComponent<MeshRenderer>();
                mr.sharedMaterial = UIManager.gm.m_energy_material;
                Material m = mr.material;
                m.color = Color.green;
                m.SetColor("_GlowColor", Color.green);
                m.SetColor("_EdgeColor", Color.green);
                mr.enabled = true;

                ExtraSpawns.Add(vis);
            }            
        }

        public static void Export()
        {
            if (ExtraSpawns.Count != 0)
            {
                string filepath = Path.Combine(Config.OLModDir, $"{GameplayManager.Level.FileName}-spawnpoints.json");
                string extension = Path.GetExtension(filepath);

                int i = 0;
                while (File.Exists(filepath))
                {
                    if (i == 0)
                        filepath = filepath.Replace(extension, "(" + ++i + ")" + extension);
                    else
                        filepath = filepath.Replace("(" + i + ")" + extension, "(" + ++i + ")" + extension);
                }

                List<LevelData.SpawnPoint> payload = new List<LevelData.SpawnPoint>();

                foreach (SpawnpointVis spv in ExtraSpawns)
                {
                    payload.Add(spv.spawnpoint);
                }
                Debug.Log("Exported spawnpoints to filepath: " + filepath);
                File.AppendAllText(filepath, JsonConvert.SerializeObject(payload, Formatting.Indented, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }));
            }
            else
            {
                Debug.Log("No additional spawnpoints, nothing to export!");
            }
        }
    }

    public struct SpawnpointVis
    {
        public Vector3 position;
        public Quaternion rotation;
        public LevelData.SpawnPoint spawnpoint;
        public GameObject visualizer;
    }

    [HarmonyPatch(typeof(Client), "OnMatchStart")]
    class MPSpawnExtension_Client_OnMatchStart
    {
        static void Postfix()
        {
            if (GameplayManager.IsDedicatedServer() || !GameManager.m_local_player.m_spectator)
                return;

            if (MPSpawnExtensionVis.visualizing)
            {
                MPSpawnExtensionVis.ExtraSpawns.Clear();

                foreach (LevelData.SpawnPoint sp in GameManager.m_level_data.m_player_spawn_points)
                {
                    GameObject go = GameObject.Instantiate(MPShips.selected.collider[0], sp.position, sp.orientation);

                    MeshRenderer mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = UIManager.gm.m_energy_material;
                }
            }
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawHUD")]
    class MPSpawnExtension_UIElement_DrawHUD
    {
        static void Postfix(UIElement __instance)
        {
            if (MPSpawnExtensionVis.visualizing && MPObserver.Enabled)
            {
                Vector2 vector = default(Vector2);
                vector.x = UIManager.UI_LEFT + 20f;
                vector.y = UIManager.UI_TOP + 120f;
                __instance.DrawStringSmall("Spawnpoint Edit Mode", vector, 0.55f, StringOffset.LEFT, UIManager.m_col_damage, 1f);
                vector.y += 18f;
                __instance.DrawStringSmall("Use SMASH ATTACK to add or remove spawnpoints", vector, 0.4f, StringOffset.LEFT, UIManager.m_col_damage, 1f);
                vector.y += 15f;
                __instance.DrawStringSmall("Use command \"export-spawns\" when finished", vector, 0.4f, StringOffset.LEFT, UIManager.m_col_damage, 1f);
                vector.y += 20f;
                __instance.DrawStringSmall("Additional spawnpoints: " + MPSpawnExtensionVis.ExtraSpawns.Count, vector, 0.4f, StringOffset.LEFT, UIManager.m_col_damage, 1f);
            }
        }
    }
}
