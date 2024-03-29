using HarmonyLib;
using Overload;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GameMod
{
    /// <summary>
    ///    adds a config file per pilot in a way that allows keeping up backward compatibility
    ///    for settings that dont belong into .xprefsmod due to their size.
    ///           
    ///  Author:  luponix 
    ///  Created: 2021-06-03
    /// </summary>
    class ExtendedConfig
    {

        private const string file_extension = ".extendedconfig";
        private static List<string> unknown_sections;

        // Legacy autoselect file.
        public static string textFile = Path.Combine(Application.persistentDataPath, "AutoSelect-Config.txt");

        // On Game Loading or when selecting a different PILOT read or generating PILOT.extendedconfig
        [HarmonyPriority(Priority.Last)]
        [HarmonyPatch(typeof(PilotManager), "Select", new Type[] { typeof(string) })]
        internal class ExtendedConfig_PilotManager_Select
        {
            public static void Prefix(string name)
            {
                LoadPilotExtendedConfig(name);
            }

            public static void Postfix()
            {
                Section_JoystickCurve.ParseSectionData();
            }

            public static void LoadPilotExtendedConfig(string name)
            {
                SetDefaultConfig();

                if (GameplayManager.IsDedicatedServer())
                {
                    Debug.Log("ExtendedConfig_PilotManager_Select called on the server");
                    return;
                }

                var loaded = false;

                if (!string.IsNullOrEmpty(name))
                {
                    string filepath = Path.Combine(Application.persistentDataPath, name + file_extension);
                    if (File.Exists(filepath))
                    {
                        ReadConfigData(filepath);
                        loaded = true;
                    }
                }

                if (!loaded)
                {
                    // Attempt to use autoselect from pre 0.4.1.
                    if (File.Exists(textFile))
                    {
                        Debug.Log("Extended config does not exist for pilot, attempting to load pre-0.4.1 autoselect.");
                        using (StreamReader sr = File.OpenText(textFile))
                        {
                            MPAutoSelection.PrimaryPriorityArray[0] = sr.ReadLine();
                            MPAutoSelection.PrimaryPriorityArray[1] = sr.ReadLine();
                            MPAutoSelection.PrimaryPriorityArray[2] = sr.ReadLine();
                            MPAutoSelection.PrimaryPriorityArray[3] = sr.ReadLine();
                            MPAutoSelection.PrimaryPriorityArray[4] = sr.ReadLine();
                            MPAutoSelection.PrimaryPriorityArray[5] = sr.ReadLine();
                            MPAutoSelection.PrimaryPriorityArray[6] = sr.ReadLine();
                            MPAutoSelection.PrimaryPriorityArray[7] = sr.ReadLine();
                            MPAutoSelection.SecondaryPriorityArray[0] = sr.ReadLine();
                            MPAutoSelection.SecondaryPriorityArray[1] = sr.ReadLine();
                            MPAutoSelection.SecondaryPriorityArray[2] = sr.ReadLine();
                            MPAutoSelection.SecondaryPriorityArray[3] = sr.ReadLine();
                            MPAutoSelection.SecondaryPriorityArray[4] = sr.ReadLine();
                            MPAutoSelection.SecondaryPriorityArray[5] = sr.ReadLine();
                            MPAutoSelection.SecondaryPriorityArray[6] = sr.ReadLine();
                            MPAutoSelection.SecondaryPriorityArray[7] = sr.ReadLine();
                            MPAutoSelection.PrimaryNeverSelect[0] = sr.ReadLine().ToLower() == "true";
                            MPAutoSelection.PrimaryNeverSelect[1] = sr.ReadLine().ToLower() == "true";
                            MPAutoSelection.PrimaryNeverSelect[2] = sr.ReadLine().ToLower() == "true";
                            MPAutoSelection.PrimaryNeverSelect[3] = sr.ReadLine().ToLower() == "true";
                            MPAutoSelection.PrimaryNeverSelect[4] = sr.ReadLine().ToLower() == "true";
                            MPAutoSelection.PrimaryNeverSelect[5] = sr.ReadLine().ToLower() == "true";
                            MPAutoSelection.PrimaryNeverSelect[6] = sr.ReadLine().ToLower() == "true";
                            MPAutoSelection.PrimaryNeverSelect[7] = sr.ReadLine().ToLower() == "true";
                            MPAutoSelection.SecondaryNeverSelect[0] = sr.ReadLine().ToLower() == "true";
                            MPAutoSelection.SecondaryNeverSelect[1] = sr.ReadLine().ToLower() == "true";
                            MPAutoSelection.SecondaryNeverSelect[2] = sr.ReadLine().ToLower() == "true";
                            MPAutoSelection.SecondaryNeverSelect[3] = sr.ReadLine().ToLower() == "true";
                            MPAutoSelection.SecondaryNeverSelect[4] = sr.ReadLine().ToLower() == "true";
                            MPAutoSelection.SecondaryNeverSelect[5] = sr.ReadLine().ToLower() == "true";
                            MPAutoSelection.SecondaryNeverSelect[6] = sr.ReadLine().ToLower() == "true";
                            MPAutoSelection.SecondaryNeverSelect[7] = sr.ReadLine().ToLower() == "true";
                            MPAutoSelection.primarySwapFlag = sr.ReadLine().ToLower() == "true";
                            MPAutoSelection.secondarySwapFlag = sr.ReadLine().ToLower() == "true";
                            MPAutoSelection.swapWhileFiring = sr.ReadLine().ToLower() == "true";
                            MPAutoSelection.dontAutoselectAfterFiring = sr.ReadLine().ToLower() == "true";
                            MPAutoSelection.zorc = sr.ReadLine().ToLower() == "true";
                            MPAutoSelection.miasmic = sr.ReadLine().ToLower() == "true";
                        }

                        Section_AutoSelect.Set(true);
                    }
                }

                ApplyConfigData();
            }
        }


        // reads all lines and passes them to their respective functions to process them 
        // unknown sections get stored in ExtendedConfig.unknown_lines to reattach them to the end when saving
        private static void ReadConfigData(string filepath)
        {
            List<string> completed = new List<string>();

            uConsole.Log("ReadConfigData");
            using (StreamReader sr = new StreamReader(filepath))
            {
                unknown_sections = new List<string>();
                List<string> current_section = new List<string>();
                string line = String.Empty;
                string current_section_id = "unknown";

                while ((line = sr.ReadLine()) != null)
                {

                    if (line.StartsWith("[SECTION:"))
                    {
                        if (known_sections.Contains(line))
                        {
                            current_section_id = line;
                        }
                        else
                        {
                            current_section_id = "unknown";
                            unknown_sections.Add(line);
                        }
                    }
                    else if (line.Equals("[/END]"))
                    {
                        if (!current_section_id.Equals("unknown"))
                        {
                            PassSectionToFunction(current_section, current_section_id);
                            completed.Add(current_section_id);
                            current_section_id = "unknown";
                            current_section.Clear();
                        }
                        else
                        {
                            unknown_sections.Add(line);
                        }
                    }
                    else
                    {
                        if (current_section_id.Equals("unknown"))
                        {
                            unknown_sections.Add(line);
                        }
                        else
                        {
                            current_section.Add(line);
                        }
                    }
                }
            }

            foreach (string s in known_sections)
            {
                // a section is missing from the config file, create it
                if (!completed.Contains(s))
                {
                    List<string> l = new List<string>();
                    PassSectionToFunction(l, s);
                    Debug.Log("Creating missing section \"" + s + "\" in pilot's .extendedconfig");
                }
            }
        }


        [HarmonyPatch(typeof(Controls), "SaveControlData")]
        internal class ExtendedConfig_Controls_SaveControlData
        {
            public static void Postfix()
            {
                //uConsole.Log("ExtendedConfig_Controls_SaveControlData");
                if (GameplayManager.IsDedicatedServer())
                {
                    Debug.Log("ExtendedConfig_Controls_SaveControlData called on the server");
                    return;
                }
                SaveActivePilot();
            }
        }

        [HarmonyPatch(typeof(PilotManager), "SavePreferences")]
        internal class ExtendedConfig_PilotManager_SavePreferences
        {
            public static void Postfix()
            {
                //uConsole.Log("ExtendedConfig_PilotManager_SavePreferences");
                if (GameplayManager.IsDedicatedServer())
                {
                    Debug.Log("ExtendedConfig_Controls_SavePreferences called on the server");
                    return;
                }
                SaveActivePilot();
            }
        }

        [HarmonyPatch(typeof(PilotManager), "Create")]
        internal class ExtendedConfig_PilotManager_Create
        {
            public static void Prefix()
            {
                uConsole.Log("ExtendedConfig_PilotManager_Create");
                if (GameplayManager.IsDedicatedServer())
                {
                    Debug.Log("ExtendedConfig_PilotManager_Create called on the server");
                    return;
                }
                if (string.IsNullOrEmpty(PilotManager.ActivePilot))
                {
                    SetDefaultConfig();
                }
                SaveActivePilot();
            }

            public static void Postfix(string name, bool copy_prefs, bool copy_config)
            {
                if (GameplayManager.IsDedicatedServer())
                {
                    Debug.Log("ExtendedConfig_PilotManager_Create.Postfix called on the server");
                    return;
                }
                if (!copy_prefs && !copy_config)
                {
                    SetDefaultConfig();
                    unknown_sections = new List<string>();
                }
                SaveActivePilot();
            }
        }

        [HarmonyPatch(typeof(Platform), "DeleteUserData")]
        internal class ExtendedConfig_Platform_DeleteUserData
        {
            static void Postfix(string filename)
            {
                if (GameplayManager.IsDedicatedServer())
                {
                    Debug.Log("ExtendedConfig_Platform_DeleteUserData called on the server");
                    return;
                }
                if (filename.EndsWith(".xprefs"))
                {
                    Platform.DeleteUserData(filename.Replace(".xprefs", file_extension));
                }
            }
        }

        [HarmonyPatch(typeof(Controls), "OnControllerConnected")]
        internal class ExtendedConfig_Controls_OnControllerConnected
        {
            static void Postfix()
            {
                //Debug.Log("ExtendedConfig_Controls_OnControllerConnected");
                if (!GameplayManager.IsDedicatedServer())
                {
                    PilotManager.Select(PilotManager.ActivePilot);
                }
            }
        }
        /*
        [HarmonyPatch(typeof(Controls), "OnControllerDisconnected")]
        internal class ExtendedConfig_Controls_OnControllerDisconnected
        {
            static void Prefix()
            {
                //Debug.Log("ExtendedConfig_Controls_OnControllerDisconnected");
                if (!Network.isServer)
                {
                    
                }
            }
        }*/


        /////////////////////////////////////////////////////////////////////////////////////////
        ///                           Add new Sections here:                                  ///
        /////////////////////////////////////////////////////////////////////////////////////////

        private static List<string> known_sections = new List<string> {
            "[SECTION: AUTOSELECT]",
            "[SECTION: JOYSTICKCURVE]",
            "[SECTION: WEAPONCYCLING]",
            "[SECTION: AUDIOTAUNT_KEYBINDS]",
            "[SECTION: AUDIOTAUNT_MUTED_PLAYERS]",
            "[SECTION: AUDIOTAUNT_SELECTED_TAUNTS]",
            //...
        };


        private static void PassSectionToFunction(List<string> section, string section_name)
        {
            if (section_name.Equals(known_sections[0]))
            {
                Section_AutoSelect.Load(section);
                return;
            }
            if (section_name.Equals(known_sections[1]))
            {
                Section_JoystickCurve.Load(section);
                return;
            }
            if (section_name.Equals(known_sections[2]))
            {
                Section_WeaponCycling.Load(section);
                return;
            }
            if (section_name.Equals(known_sections[3]))
            {
                Section_AudiotauntKeybinds.Load(section);
                return;
            }
            if (section_name.Equals(known_sections[3]))
            {
                Section_AudiotauntKeybinds.Load(section);
                return;
            }
            if (section_name.Equals(known_sections[4]))
            {
                Section_AudiotauntMutedPlayers.Load(section);
                return;
            }
            if (section_name.Equals(known_sections[5]))
            {
                Section_AudiotauntSelectedTaunts.Load(section);
                return;
            }
            //...

        }

        public static void SaveActivePilot()
        {
            if (!PilotManager.Exists(PilotManager.ActivePilot))
            {
                return;
            }
            try
            {
                string filepath = Path.Combine(Application.persistentDataPath, PilotManager.ActivePilot + file_extension);

                using (StreamWriter w = File.CreateText(filepath))
                {
                    w.WriteLine("[SECTION: AUTOSELECT]");
                    Section_AutoSelect.Save(w);
                    w.WriteLine("[/END]");

                    w.WriteLine("[SECTION: JOYSTICKCURVE]");
                    Section_JoystickCurve.Save(w);
                    w.WriteLine("[/END]");

                    w.WriteLine("[SECTION: WEAPONCYCLING]");
                    Section_WeaponCycling.Save(w);
                    w.WriteLine("[/END]");

                    w.WriteLine("[SECTION: AUDIOTAUNT_KEYBINDS]");
                    Section_AudiotauntKeybinds.Save(w);
                    w.WriteLine("[/END]");

                    w.WriteLine("[SECTION: AUDIOTAUNT_MUTED_PLAYERS]");
                    Section_AudiotauntMutedPlayers.Save(w);
                    w.WriteLine("[/END]");

                    w.WriteLine("[SECTION: AUDIOTAUNT_SELECTED_TAUNTS]");
                    Section_AudiotauntSelectedTaunts.Save(w);
                    w.WriteLine("[/END]");

                    //...

                    if (unknown_sections != null)
                    {
                        foreach (string line in unknown_sections)
                        {
                            w.WriteLine(line);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is UnauthorizedAccessException)
                {
                    Debug.Log("Error in ExtendedConfig.SaveActivePilot: Could not save pilot file due to insufficient permissions");
                    return;
                }
                Debug.Log("Error in ExtendedConfig.SaveActivePilot: " + ex);
            }
        }

        public static void SetDefaultConfig()
        {
            Section_AutoSelect.Set();
            Section_JoystickCurve.SetDefault();
            Section_WeaponCycling.Set();
            Section_AudiotauntKeybinds.SetDefaultKeybinds();
        }

        public static void ApplyConfigData()
        {
            Section_AutoSelect.ApplySettings();
            Section_WeaponCycling.ApplySettings();
        }



        /////////////////////////////////////////////////////////////////////////////////////////
        ///                           SECTION SPECIFIC FUNCTIONS:                             ///
        /////////////////////////////////////////////////////////////////////////////////////////

        public static string RemoveWhitespace(string str)
        {
            return string.Join("", str.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
        }

        internal class Section_AutoSelect
        {
            public static Dictionary<string, string> settings;

            public static void Load(List<string> section)
            {
                settings = new Dictionary<string, string>();
                string l;
                foreach (string line in section)
                {
                    l = RemoveWhitespace(line);
                    string[] res = l.Split(':');
                    if (res.Length == 2)
                    {
                        settings.Add(res[0], res[1]);
                    }
                    else
                    {
                        Debug.Log("Error in ExtendedConfig.ProcessAutoSelectSection: unexpected line split: " + line + ", Setting Default Values.");
                        Set();
                        return;
                    }
                }
                ApplySettings();
            }

            public static void Save(StreamWriter w)
            {
                if (settings != null)
                {
                    foreach (var setting in settings)
                    {
                        if (setting.Key != null && setting.Value != null)
                        {
                            w.WriteLine("   " + setting.Key + ": " + setting.Value);
                        }
                    }
                }
            }

            // sets the values of the AutoSelect dictionary
            //  mirror = false   sets the default values
            //  mirror = true    sets the current MPAutoSelection values
            public static void Set(bool mirror = false)
            {
                settings = new Dictionary<string, string>();
                settings.Add("p_priority_0", mirror ? MPAutoSelection.PrimaryPriorityArray[0] : "THUNDERBOLT");
                settings.Add("p_priority_1", mirror ? MPAutoSelection.PrimaryPriorityArray[1] : "CYCLONE");
                settings.Add("p_priority_2", mirror ? MPAutoSelection.PrimaryPriorityArray[2] : "DRILLER");
                settings.Add("p_priority_3", mirror ? MPAutoSelection.PrimaryPriorityArray[3] : "IMPULSE");
                settings.Add("p_priority_4", mirror ? MPAutoSelection.PrimaryPriorityArray[4] : "FLAK");
                settings.Add("p_priority_5", mirror ? MPAutoSelection.PrimaryPriorityArray[5] : "CRUSHER");
                settings.Add("p_priority_6", mirror ? MPAutoSelection.PrimaryPriorityArray[6] : "LANCER");
                settings.Add("p_priority_7", mirror ? MPAutoSelection.PrimaryPriorityArray[7] : "REFLEX");
                settings.Add("s_priority_0", mirror ? MPAutoSelection.SecondaryPriorityArray[0] : "DEVASTATOR");
                settings.Add("s_priority_1", mirror ? MPAutoSelection.SecondaryPriorityArray[1] : "NOVA");
                settings.Add("s_priority_2", mirror ? MPAutoSelection.SecondaryPriorityArray[2] : "TIMEBOMB");
                settings.Add("s_priority_3", mirror ? MPAutoSelection.SecondaryPriorityArray[3] : "VORTEX");
                settings.Add("s_priority_4", mirror ? MPAutoSelection.SecondaryPriorityArray[4] : "HUNTER");
                settings.Add("s_priority_5", mirror ? MPAutoSelection.SecondaryPriorityArray[5] : "FALCON");
                settings.Add("s_priority_6", mirror ? MPAutoSelection.SecondaryPriorityArray[6] : "MISSILE_POD");
                settings.Add("s_priority_7", mirror ? MPAutoSelection.SecondaryPriorityArray[7] : "CREEPER");
                settings.Add("p_neverselect_0", mirror ? MPAutoSelection.PrimaryNeverSelect[0].ToString() : "false");
                settings.Add("p_neverselect_1", mirror ? MPAutoSelection.PrimaryNeverSelect[1].ToString() : "false");
                settings.Add("p_neverselect_2", mirror ? MPAutoSelection.PrimaryNeverSelect[2].ToString() : "false");
                settings.Add("p_neverselect_3", mirror ? MPAutoSelection.PrimaryNeverSelect[3].ToString() : "false");
                settings.Add("p_neverselect_4", mirror ? MPAutoSelection.PrimaryNeverSelect[4].ToString() : "false");
                settings.Add("p_neverselect_5", mirror ? MPAutoSelection.PrimaryNeverSelect[5].ToString() : "false");
                settings.Add("p_neverselect_6", mirror ? MPAutoSelection.PrimaryNeverSelect[6].ToString() : "false");
                settings.Add("p_neverselect_7", mirror ? MPAutoSelection.PrimaryNeverSelect[7].ToString() : "false");
                settings.Add("s_neverselect_0", mirror ? MPAutoSelection.SecondaryNeverSelect[0].ToString() : "false");
                settings.Add("s_neverselect_1", mirror ? MPAutoSelection.SecondaryNeverSelect[1].ToString() : "false");
                settings.Add("s_neverselect_2", mirror ? MPAutoSelection.SecondaryNeverSelect[2].ToString() : "false");
                settings.Add("s_neverselect_3", mirror ? MPAutoSelection.SecondaryNeverSelect[3].ToString() : "false");
                settings.Add("s_neverselect_4", mirror ? MPAutoSelection.SecondaryNeverSelect[4].ToString() : "false");
                settings.Add("s_neverselect_5", mirror ? MPAutoSelection.SecondaryNeverSelect[5].ToString() : "false");
                settings.Add("s_neverselect_6", mirror ? MPAutoSelection.SecondaryNeverSelect[6].ToString() : "false");
                settings.Add("s_neverselect_7", mirror ? MPAutoSelection.SecondaryNeverSelect[7].ToString() : "false");
                settings.Add("p_swap", mirror ? MPAutoSelection.primarySwapFlag.ToString() : "false");
                settings.Add("s_swap", mirror ? MPAutoSelection.secondarySwapFlag.ToString() : "false");
                settings.Add("dev_alert", mirror ? MPAutoSelection.zorc.ToString() : "true");
                settings.Add("reduced_hud", mirror ? MPAutoSelection.miasmic.ToString() : "false");
                settings.Add("swap_while_firing", mirror ? MPAutoSelection.swapWhileFiring.ToString() : "false");
                settings.Add("dont_autoselect_after_firing", mirror ? MPAutoSelection.dontAutoselectAfterFiring.ToString() : "false");
            }

            public static void ApplySettings()
            {
                try
                {
                    MPAutoSelection.PrimaryPriorityArray[0] = settings["p_priority_0"];
                    MPAutoSelection.PrimaryPriorityArray[1] = settings["p_priority_1"];
                    MPAutoSelection.PrimaryPriorityArray[2] = settings["p_priority_2"];
                    MPAutoSelection.PrimaryPriorityArray[3] = settings["p_priority_3"];
                    MPAutoSelection.PrimaryPriorityArray[4] = settings["p_priority_4"];
                    MPAutoSelection.PrimaryPriorityArray[5] = settings["p_priority_5"];
                    MPAutoSelection.PrimaryPriorityArray[6] = settings["p_priority_6"];
                    MPAutoSelection.PrimaryPriorityArray[7] = settings["p_priority_7"];
                    MPAutoSelection.SecondaryPriorityArray[0] = settings["s_priority_0"];
                    MPAutoSelection.SecondaryPriorityArray[1] = settings["s_priority_1"];
                    MPAutoSelection.SecondaryPriorityArray[2] = settings["s_priority_2"];
                    MPAutoSelection.SecondaryPriorityArray[3] = settings["s_priority_3"];
                    MPAutoSelection.SecondaryPriorityArray[4] = settings["s_priority_4"];
                    MPAutoSelection.SecondaryPriorityArray[5] = settings["s_priority_5"];
                    MPAutoSelection.SecondaryPriorityArray[6] = settings["s_priority_6"];
                    MPAutoSelection.SecondaryPriorityArray[7] = settings["s_priority_7"];
                    MPAutoSelection.PrimaryNeverSelect[0] = Convert.ToBoolean(settings["p_neverselect_0"]);
                    MPAutoSelection.PrimaryNeverSelect[1] = Convert.ToBoolean(settings["p_neverselect_1"]);
                    MPAutoSelection.PrimaryNeverSelect[2] = Convert.ToBoolean(settings["p_neverselect_2"]);
                    MPAutoSelection.PrimaryNeverSelect[3] = Convert.ToBoolean(settings["p_neverselect_3"]);
                    MPAutoSelection.PrimaryNeverSelect[4] = Convert.ToBoolean(settings["p_neverselect_4"]);
                    MPAutoSelection.PrimaryNeverSelect[5] = Convert.ToBoolean(settings["p_neverselect_5"]);
                    MPAutoSelection.PrimaryNeverSelect[6] = Convert.ToBoolean(settings["p_neverselect_6"]);
                    MPAutoSelection.PrimaryNeverSelect[7] = Convert.ToBoolean(settings["p_neverselect_7"]);
                    MPAutoSelection.SecondaryNeverSelect[0] = Convert.ToBoolean(settings["s_neverselect_0"]);
                    MPAutoSelection.SecondaryNeverSelect[1] = Convert.ToBoolean(settings["s_neverselect_1"]);
                    MPAutoSelection.SecondaryNeverSelect[2] = Convert.ToBoolean(settings["s_neverselect_2"]);
                    MPAutoSelection.SecondaryNeverSelect[3] = Convert.ToBoolean(settings["s_neverselect_3"]);
                    MPAutoSelection.SecondaryNeverSelect[4] = Convert.ToBoolean(settings["s_neverselect_4"]);
                    MPAutoSelection.SecondaryNeverSelect[5] = Convert.ToBoolean(settings["s_neverselect_5"]);
                    MPAutoSelection.SecondaryNeverSelect[6] = Convert.ToBoolean(settings["s_neverselect_6"]);
                    MPAutoSelection.SecondaryNeverSelect[7] = Convert.ToBoolean(settings["s_neverselect_7"]);
                    MPAutoSelection.primarySwapFlag = Convert.ToBoolean(settings["p_swap"]);
                    MPAutoSelection.secondarySwapFlag = Convert.ToBoolean(settings["s_swap"]);
                    MPAutoSelection.zorc = Convert.ToBoolean(settings["dev_alert"]);
                    MPAutoSelection.miasmic = Convert.ToBoolean(settings["reduced_hud"]);
                    MPAutoSelection.swapWhileFiring = Convert.ToBoolean(settings["swap_while_firing"]);
                    MPAutoSelection.dontAutoselectAfterFiring = Convert.ToBoolean(settings["dont_autoselect_after_firing"]);
                }
                catch (Exception ex)
                {
                    Debug.Log("Error while parsing AutoSelect settings. missing entry " + ex + "\nSetting Default values");
                    Set();
                    ApplySettings();
                }
            }

        }

        internal class Section_JoystickCurve
        {
            public static List<Controller> controllers = new List<Controller>();
            public static List<string> lines = new List<string>();

            public class Controller
            {
                public bool populated = false;
                public string name = "";
                public int id = -1;
                public List<Axis> axes = new List<Axis>();

                public class Axis
                {
                    public Vector2[] curve_points = new Vector2[4];
                    public float[] curve_lookup = new float[200];

                    public Vector2[] CloneCurvePoints()
                    {
                        return new Vector2[] {
                            new Vector2(curve_points[0].x,curve_points[0].y),
                            new Vector2(curve_points[1].x,curve_points[1].y),
                            new Vector2(curve_points[2].x,curve_points[2].y),
                            new Vector2(curve_points[3].x,curve_points[3].y)
                        };

                    }
                }
            }


            public class Type
            {
                public string name;
                public int amount;
                public int[] positions;

                public Type(string _name, int _amount, int[] _positions)
                {
                    name = _name;
                    amount = _amount;
                    positions = _positions;
                }
            }

            public static void Load(List<string> _section)
            {
                // Remove Whitespaces
                for (int i = 0; i < _section.Count; i++)
                {
                    if (!string.IsNullOrEmpty(_section[i]) && _section[i].Length > 3)
                        lines.Add(_section[i].Substring(3));
                    else
                        lines.Add(_section[i]);
                }

                SetDefault();
            }

            public static void ParseSectionData()
            {
                Debug.Log("\n- Loading Joystick Curves: ");
                SetDefault();

                // Save the current configuration in case we need to reset back to it when we encounter an incorrectable error
                List<Controller> copy_of_controllers = CloneCurrentControllerConfiguration();

                //PrintControllerNamesAndAlignment();
                try
                {
                    // read in the data of the devices 
                    int index = -1;
                    int.TryParse(lines[++index], out int numControllers); // read in the amount of controllers that are saved in this curve section
                    List<Controller> parsed_devices = new List<Controller>();
                    for (int i = 0; i < numControllers; i++){
                        // create an empty device and populate the name and id
                        Controller device = new Controller();
                        string[] device_informations = lines[++index].Split(';');
                        string controllerName = device_informations[0];
                        device.name = controllerName;
                        device.id = -1;
                        if (device_informations.Length == 2)
                        {
                            int.TryParse(device_informations[1], out device.id);
                        }

                        // read in the number of axes for this device and create empty axes for the device
                        int.TryParse(lines[++index], out int val2);
                        int numAxes = val2;
                        for (int g = 0; g < numAxes; g++) 
                            device.axes.Add(new Controller.Axis());

                        // populate device with default or saved curve points
                        for (int j = 0; j < numAxes; j++){
                            if (j >= device.axes.Count){
                                device.axes.Add(new Controller.Axis());
                            }
                            float value = 0f;
                            device.axes[j].curve_points = DefaultCurvePoints();
                            device.axes[j].curve_points[0].y = float.TryParse(lines[++index], out value) && value >= 0f && value <= 1f ? value : 0f;
                            device.axes[j].curve_points[1].x = float.TryParse(lines[++index], out value) && value >= 0f && value <= 1f ? value : 0.25f;
                            device.axes[j].curve_points[1].y = float.TryParse(lines[++index], out value) && value >= 0f && value <= 1f ? value : 0.25f;
                            device.axes[j].curve_points[2].x = float.TryParse(lines[++index], out value) && value >= 0f && value <= 1f ? value : 0.75f;
                            device.axes[j].curve_points[2].y = float.TryParse(lines[++index], out value) && value >= 0f && value <= 1f ? value : 0.75f;
                            device.axes[j].curve_points[3].y = float.TryParse(lines[++index], out value) && value >= 0f && value <= 1f ? value : 1f;
                            // generate the lookup table
                            device.axes[j].curve_lookup = ExtendedConfig.Section_JoystickCurve.GenerateCurveLookupTable(device.axes[j].curve_points);
                        }
                        //Debug.Log(" Added a device: "+device.name+" : "+ ControllerContainsData(device));
                        parsed_devices.Add(device);
                    }

                    // Find and assign the best fit to the current default configuration
                    // best fit base line = equal name, equal axis count
                    // best fit = matching or non '-1' id > contains data > exists
                    for ( int i = 0; i < Controls.m_controllers.Count; i++){
                        Debug.Log(i);
                        // establish base line and collect candidates
                        List<Controller> candidates = new List<Controller>();
                        foreach( Controller c in parsed_devices){
                            if (c.name.Equals(Controls.m_controllers[i].name) & c.axes.Count == Controls.m_controllers[i].m_axis_count)
                            {
                                Debug.Log(" Added a candidate");
                                candidates.Add(c);
                            }
                            else
                            {
                                Debug.Log(" "+ c.name+" : "+ Controls.m_controllers[i].name+", "+ c.axes.Count+" : "+ Controls.m_controllers[i].m_axis_count);
                            }
                                
                        }
                        // if there are no candidates that fit the baseline then leave this controller with default values
                        if (candidates.Count <= 0){
                            controllers[i].populated = true;
                            Debug.Log(" Set "+controllers[i].name + " to DEFAULT");
                            continue;
                        }
                        // if there are candidates then find the best fit among them
                        int best_score = 0;
                        int best_candidate = 0;
                        for(int x = 0; x < candidates.Count; x++){
                            int score = 0;
                            if (candidates[x].id == Controls.m_controllers[i].joystickID) score += 8;
                            if (ControllerContainsData(candidates[x])) score += 4;
                            if (candidates[x].id != -1) score += 2;
                            if( score > best_score){
                                best_score = score;
                                best_candidate = x;
                            }
                        }
                        // copy over the data
                        CopyController(candidates[best_candidate], controllers[i]);
                        controllers[i].id = Controls.m_controllers[i].joystickID;
                        controllers[i].populated = true;
                        parsed_devices.Remove(candidates[best_candidate]);

                    }

                }
                catch (Exception ex)
                {
                    Debug.Log("Error in ExtendedConfig.Section_JoystickCurve.Load:  " + ex + ", Setting Former Values.");
                    controllers = copy_of_controllers;
                }

                PrintControllerNamesAndAlignment();

                PrintJoystickCurves();
            }

            public static void CopyController(Controller data, Controller blank)
            {
                if (data == null | blank == null)
                    return;

                blank.id = data.id;
                blank.name = data.name;
                for(int i = 0; i < data.axes.Count; i++)
                {
                    blank.axes[i].curve_points = data.axes[i].CloneCurvePoints();
                    blank.axes[i].curve_lookup = ExtendedConfig.Section_JoystickCurve.GenerateCurveLookupTable(blank.axes[i].curve_points);
                }
            }

            public static bool ControllerContainsData( Controller c )
            {
                if(c == null)
                {
                    Debug.Log("[ExtendedConfig.Warning] ControllerContainsData received empty controller");
                    return false;
                }
                for( int i = 0; i < c.axes.Count; i++ ){
                    if (!IsAxisDefault(c.axes[i]))
                        return true;
                }
                return false;
            }

            public static bool IsAxisDefault( Controller.Axis axis )
            {
                return axis.curve_points[0].y == 0f
                     & axis.curve_points[1].x == 0.25f
                     & axis.curve_points[1].y == 0.25f
                     & axis.curve_points[2].x == 0.75f
                     & axis.curve_points[2].y == 0.75f
                     & axis.curve_points[3].y == 1f;
            }

            public static void PrintJoystickCurves()
            {
                foreach( Controller c in controllers )
                {
                    Debug.Log("\n"+c.name+" : "+c.id);
                    for(int i = 0; i < Mathf.Min(c.axes.Count, 6); i++)
                    {
                        Debug.Log("   axis: " 
                            + c.axes[i].curve_points[0].y + ", " 
                            + c.axes[i].curve_points[1].x + ", " 
                            + c.axes[i].curve_points[1].y + ", " 
                            + c.axes[i].curve_points[2].x + ", " 
                            + c.axes[i].curve_points[2].y + ", " 
                            + c.axes[i].curve_points[3].y);
                    }
                    Debug.Log("");
                }
            }

            public static void PrintControllerNamesAndAlignment()
            {
                int max = new[] { Controls.m_controllers.Count, Controllers.controllers.Count, controllers.Count }.Max();

                Debug.Log(string.Format(" {0, -50} | {1,-50} | {2,-50}", "Rewired", "Sensitivity/Deadzone", "Response-Curves"));
                string spacer = "";
                for (int i = 0; i < 156; i++)
                    spacer += "-";
                Debug.Log(spacer);
                for (int i = 0; i < max; i++)
                {
                    Debug.Log(string.Format(" {0, -50} | {1,-50} | {2,-50}", 
                        i < Controls.m_controllers.Count ? Controls.m_controllers[i].name + " : " + Controls.m_controllers[i].joystickID : "",
                        i < Controllers.controllers.Count ? Controllers.controllers[i].m_device_name + " : " + Controllers.controllers[i].m_joystick_id : "",
                        i < controllers.Count ? controllers[i].name + " : " + controllers[i].id : ""
                    ));
                }
                Debug.Log("");
            }

            public static List<Controller> CloneCurrentControllerConfiguration()
            {
                List<Controller>  copy_of_controllers = new List<Controller>();
                foreach (Controller c in controllers)
                {
                    copy_of_controllers.Add(new Controller
                    {
                        name = c.name,
                        id = c.id,
                        axes = new List<Controller.Axis>()
                    });

                    foreach (Controller.Axis axis in c.axes)
                    {
                        copy_of_controllers[copy_of_controllers.Count - 1].axes.Add(new Controller.Axis()
                        {
                            curve_points = axis.curve_points,
                            curve_lookup = axis.curve_lookup
                        });
                    }
                }
                return copy_of_controllers;
            }

            public static void Save(StreamWriter w)
            {
                try
                {
                    w.WriteLine(controllers.Count);
                    for (int i = 0; i < controllers.Count; i++)
                    {
                        w.WriteLine("   " + controllers[i].name + ";" + controllers[i].id);
                        w.WriteLine("   " + controllers[i].axes.Count);
                        for (int j = 0; j < controllers[i].axes.Count; j++)
                        {
                            w.WriteLine("      " + controllers[i].axes[j].curve_points[0].y);
                            w.WriteLine("      " + controllers[i].axes[j].curve_points[1].x);
                            w.WriteLine("      " + controllers[i].axes[j].curve_points[1].y);
                            w.WriteLine("      " + controllers[i].axes[j].curve_points[2].x);
                            w.WriteLine("      " + controllers[i].axes[j].curve_points[2].y);
                            w.WriteLine("      " + controllers[i].axes[j].curve_points[3].y);
                        }

                    }
                }
                catch (Exception ex)
                {
                    Debug.Log("Error in ExtendedConfig.Section_JoystickCurve.Save(): \n" + ex);
                }
            }

            public static void SetDefault()
            {
                controllers.Clear();
                for (int i = 0; i < Controls.m_controllers.Count; i++)
                {
                    controllers.Add(new Controller
                    {
                        populated = false,
                        name = Controls.m_controllers[i].name,
                        id = Controls.m_controllers[i].joystickID,
                        axes = new List<Controller.Axis>()
                    });
                    for (int j = 0; j < Controls.m_controllers[i].m_axis_count; j++)
                    {
                        controllers[i].axes.Add(new Controller.Axis()
                        {
                            curve_points = DefaultCurvePoints(),
                            curve_lookup = GenerateCurveLookupTable(DefaultCurvePoints())
                        });
                    }
                }
            }

            public static Vector2[] DefaultCurvePoints()
            {
                return new Vector2[] { Vector2.zero, new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f), Vector2.one };
            }

            public static float[] GenerateCurveLookupTable(Vector2[] points)
            {
                if (points.Length != 4 || points[0] == null || points[1] == null || points[2] == null || points[3] == null)
                {
                    Debug.Log("ExtendedConfig.GenerateCurveLookupTable: invalid argument");
                    points = DefaultCurvePoints();
                }


                // generate initial curve
                Vector2[] curve = new Vector2[200];
                float t;
                for (int i = 0; i < 200; i++)
                {
                    t = 1f / 200f * i;
                    curve[i] = new Vector2(
                       JoystickCurveEditor.CubicBezierAxisForT(t, points[0].x, points[1].x, points[2].x, points[3].x),
                       JoystickCurveEditor.CubicBezierAxisForT(t, points[0].y, points[1].y, points[2].y, points[3].y)
                       );
                }

                // normalize the initial curve for slightly faster lookup and constant distribution
                float[] normalized = new float[200];
                for (int i = 0; i < 200; i++)
                {
                    float x = i * 0.005f;
                    int k = 1;
                    while (curve[k].x <= x && k < 199) k++;

                    normalized[i] = curve[k - 1].y + (x - curve[k - 1].x) / (curve[k].x - curve[k - 1].x) * (curve[k].y - curve[k - 1].y);
                }
                //string debug = "";
                //foreach (float f in normalized) debug += "," + f.ToString();
                //Debug.Log("NORMALIZED AXIS: \n" + debug);
                return normalized;
            }
        }

        internal class Section_WeaponCycling
        {
            public static Dictionary<string, string> settings;

            public static void Load(List<string> section)
            {
                MPWeaponCycling.UpdateWeaponOrder();

                settings = new Dictionary<string, string>();
                string l;
                foreach (string line in section)
                {
                    l = RemoveWhitespace(line);
                    string[] res = l.Split(':');
                    if (res.Length == 2)
                    {
                        settings.Add(res[0], res[1]);
                    }
                    else
                    {
                        Debug.Log("Error in ExtendedConfig.ProcessWeaponCyclingSection: unexpected line split: " + line + ", Setting Default Values.");
                        Set();
                        return;
                    }
                }
                ApplySettings();
            }

            public static void Save(StreamWriter w)
            {
                if (settings != null)
                {
                    foreach (var setting in settings)
                    {
                        if (setting.Key != null && setting.Value != null)
                        {
                            w.WriteLine("   " + setting.Key + ": " + setting.Value);
                        }
                    }
                }
            }

            // sets the values of the AutoSelect dictionary
            //  mirror = false   sets the default values
            //  mirror = true    sets the current MPWeaponCycling values
            public static void Set(bool mirror = false)
            {


                settings = new Dictionary<string, string>();
                settings.Add("p_cycle_0", mirror ? MPWeaponCycling.CPrimaries[0].ToString() : "true");
                settings.Add("p_cycle_1", mirror ? MPWeaponCycling.CPrimaries[1].ToString() : "true");
                settings.Add("p_cycle_2", mirror ? MPWeaponCycling.CPrimaries[2].ToString() : "true");
                settings.Add("p_cycle_3", mirror ? MPWeaponCycling.CPrimaries[3].ToString() : "true");
                settings.Add("p_cycle_4", mirror ? MPWeaponCycling.CPrimaries[4].ToString() : "true");
                settings.Add("p_cycle_5", mirror ? MPWeaponCycling.CPrimaries[5].ToString() : "true");
                settings.Add("p_cycle_6", mirror ? MPWeaponCycling.CPrimaries[6].ToString() : "true");
                settings.Add("p_cycle_7", mirror ? MPWeaponCycling.CPrimaries[7].ToString() : "true");
                settings.Add("s_cycle_0", mirror ? MPWeaponCycling.CSecondaries[0].ToString() : "true");
                settings.Add("s_cycle_1", mirror ? MPWeaponCycling.CSecondaries[1].ToString() : "true");
                settings.Add("s_cycle_2", mirror ? MPWeaponCycling.CSecondaries[2].ToString() : "true");
                settings.Add("s_cycle_3", mirror ? MPWeaponCycling.CSecondaries[3].ToString() : "true");
                settings.Add("s_cycle_4", mirror ? MPWeaponCycling.CSecondaries[4].ToString() : "true");
                settings.Add("s_cycle_5", mirror ? MPWeaponCycling.CSecondaries[5].ToString() : "true");
                settings.Add("s_cycle_6", mirror ? MPWeaponCycling.CSecondaries[6].ToString() : "true");
                settings.Add("s_cycle_7", mirror ? MPWeaponCycling.CSecondaries[7].ToString() : "true");
            }

            public static void ApplySettings()
            {
                try
                {
                    MPWeaponCycling.CPrimaries[0] = Convert.ToBoolean(settings["p_cycle_0"]);
                    MPWeaponCycling.CPrimaries[1] = Convert.ToBoolean(settings["p_cycle_1"]);
                    MPWeaponCycling.CPrimaries[2] = Convert.ToBoolean(settings["p_cycle_2"]);
                    MPWeaponCycling.CPrimaries[3] = Convert.ToBoolean(settings["p_cycle_3"]);
                    MPWeaponCycling.CPrimaries[4] = Convert.ToBoolean(settings["p_cycle_4"]);
                    MPWeaponCycling.CPrimaries[5] = Convert.ToBoolean(settings["p_cycle_5"]);
                    MPWeaponCycling.CPrimaries[6] = Convert.ToBoolean(settings["p_cycle_6"]);
                    MPWeaponCycling.CPrimaries[7] = Convert.ToBoolean(settings["p_cycle_7"]);
                    MPWeaponCycling.CSecondaries[0] = Convert.ToBoolean(settings["s_cycle_0"]);
                    MPWeaponCycling.CSecondaries[1] = Convert.ToBoolean(settings["s_cycle_1"]);
                    MPWeaponCycling.CSecondaries[2] = Convert.ToBoolean(settings["s_cycle_2"]);
                    MPWeaponCycling.CSecondaries[3] = Convert.ToBoolean(settings["s_cycle_3"]);
                    MPWeaponCycling.CSecondaries[4] = Convert.ToBoolean(settings["s_cycle_4"]);
                    MPWeaponCycling.CSecondaries[5] = Convert.ToBoolean(settings["s_cycle_5"]);
                    MPWeaponCycling.CSecondaries[6] = Convert.ToBoolean(settings["s_cycle_6"]);
                    MPWeaponCycling.CSecondaries[7] = Convert.ToBoolean(settings["s_cycle_7"]);
                }
                catch (Exception ex)
                {
                    Debug.Log("Error while parsing WeaponCycling settings. missing entry " + ex + "\nSetting Default values");
                    Set();
                    ApplySettings();
                }
            }

        }

        internal class Section_AudiotauntKeybinds
        {
            public static void Load(List<string> section)
            {
                string l;
                for (int i = 0; i < section.Count; i++)
                {
                    if (i < MPAudioTaunts.AMOUNT_OF_TAUNTS_PER_CLIENT)
                    {
                        l = RemoveWhitespace(section[i]);
                        int.TryParse(l, out int val);

                        if (val >= -1)
                        {
                            MPAudioTaunts.AClient.keybinds[i] = val;
                        }
                    }

                }
            }

            public static void Save(StreamWriter w)
            {
                for (int i = 0; i < MPAudioTaunts.AMOUNT_OF_TAUNTS_PER_CLIENT; i++)
                {
                    w.WriteLine("   " + MPAudioTaunts.AClient.keybinds[i]);
                }
            }

            // -1 = no keycode set
            public static void SetDefaultKeybinds()
            {
                for (int i = 0; i < MPAudioTaunts.AClient.keybinds.Length; i++)
                {
                    MPAudioTaunts.AClient.keybinds[i] = -1;
                }
            }
        }

        internal class Section_AudiotauntSelectedTaunts
        {
            public static void Load(List<string> section){  
                string hashes = "", l = "";
                for (int i = 0; i < section.Count; i++){
                    if (i != 0)
                        hashes += "/";
                    if (i < MPAudioTaunts.AMOUNT_OF_TAUNTS_PER_CLIENT){
                        l = RemoveWhitespace(section[i]);
                        hashes += l;
                    }
                }
                MPAudioTaunts.AClient.loaded_local_taunts = hashes;
                MPAudioTaunts.AClient.LoadLocalAudioTauntsFromPilotPrefs();
            }

            public static void Save(StreamWriter w){
                for (int i = 0; i < MPAudioTaunts.AMOUNT_OF_TAUNTS_PER_CLIENT; i++)
                    if (MPAudioTaunts.AClient.local_taunts.Length > i && MPAudioTaunts.AClient.local_taunts[i] != null)
                        w.WriteLine("   " + MPAudioTaunts.AClient.local_taunts[i].hash);
            }
        }

        internal class Section_AudiotauntMutedPlayers
        {
            public static HashSet<string> ids = new HashSet<string>();

            public static void Load(List<string> section)
            {
                for (int i = 0; i < section.Count; i++)
                   ids.Add(RemoveWhitespace(section[i]));
            }

            public static void Save(StreamWriter w)
            {
                foreach(string id in ids)
                    w.WriteLine("   " + id);
            }

        }

    }
}

