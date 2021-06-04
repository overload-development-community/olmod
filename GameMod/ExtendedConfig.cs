using Harmony;
using Overload;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
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


        // On Game Loading or when selecting a different PILOT read or generate PILOT.extendedconfig
        [HarmonyPatch(typeof(PilotManager), "Select", new Type[] { typeof(string) })]
        internal class ExtendedConfig_PilotManager_Select
        {
            public static void Postfix( string name )
            {
                SetDefaultConfig();
                string filepath = Path.Combine(Application.persistentDataPath, name + file_extension);
                if ( File.Exists(filepath) )
                {
                    ReadConfigData(filepath);
                }
                ApplyConfigData();
            }
        }

        // reads all lines and passes them to their respective functions to process them 
        // unknown sections get stored in ExtendedConfig.unknown_lines to reattach them to the end when saving
        private static void ReadConfigData( string filepath )
        {
            using (StreamReader sr = new StreamReader(filepath))
            {
                unknown_sections = new List<string>();
                List<string> current_section = new List<string>();
                string line = String.Empty;
                string current_section_id = "unknown";

                while ((line = sr.ReadLine()) != null)
                {

                    if( line.StartsWith("[SECTION:"))
                    {
                        if( known_sections.Contains(line) )
                        {
                            current_section_id = line;
                        }
                        else
                        {
                            current_section_id = "unknown";
                            unknown_sections.Add(line);
                        }
                    }
                    else if( line.Equals("[/END]") )
                    {
                        if (!current_section_id.Equals("unknown"))
                        {
                            PassSectionToFunction(current_section, current_section_id);
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
        }

        [HarmonyPatch(typeof(PilotManager), "SavePreferences")]
        internal class ExtendedConfig_PilotManager_SavePreferences
        {
            public static void Postfix()
            {
                SaveActivePilot();
            }
        }
        
        [HarmonyPatch(typeof(Controls), "SaveControlData")]
        internal class ExtendedConfig_Controls_SaveControlData
        {
            public static void Postfix()
            {
                SaveActivePilot();
            }
        }

        [HarmonyPatch(typeof(PilotManager), "Create")]
        internal class ExtendedConfig_PilotManager_Create
        {
            public static void Prefix()
            {
                SaveActivePilot();
            }

            public static void Postfix(string name, bool copy_prefs = false, bool copy_config = false)
            {
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
                if (filename.EndsWith(".xprefs"))
                {
                    Platform.DeleteUserData(filename.Replace(".xprefs", file_extension));
                }
            }
        }




        /////////////////////////////////////////////////////////////////////////////////////////
        ///                           Add new Sections here:                                  ///
        /////////////////////////////////////////////////////////////////////////////////////////

        private static List<string> known_sections = new List<string> {
            "[SECTION: AUTOSELECT]",

        };
        

        private static void PassSectionToFunction(List<string> section, string section_name)
        {
            if (section_name.Equals("[SECTION: AUTOSELECT]"))
            {
                Section_AutoSelect.Load(section);
            }



        }

        public static void SaveActivePilot()
        {
            string filepath = Path.Combine(Application.persistentDataPath, PilotManager.ActivePilot + file_extension);
            using (StreamWriter w = File.CreateText(filepath))
            {
                w.WriteLine("[SECTION: AUTOSELECT]");
                Section_AutoSelect.Save(w);
                w.WriteLine("[/END]");




                foreach (string line in unknown_sections)
                {
                    w.WriteLine(line);
                }
            }
        }

        private static void SetDefaultConfig()
        {
            Section_AutoSelect.Set();

        }

        public static void ApplyConfigData()
        {
            Section_AutoSelect.ApplySettings();

        }



        /////////////////////////////////////////////////////////////////////////////////////////
        ///                           SECTION SPECIFIC FUNCTIONS:                             ///
        /////////////////////////////////////////////////////////////////////////////////////////

        internal class Section_AutoSelect
        {
            public static Dictionary<string, string> settings;

            public static void Load(List<string> settings)
            {
                Section_AutoSelect.settings = new Dictionary<string, string>();
                string l;
                foreach (string line in settings)
                {
                    l = RemoveWhitespace(line);
                    string[] res = l.Split(':');
                    if (res.Length == 2)
                    {
                        Section_AutoSelect.settings.Add(res[0], res[1]);
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
            public static void Set( bool mirror = false )
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
                catch( Exception ex )
                {
                    Debug.Log("Error while parsing AutoSelect settings. missing entry "+ex+"\nSetting Default values");
                    Set();
                    ApplySettings();
                }
            }

            public static string RemoveWhitespace(string str)
            {
                return string.Join("", str.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
            }
        }



    }
}

