using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Newtonsoft.Json;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod
{
    public static class MPSpawnExtension
    {
        const string BASE_URL = "https://raw.githubusercontent.com/overload-development-community/ol-map-revisions/main/spawnpoints/";

        public static List<LevelData.SpawnPoint> spawnpoints = new List<LevelData.SpawnPoint>();
        public static bool DownloadBusy = false;
        public static bool DownloadChecked = false;


        // *****************************
        // INPUT
        // *****************************


        public static void ResetForNewLevel()
        {
            //Debug.Log("CCF resetting in MPSpawnExtension");
            spawnpoints.Clear();
            DownloadChecked = false;
        }

        public static void CheckExtraSpawnpoints(string name)
        {
            if (!GameplayManager.IsMultiplayer || DownloadChecked)
            {
                return;
            }

            DownloadChecked = true;

            string[] split = name.Split(':');
            if (split == null)
            {
                Debug.Log("Invalid level name in CheckExtraSpawnpoints, skipping.");
                return;
            }

            string levelname = split[0].ToUpperInvariant();

            if (levelname.EndsWith(".MP"))
            {
                levelname = levelname.Remove(levelname.Length - 3);
            }

            GameManager.m_gm.StartCoroutine(LoadSpawnpointFile(levelname));
        }

        public static IEnumerator LoadSpawnpointFile(string name)
        {
            DownloadBusy = true; // piggybacking off of the download code delay

            //Debug.Log("CCF requesting url " + BASE_URL + $"{name}-spawnpoints.json");

            UnityWebRequest www = UnityWebRequest.Get(BASE_URL + $"{name}-spawnpoints.json");
            www.timeout = 3;
            yield return www.SendWebRequest();

            if (www.isNetworkError)
            {
                Debug.Log(www.error);
                DownloadBusy = false;
                yield break;
            }
            if (www.responseCode == 200) // we found a file
            {
                try
                {
                    spawnpoints = JsonConvert.DeserializeObject<List<LevelData.SpawnPoint>>(www.downloadHandler.text);
                    Debug.Log($"{spawnpoints.Count} additional spawnpoints parsed from the current level's online json file");
                }
                catch (Exception e)
                {
                    Debug.LogError("WARNING: Unable to parse additional player spawns, continuing with originals (error message: " + e.Message + ")");
                    spawnpoints.Clear();
                }
            }

            DownloadBusy = false;
        }

        public static void LoadExtraSpawnpoints(LevelData ld)
        {
            //Debug.Log("CCF additional spawn count: " + spawnpoints.Count);
            if (spawnpoints.Count != 0)
            {
                LevelData.SpawnPoint[] NewSpawnPoints = ld.m_player_spawn_points.Concat(spawnpoints).ToArray();
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
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPSpawnExtension), "LoadExtraSpawnpoints"));

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
                if (!MPSpawnExtension.spawnpoints.Contains(sp) && Vector3.Distance(GameManager.m_player_ship.c_transform_position, sp.position) <= 3f)
                {
                    adding = false;
                }
            }

            if (adding)
            {
                Vector3 rot = GameManager.m_player_ship.c_transform.eulerAngles;
                Quaternion quantized = Quaternion.Euler(((int)rot.x + 15) / 30 * 30, ((int)rot.y + 15) / 30 * 30, ((int)rot.z + 15) / 30 * 30); // quantize things a bit for rotation

                SpawnpointVis vis = new SpawnpointVis
                {
                    position = GameManager.m_player_ship.c_transform_position,
                    rotation = quantized,
                    spawnpoint = new LevelData.SpawnPoint(),
                    visualizer = GameObject.Instantiate(MPShips.selected.collider[0], GameManager.m_player_ship.c_transform_position, quantized)
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
                string filepath = Path.Combine(Config.OLModDir, $"{GameplayManager.Level.FileName.ToUpperInvariant()}-spawnpoints.json");
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

                    if (MPSpawnExtension.spawnpoints.Contains(sp))
                    {
                        Material m = mr.material;
                        m.color = Color.green;
                        m.SetColor("_GlowColor", Color.green);
                        m.SetColor("_EdgeColor", Color.green);

                        MPSpawnExtensionVis.ExtraSpawns.Add(new SpawnpointVis() { position = sp.position, rotation = sp.orientation, spawnpoint = sp, visualizer = go });
                    }

                    mr.enabled = true;
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
