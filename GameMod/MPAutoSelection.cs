using System;
using System.IO;
using System.Timers;
using Harmony;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod
{
    class MPAutoSelection
    {

        [HarmonyPatch(typeof(GameManager), "Start")]
        internal class CommandsAndInitialisationPatch
        {
            private static void Postfix(GameManager __instance)
            {
                uConsole.RegisterCommand("toggleprimaryorder", "toggles all Weapon Selection logic related to primary weapons", new uConsole.DebugCommand(CommandsAndInitialisationPatch.CmdTogglePrimary));
                uConsole.RegisterCommand("togglesecondaryorder", "toggles all Weapon Selection logic related to secondary weapons", new uConsole.DebugCommand(CommandsAndInitialisationPatch.CmdToggleSecondary));
                uConsole.RegisterCommand("toggle_hud", "Toggles some HUD elements", new uConsole.DebugCommand(CommandsAndInitialisationPatch.CmdToggleHud));

                Initialise();
            }

            // COMMANDS
            private static void CmdToggleHud()
            {
                miasmic = !miasmic;
                uConsole.Log("Toggled HUD! current state : " + miasmic);
                MPAutoSelectionUI.DrawMpAutoselectOrderingScreen.saveToFile();
            }

            private static void CmdTogglePrimary()
            {
                primarySwapFlag = !primarySwapFlag;
                uConsole.Log("[AS] Primary weapon swapping: " + primarySwapFlag);
                MPAutoSelectionUI.DrawMpAutoselectOrderingScreen.saveToFile();
            }

            private static void CmdToggleSecondary()
            {
                secondarySwapFlag = !secondarySwapFlag;
                uConsole.Log("[AS] Secondary weapon swapping: " + secondarySwapFlag);
                MPAutoSelectionUI.DrawMpAutoselectOrderingScreen.saveToFile();
            }
        }





        [HarmonyPatch(typeof(UIElement), "DrawHUDArmor")]
        internal class MaybeDrawHUDElement1
        {
            public static bool Prefix(UIElement __instance)
            {
                return !miasmic;
            }
        }

        [HarmonyPatch(typeof(UIElement), "DrawHUDEnergyAmmo")]
        internal class MaybeDrawHUDElement2
        {
            public static bool Prefix()
            {
                return !miasmic;
            }
        }

        [HarmonyPatch(typeof(UIElement), "DrawHUDIndicators")]
        internal class MaybeDrawHUDElement3
        {
            public static bool Prefix(UIElement __instance)
            {
                return !miasmic;
            }
        }







        // Keeps track of what entry point was used to reach the customize menu
        [HarmonyPatch(typeof(UIElement), "DrawMpPreMatchMenu")]
        internal class ClientInLobby
        {
            public static void Postfix()
            {
                isCurrentlyInLobby = true;
            }
        }
        // Keeps track of what entry point was used to reach the customize menu
        [HarmonyPatch(typeof(UIElement), "DrawMpMenu")]
        internal class ClientNotInLobby
        {
            public static void Postfix()
            {
                isCurrentlyInLobby = false;
            }
        }








        /////////////////////////////////////////////////////////////////////////////////////
        //              INITIALISATION AND FETCH SETTINGS / PREFERENCES                  
        /////////////////////////////////////////////////////////////////////////////////////
        public static void Initialise()
        {
            MenuManager.opt_primary_autoswitch = 0;
            if (File.Exists(textFile))
            {
                readContent();
            }
            else
            {

                Debug.Log("-AUTOSELECT- [ERROR] File does not exist. Creating default priority list");
                createDefaultPriorityFile();
                readContent();
            }
            isInitialised = true;
        }

        private static void createDefaultPriorityFile()
        {
            using (StreamWriter sw = File.CreateText(textFile))
            {
                sw.WriteLine("THUNDERBOLT");
                sw.WriteLine("CYCLONE");
                sw.WriteLine("DRILLER");
                sw.WriteLine("IMPULSE");
                sw.WriteLine("FLAK");
                sw.WriteLine("CRUSHER");
                sw.WriteLine("LANCER");
                sw.WriteLine("REFLEX");
                sw.WriteLine("DEVASTATOR");
                sw.WriteLine("NOVA");
                sw.WriteLine("TIMEBOMB");
                sw.WriteLine("HUNTER");
                sw.WriteLine("VORTEX");
                sw.WriteLine("FALCON");
                sw.WriteLine("MISSILE_POD");
                sw.WriteLine("CREEPER");
                sw.WriteLine(PrimaryNeverSelect[0]);
                sw.WriteLine(PrimaryNeverSelect[1]);
                sw.WriteLine(PrimaryNeverSelect[2]);
                sw.WriteLine(PrimaryNeverSelect[3]);
                sw.WriteLine(PrimaryNeverSelect[4]);
                sw.WriteLine(PrimaryNeverSelect[5]);
                sw.WriteLine(PrimaryNeverSelect[6]);
                sw.WriteLine(PrimaryNeverSelect[7]);
                sw.WriteLine(SecondaryNeverSelect[0]);
                sw.WriteLine(SecondaryNeverSelect[1]);
                sw.WriteLine(SecondaryNeverSelect[2]);
                sw.WriteLine(SecondaryNeverSelect[3]);
                sw.WriteLine(SecondaryNeverSelect[4]);
                sw.WriteLine(SecondaryNeverSelect[5]);
                sw.WriteLine(SecondaryNeverSelect[6]);
                sw.WriteLine(SecondaryNeverSelect[7]);
                sw.WriteLine(MPAutoSelection.primarySwapFlag);
                sw.WriteLine(MPAutoSelection.secondarySwapFlag);
                sw.WriteLine("False");//MPAutoSelection.COswapToHighest);
                sw.WriteLine("False");//MPAutoSelection.patchPrevNext);
                sw.WriteLine(MPAutoSelection.zorc);
                sw.WriteLine(MPAutoSelection.miasmic);
            }
        }

        private static bool stringToBool(string b)
        {
            if (b == "True")
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private static void readContent()
        {
            using (StreamReader file = new StreamReader(textFile))
            {
                int counter = 0;
                string ln;



                while ((ln = file.ReadLine()) != null)
                {
                    /// Contains the priorities of the primary weapons
                    if (counter < 8)
                    {
                        if (ln == "THUNDERBOLT" | ln == "IMPULSE" | ln == "CYCLONE" | ln == "DRILLER" | ln == "LANCER" | ln == "REFLEX" | ln == "FLAK" | ln == "CRUSHER")
                        {
                            PrimaryPriorityArray[counter] = ln;
                        }
                        else
                        {
                            Debug.Log("-AUTOORDER- [ERROR](1) unexpected line content -> (content: " + ln + " )");
                            return;
                        }

                    }

                    /// Contains the priorities of the secondary weapons
                    else if (counter < 16)
                    {
                        if (ln == "DEVASTATOR" | ln == "TIMEBOMB" | ln == "VORTEX" | ln == "NOVA" | ln == "HUNTER" | ln == "FALCON" | ln == "CREEPER" | ln == "MISSILE_POD")
                        {
                            SecondaryPriorityArray[counter - 8] = ln;
                        }
                        else
                        {
                            Debug.Log("-AUTOORDER- [ERROR](2) unexpected line content -> (content: " + ln + " )");
                            return;
                        }
                    }

                    /// Contains true/false whether primary priorities are neverselected
                    else if (counter < 24)
                    {
                        if (ln == "True" || ln == "False")
                        {
                            PrimaryNeverSelect[counter - 16] = stringToBool(ln);
                        }
                        else
                        {
                            for (int i = 0; i < 8; i++)
                            {
                                PrimaryNeverSelect[i] = false;
                            }
                        }
                    }
                    /// Contains true/false whether secondary priorities are neverselected
                    else if (counter < 32)
                    {
                        if (ln == "True" || ln == "False")
                        {
                            SecondaryNeverSelect[counter - 24] = stringToBool(ln);
                        }
                        else
                        {
                            for (int i = 0; i < 8; i++)
                            {
                                SecondaryNeverSelect[i] = false;
                            }
                        }
                    }
                    else if (counter == 32)
                    {
                        if (ln == "True" || ln == "False") { primarySwapFlag = stringToBool(ln); }
                    }
                    else if (counter == 33)
                    {
                        if (ln == "True" || ln == "False") { secondarySwapFlag = stringToBool(ln); }
                    }
                    else if (counter == 34)
                    {
                        //if (ln == "True" || ln == "False") { COswapToHighest = stringToBool(ln); }
                    }
                    else if (counter == 35)
                    {
                        //if (ln == "True" || ln == "False") { patchPrevNext = stringToBool(ln); }
                    }
                    else if (counter == 36)
                    {
                        if (ln == "True" || ln == "False") { zorc = stringToBool(ln); }
                    }
                    else if (counter == 37)
                    {
                        if (ln == "True" || ln == "False") { miasmic = stringToBool(ln); }
                    }

                    else
                    {
                        Debug.Log("-AUTOORDER- [ERROR](3) unexpected line content -> (content: " + ln + " : " + counter + " )");

                        return;
                    }
                    counter++;
                }
                file.Close();

            }
        }

















        /////////////////////////////////////////////////////////////////////////////////////
        //              PRIMARY WEAPONS CHAIN                   
        /////////////////////////////////////////////////////////////////////////////////////

        public static bool isThereAmmoForThisPrimary(string weapon, Player local)
        {
            foreach (string ele in EnergyWeapons)
            {
                if (ele.Equals(weapon)) return local.m_energy > 0;
            }
            foreach (string ele in AmmoWeapons)
            {
                if (ele.Equals(weapon)) return local.m_ammo > 0;
            }
            return false;
        }


        

        public static void maybeSwapPrimary(bool silent = false)
        {
            //is there even a potential static option to switch to
            if (areThereAllowedPrimaries())
            {
                // case 0: energy but no ammo
                if (GameManager.m_local_player.m_energy > 0 && !(GameManager.m_local_player.m_ammo > 0))
                {
                    //is there an unlocked energy weapon
                    string[] candidates = returnArrayOfUnlockedPrimaries(EnergyWeapons);
                    if (candidates.Length > 0)
                    {
                        string a = returnHighestPrimary(candidates);
                        if (!a.Equals("a")) swapToWeapon(a, silent);
                    }
                    else
                    {
                        swap_failed = true;
                    }
                    return;
                }
                // case 1: ammo but no energy
                if (!(GameManager.m_local_player.m_energy > 0) && GameManager.m_local_player.m_ammo > 0)
                {
                    //is there an unlocked ammo weapon
                    string[] candidates = returnArrayOfUnlockedPrimaries(AmmoWeapons);
                    if (candidates.Length > 0)
                    {
                        string a = returnHighestPrimary(candidates);
                        if (!a.Equals("a")) swapToWeapon(a, silent);
                    }
                    else
                    {
                        swap_failed = true;
                    }
                    return;
                }
                // case 2: ammo and energy
                //give me the highest unlocked weapon
                string[] candidates1 = returnArrayOfUnlockedPrimaries(PrimaryPriorityArray);
                if (candidates1.Length > 0)
                {
                    string a = returnHighestPrimary(candidates1);
                    if (!a.Equals("a")) swapToWeapon(a, silent);
                }
                return;
            }
        }

        private static bool areThereAllowedPrimaries()
        {
            for (int i = 0; i < 8; i++)
            {
                if (PrimaryNeverSelect[i] == false) return true;
            }
            return false;
        }

        private static string[] returnArrayOfUnlockedPrimaries(string[] arr)
        {
            int len = arr.Length;
            if (len > 0)
            {
                int counter = 0;
                string[] temp = new string[len];
                for (int i = 0; i < len; i++)
                {
                    if (isWeaponAccessibleAndNotNeverselect(arr[i]))
                    {
                        temp[counter] = arr[i];
                        counter++;
                    }
                }
                string[] result = new string[counter];
                for (int j = 0; j < counter; j++)
                {
                    result[j] = temp[j];
                }
                return result;
            }
            return new string[0];
        }

        private static bool isWeaponAccessibleAndNotNeverselect(string weapon)
        {
            if (weapon.Equals("IMPULSE")) return !(GameManager.m_local_player.m_weapon_level[0].ToString().Equals("LOCKED")) && !isPrimaryOnNeverSelectList("IMPULSE");
            if (weapon.Equals("CYCLONE")) return !(GameManager.m_local_player.m_weapon_level[1].ToString().Equals("LOCKED")) && !isPrimaryOnNeverSelectList("CYCLONE");
            if (weapon.Equals("REFLEX")) return !(GameManager.m_local_player.m_weapon_level[2].ToString().Equals("LOCKED")) && !isPrimaryOnNeverSelectList("REFLEX");
            if (weapon.Equals("CRUSHER")) return !(GameManager.m_local_player.m_weapon_level[3].ToString().Equals("LOCKED")) && !isPrimaryOnNeverSelectList("CRUSHER");
            if (weapon.Equals("DRILLER")) return !(GameManager.m_local_player.m_weapon_level[4].ToString().Equals("LOCKED")) && !isPrimaryOnNeverSelectList("DRILLER");
            if (weapon.Equals("FLAK")) return !(GameManager.m_local_player.m_weapon_level[5].ToString().Equals("LOCKED")) && !isPrimaryOnNeverSelectList("FLAK");
            if (weapon.Equals("THUNDERBOLT")) return !(GameManager.m_local_player.m_weapon_level[6].ToString().Equals("LOCKED")) && !isPrimaryOnNeverSelectList("THUNDERBOLT");
            if (weapon.Equals("LANCER")) return !(GameManager.m_local_player.m_weapon_level[7].ToString().Equals("LOCKED")) && !isPrimaryOnNeverSelectList("LANCER");
            else
            {
                return false;
            }
        }

        private static bool isPrimaryOnNeverSelectList(string weapon)
        {
            for (int i = 0; i < 8; i++)
            {
                if (weapon.Equals(PrimaryPriorityArray[i])) return PrimaryNeverSelect[i];
            }
            return false;
        }

        private static string returnHighestPrimary(string[] arr)
        {
            for (int i = 0; i < 8; i++)
            {
                foreach (string sel in arr)
                {
                    if (sel.Equals(PrimaryPriorityArray[i])) return PrimaryPriorityArray[i];
                }
            }
            return "a";

        }

        private static void swapToWeapon(string weaponName, bool silent = false)
        {
            if (!(GameManager.m_local_player.m_weapon_type.Equals(stringToWeaponType(weaponName))))
            {
                GameManager.m_local_player.Networkm_weapon_type = stringToWeaponType(weaponName);
                GameManager.m_local_player.CallCmdSetCurrentWeapon(GameManager.m_local_player.m_weapon_type);


                if (GameManager.m_game_state != GameManager.GameState.GAMEPLAY)
                {
                    return;
                }

                UIElement.WEAPON_SELECT_FLASH = 1.25f;
                UIElement.WEAPON_SELECT_NAME = string.Format(Loc.LS("{0} SELECTED"), Player.WeaponNames[GameManager.m_local_player.m_weapon_type]);
                if (!silent)
                {
                    SFXCueManager.PlayCue2D(SFXCue.hud_cycle_typeA1, 1f, 0f, 0f, false);
                    GameManager.m_audio.PlayCue2D(363, 0.1f, 0f, 0f, false);
                    GameManager.m_local_player.c_player_ship.SetRefireDelayAfterWeaponSwitch();
                }

                GameManager.m_local_player.c_player_ship.m_thunder_power = 0f;
                GameManager.m_local_player.c_player_ship.SwitchVisibleWeapon(false, WeaponType.NUM);
            }
            if (!silent)
            {
                SFXCueManager.PlayRawSoundEffect2D(SoundEffect.hud_notify_message1, 1f, 0.15f, 0.1f, false);
            }
        }

        private static WeaponType stringToWeaponType(string weapon)
        {
            if (weapon.Equals("IMPULSE")) return WeaponType.IMPULSE;
            if (weapon.Equals("CYCLONE")) return WeaponType.CYCLONE;
            if (weapon.Equals("REFLEX")) return WeaponType.REFLEX;
            if (weapon.Equals("CRUSHER")) return WeaponType.CRUSHER;
            if (weapon.Equals("DRILLER")) return WeaponType.DRILLER;
            if (weapon.Equals("FLAK")) return WeaponType.FLAK;
            if (weapon.Equals("THUNDERBOLT")) return WeaponType.THUNDERBOLT;
            if (weapon.Equals("LANCER")) return WeaponType.LANCER;
            else
            {
                return WeaponType.NUM;
            }
        }

        private static string WeaponTypeToString(WeaponType weapon)
        {
            if (weapon == WeaponType.IMPULSE) return "IMPULSE";
            if (weapon == WeaponType.CYCLONE) return "CYCLONE";
            if (weapon == WeaponType.REFLEX) return "REFLEX";
            if (weapon == WeaponType.CRUSHER) return "CRUSHER";
            if (weapon == WeaponType.DRILLER) return "DRILLER";
            if (weapon == WeaponType.FLAK) return "FLAK";
            if (weapon == WeaponType.THUNDERBOLT) return "THUNDERBOLT";
            if (weapon == WeaponType.LANCER) return "LANCER";
            if (weapon == WeaponType.NUM) return "NUM";
            else
            {
                uConsole.Log("fired an end");
                return "end";
            }
        }


        public static int getWeaponPriority(WeaponType primary)
        {
            if (MPAutoSelection.isInitialised)
            {
                string wea = primary.ToString();

                for (int i = 0; i < 8; i++)
                {
                    if (wea.Equals(PrimaryPriorityArray[i]))
                    {
                        return i;
                    }
                    if (wea.Equals("CRUSHER") && PrimaryPriorityArray[i].Equals("CRUSHER"))
                    {
                        return i;
                    }
                }
                uConsole.Log("-AUTOSELECT- [WARN]: getWeaponPriority:-1, primary wasnt in array");
                return -1;
            }
            else
            {
                uConsole.Log("-AUTOSELECT- [WARN]: getWeaponPriority:-1, priority didnt get initialised");
                return -1;
            }
        }


        /////////////////////////////////////////////////////////////////////////////////////
        //              MISSILES                 
        /////////////////////////////////////////////////////////////////////////////////////
        public static int getWeaponIndex(string weapon)
        {
            if (weapon.Equals("IMPULSE") || weapon.Equals("FALCON")) return 0;
            if (weapon.Equals("CYCLONE") || weapon.Equals("MISSILE_POD")) return 1;
            if (weapon.Equals("REFLEX") || weapon.Equals("HUNTER")) return 2;
            if (weapon.Equals("CRUSHER") || weapon.Equals("CREEPER")) return 3;
            if (weapon.Equals("DRILLER") || weapon.Equals("NOVA")) return 4;
            if (weapon.Equals("FLAK") || weapon.Equals("DEVASTATOR")) return 5;
            if (weapon.Equals("THUNDERBOLT") || weapon.Equals("TIMEBOMB")) return 6;
            if (weapon.Equals("LANCER") || weapon.Equals("VORTEX")) return 7;
            else
            {
                uConsole.Log("-AUTOORDERSELECT- [ERROR] getWeaponIndex didnt recognise the given weapon string");
                return 0;
            }
        }

        private static bool isMissileAccessibleAndNotNeverselect(string weapon)
        {
            if (weapon.Equals("FALCON")) return !(GameManager.m_local_player.m_missile_level[0].ToString().Equals("LOCKED")) && !isSecondaryOnNeverSelectList("FALCON");
            if (weapon.Equals("MISSILE_POD")) return !(GameManager.m_local_player.m_missile_level[1].ToString().Equals("LOCKED")) && !isSecondaryOnNeverSelectList("MISSILE_POD");
            if (weapon.Equals("HUNTER")) return !(GameManager.m_local_player.m_missile_level[2].ToString().Equals("LOCKED")) && !isSecondaryOnNeverSelectList("HUNTER");
            if (weapon.Equals("CREEPER")) return !(GameManager.m_local_player.m_missile_level[3].ToString().Equals("LOCKED")) && !isSecondaryOnNeverSelectList("CREEPER");
            if (weapon.Equals("NOVA")) return !(GameManager.m_local_player.m_missile_level[4].ToString().Equals("LOCKED")) && !isSecondaryOnNeverSelectList("NOVA");
            if (weapon.Equals("DEVASTATOR")) return !(GameManager.m_local_player.m_missile_level[5].ToString().Equals("LOCKED")) && !isSecondaryOnNeverSelectList("DEVASTATOR");
            if (weapon.Equals("TIMEBOMB")) return !(GameManager.m_local_player.m_missile_level[6].ToString().Equals("LOCKED")) && !isSecondaryOnNeverSelectList("TIMEBOMB");
            if (weapon.Equals("VORTEX")) return !(GameManager.m_local_player.m_missile_level[7].ToString().Equals("LOCKED")) && !isSecondaryOnNeverSelectList("VORTEX");
            else
            {
                return false;
            }

        }

        public static bool isSecondaryOnNeverSelectList(string weapon)
        {
            for (int i = 0; i < 8; i++)
            {
                if (weapon.Equals(SecondaryPriorityArray[i])) return SecondaryNeverSelect[i];
            }
            return false;
        }

        private static MissileType stringToMissileType(string weapon)
        {
            if (weapon.Equals("FALCON")) return MissileType.FALCON;
            if (weapon.Equals("MISSILE_POD")) return MissileType.MISSILE_POD;
            if (weapon.Equals("HUNTER")) return MissileType.HUNTER;
            if (weapon.Equals("CREEPER")) return MissileType.CREEPER;
            if (weapon.Equals("NOVA")) return MissileType.NOVA;
            if (weapon.Equals("DEVASTATOR")) return MissileType.DEVASTATOR;
            if (weapon.Equals("TIMEBOMB")) return MissileType.TIMEBOMB;
            if (weapon.Equals("VORTEX")) return MissileType.VORTEX;
            else
            {
                return MissileType.NUM;
            }
        }

        public static MissileType returnNextSecondary(Player local, bool prev)
        {
            if (areThereAllowedSecondaries())
            {
                MissileType currentMissile = local.m_missile_type;
                String currentMissileName = currentMissile.ToString();

                int index = getMissilePriority(currentMissile);
                if (prev && !currentMissileName.Equals(SecondaryPriorityArray[7]))
                {
                    index++;
                    while (index < 8)
                    {
                        if (isMissileAccessibleAndNotNeverselect(SecondaryPriorityArray[index]) && local.m_missile_ammo[getWeaponIndex(SecondaryPriorityArray[index])] > 0)
                        {
                            return stringToMissileType(SecondaryPriorityArray[index]);
                        }
                        index++;
                    }
                }
                else if (!prev && !currentMissileName.Equals(SecondaryPriorityArray[0]))
                {
                    index--;
                    while (index >= 0)
                    {
                        if (isMissileAccessibleAndNotNeverselect(SecondaryPriorityArray[index]) && local.m_missile_ammo[getWeaponIndex(SecondaryPriorityArray[index])] > 0)
                        {
                            return stringToMissileType(SecondaryPriorityArray[index]);
                        }
                        index--;
                    }
                }
                return MissileType.NUM;
            }
            else
            {
                return MissileType.NUM;
            }
        }

        public static int getMissilePriority(MissileType missile)
        {
            if (MPAutoSelection.isInitialised)
            {
                string mis = missile.ToString();
                for (int i = 0; i < 8; i++)
                {
                    if (mis.Equals(SecondaryPriorityArray[i]))
                    {
                        return i;
                    }
                }
                uConsole.Log("-AUTOSELECT- [WARN]: getMissilePriority:-1, primary wasnt in array");
                return -1;
            }
            else
            {
                uConsole.Log("-AUTOSELECT- [WARN]: getMissilePriority:-1, priority didnt get initialised");
                return -1;
            }

        }

        public static bool areThereAllowedSecondaries()
        {
            for (int i = 0; i < 8; i++)
            {
                if (SecondaryNeverSelect[i] == false) return true;
            }
            return false;
        }

        public static void maybeSwapMissiles(bool silent = false)
        {
            int highestMissile = findHighestPrioritizedUseableMissile();
            if (highestMissile == -1)
            {
                return;
            }
            else
            {
                swapToMissile(highestMissile, silent);
                return;
            }

        }

        public static int findHighestPrioritizedUseableMissile()
        {
            foreach (string missile in SecondaryPriorityArray)
            {
                int var = missileStringToInt(missile);
                if (GameManager.m_local_player.m_missile_ammo[var] > 0)
                {
                    return var;
                }
            }
            return -1;
        }

        public static int findHighestPrevMissile()
        {
            int currentMissile = (int)GameManager.m_local_player.m_missile_type;
            foreach (string missile in SecondaryPriorityArray)
            {
                int var = missileStringToInt(missile);
                if (GameManager.m_local_player.m_missile_ammo[var] > 0 && var != currentMissile)
                {
                    return var;
                }
            }
            return -1;
        }

        public static void swapToMissile(int weapon_num, bool silent = false)
        {
            if (GameManager.m_local_player.m_missile_level[weapon_num] == WeaponUnlock.LOCKED || GameManager.m_local_player.m_missile_ammo[weapon_num] == 0)//GameManager.m_local_player.m_missile_ammo[weapon_num] == 0)
            {
                return;
            }
            if (GameManager.m_local_player.m_missile_type != (MissileType)weapon_num)
            {
                GameManager.m_local_player.Networkm_missile_type = (MissileType)weapon_num;
                GameManager.m_local_player.CallCmdSetCurrentMissile(GameManager.m_local_player.Networkm_missile_type);

                if (GameManager.m_game_state != GameManager.GameState.GAMEPLAY)
                {
                    return;
                }

                UIElement.WEAPON_SELECT_FLASH = 1.25f;
                UIElement.WEAPON_SELECT_NAME = string.Format(Loc.LS("{0} SELECTED"), Player.MissileNames[GameManager.m_local_player.m_missile_type]);
                if (!silent)
                {
                    SFXCueManager.PlayCue2D(SFXCue.hud_cycle_typeA2, 1f, 0f, 0f, false);
                    GameManager.m_audio.PlayCue2D(362, 0.1f, 0f, 0f, false);

                    GameManager.m_local_player.c_player_ship.SetRefireDelayAfterMissileSwitch();
                }
                if (GameManager.m_local_player.m_missile_type == MissileType.DEVASTATOR)
                {
                    SFXCueManager.PlayCue2D(SFXCue.hud_warning_selected_dev, 1f, 0f, 0f, false);
                }

                GameManager.m_local_player.UpdateCurrentMissileName();
            }
        }

        public static int missileStringToInt(string missile)
        {

            if (missile.Equals("FALCON")) return 0;
            if (missile.Equals("MISSILE_POD")) return 1;
            if (missile.Equals("HUNTER")) return 2;
            if (missile.Equals("CREEPER")) return 3;
            if (missile.Equals("NOVA")) return 4;
            if (missile.Equals("DEVASTATOR")) return 5;
            if (missile.Equals("TIMEBOMB")) return 6;
            if (missile.Equals("VORTEX")) return 7;
            else
            {
                uConsole.Log("<ERROR> |: (missileStringToInt) string missile had unexpected type: " + missile);
                return -1;
            }
        }

        [HarmonyPatch(typeof(Player), "UnlockWeaponClient")]
        internal class WeaponPickup
        {
            public static void Postfix(WeaponType wt, bool silent, Player __instance)
            {
                UnlockWeaponEvent(wt, __instance);
            }

            public static void UnlockWeaponEvent(WeaponType wt, Player __instance)
            {
                if (MenuManager.opt_primary_autoswitch == 0 && MPAutoSelection.primarySwapFlag)
                {
                    if (GameplayManager.IsMultiplayerActive && NetworkMatch.InGameplay() && __instance == GameManager.m_local_player)
                    {
                        int new_weapon = getWeaponPriority(wt);
                        int current_weapon = getWeaponPriority(GameManager.m_local_player.m_weapon_type);
                        if (!PrimaryNeverSelect[new_weapon] && (new_weapon < current_weapon
                            || (MPClassic.matchEnabled && GameManager.m_local_player.m_weapon_type.Equals(WeaponType.IMPULSE) && GameManager.m_local_player.m_weapon_level[0].Equals(WeaponUnlock.LEVEL_1)) // Specific impulse upgrade case for classic mod
                            ))
                        {
                            if (!Controls.IsPressed(CCInput.FIRE_WEAPON))
                            {
                                swapToWeapon(wt.ToString());
                                GameManager.m_local_player.UpdateCurrentWeaponName();
                            }
                            else
                            {
                                waitingSwapWeaponType = wt.ToString();
                            }
                        }
                    }
                }
            }
        }

        


        [HarmonyPatch(typeof(Player), "SwitchToAmmoWeapon")]
        internal class OutOfAmmo
        {
            private static bool Prefix(Player __instance)
            {
                if (MenuManager.opt_primary_autoswitch == 0 && MPAutoSelection.primarySwapFlag)
                {
                    if (GameplayManager.IsMultiplayerActive && NetworkMatch.InGameplay() && __instance == GameManager.m_local_player)
                    {
                        maybeSwapPrimary();
                        if (swap_failed)
                        {
                            uConsole.Log("-AUTOORDER- [EB] swap failed on trying to switch to an ammo weapon");
                            swap_failed = false;
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    return true;
                }
                return true;
            }
        }


        [HarmonyPatch(typeof(Player), "SwitchToEnergyWeapon")]
        internal class OutOfEnergy
        {
            private static bool Prefix(Player __instance)
            {
                if (MenuManager.opt_primary_autoswitch == 0 && primarySwapFlag)
                {
                    if (GameplayManager.IsMultiplayerActive && NetworkMatch.InGameplay() && __instance == GameManager.m_local_player)
                    {

                        maybeSwapPrimary();
                        if (swap_failed)
                        {
                            uConsole.Log("-AUTOSELECT- [EB] swap failed on trying to switch to an energy weapon");
                            swap_failed = false;
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    return true;
                }
                return true;
            }
        }




        [HarmonyPatch(typeof(Player), "MaybeSwitchToNextMissile")]
        internal class NextLastMissileBasedOnPriority
        {
            public static bool Prefix(Player __instance)
            {

                if (MPAutoSelection.secondarySwapFlag)
                {
                    if (GameplayManager.IsMultiplayerActive && NetworkMatch.InGameplay() && __instance == GameManager.m_local_player)
                    {
                        if (!__instance.CanFireMissileAmmo(MissileType.NUM))
                        {

                            DelayedSwitchTimer re = new DelayedSwitchTimer();
                            re.Awake();
                            return false;
                        }
                    }
                }
                return true;
            }
        }


        // checks wether there was a swap that didnt get completed due to the player firing
        [HarmonyPatch(typeof(GameManager), "Update")]
        internal class ProcessDelayedSwap
        {
            public static void Postfix()
            {

                if (GameplayManager.IsMultiplayerActive && NetworkMatch.InGameplay())
                {
                    if (!Controls.IsPressed(CCInput.FIRE_WEAPON) && !waitingSwapWeaponType.Equals(""))
                    {
                        swapToWeapon(waitingSwapWeaponType);
                        GameManager.m_local_player.UpdateCurrentWeaponName();
                        waitingSwapWeaponType = "";
                    }
                }

            }
        }





        // This class is used to initiate a delayed swap in order to not confuse or get overwritten by a slow server control
        internal class DelayedSwitchTimer
        {
            Timer timer;

            public DelayedSwitchTimer() { }

            public void Awake()
            {
                var ntimer = new Timer(250);
                ntimer.Elapsed += new ElapsedEventHandler(timer_Elapsed);
                ntimer.Enabled = true;
                ntimer.AutoReset = false;
                timer = ntimer;
            }

            private void timer_Elapsed(object sender, ElapsedEventArgs e)
            {
                int weapon_num = findHighestPrioritizedUseableMissile();

                if (GameManager.m_local_player.m_missile_level[weapon_num] == WeaponUnlock.LOCKED || GameManager.m_local_player.m_missile_ammo[weapon_num] == 0)
                {
                    return;
                }
                if (GameManager.m_local_player.m_missile_type != (MissileType)weapon_num)
                {
                    GameManager.m_local_player.Networkm_missile_type = (MissileType)weapon_num;
                    GameManager.m_local_player.CallCmdSetCurrentMissile(GameManager.m_local_player.m_missile_type);
                    GameManager.m_local_player.UpdateCurrentMissileName();
                }
            }
        }




        /////////////////////////////////////////////////////////////////////////////////////
        //              VARIABLES (Switchlogic)                  
        /////////////////////////////////////////////////////////////////////////////////////
        public static String[] PrimaryPriorityArray = new String[8];   // holds the current primary priorities with 0 being the highest
        public static String[] SecondaryPriorityArray = new String[8]; // holds the current secondary priorities with 0 being the highest
        public static bool[] PrimaryNeverSelect = new bool[8];         // parallel to the primary priorities
        public static bool[] SecondaryNeverSelect = new bool[8];       // parallel to the secondary priorities
        public static string[] EnergyWeapons = { "IMPULSE", "CYCLONE", "REFLEX", "THUNDERBOLT", "LANCER" };
        public static string[] AmmoWeapons = { "CRUSHER", "FLAK", "DRILLER" };

        public static bool swap_failed = false;

        private static string waitingSwapWeaponType = ""; // used to hold the weapon type value if a swap gets put on hold due to the player still firing




        /////////////////////////////////////////////////////////////////////////////////////
        //              PUBLIC VARIABLES                  
        /////////////////////////////////////////////////////////////////////////////////////
        public static bool isCurrentlyInLobby = false;
        public static bool isInitialised = false;

        public static string last_valid_description = "CHANGE THE ORDER BY CLICKING AT THE TWO WEAPONS YOU WANT TO SWAP";

        public static bool primarySwapFlag = true;      // toggles the whole primary selection logic
        public static bool secondarySwapFlag = true;    // toggles the whole secondary selection logic
        public static bool zorc = false;                // extra alert for old men when the devastator gets autoselected, still need to find an annoying sound for that
        public static bool miasmic = false;             // dont draw certain hud elements
        public static bool allowSwapWhileFiring = true; // toggles wether weapon swaps are allowed to happen while the player is firing, if set to false it will delay the swap till the player is not firing anymore                                         

        public static string textFile = Path.Combine(Application.persistentDataPath, "AutoSelect-Config.txt");
    }

    /// <summary>
    /// Force the player's initially selected weapon to match the loadouts.
    /// </summary>
    [HarmonyPatch(typeof(Client), "OnRespawnMsg")]
    class MPAutoSelection_Client_OnRespawnMessage
    {
        public static void Postfix(NetworkMessage msg)
        {
            msg.reader.SeekZero();
            RespawnMessage respawnMessage = msg.ReadMessage<RespawnMessage>();

            GameObject gameObject = ClientScene.FindLocalObject(respawnMessage.m_net_id);
            if (gameObject == null)
            {
                return;
            }
            Player playerFromNetId = gameObject.GetComponent<Player>();
            if (playerFromNetId == null)
            {
                return;
            }

            if (playerFromNetId.isLocalPlayer)
            {
                if (MenuManager.opt_primary_autoswitch == 0 && MPAutoSelection.primarySwapFlag)
                {
                    if (GameplayManager.IsMultiplayerActive && NetworkMatch.InGameplay())
                    {
                        MPAutoSelection.maybeSwapPrimary(true);
                    }
                }

                if (MPAutoSelection.secondarySwapFlag)
                {
                    if (GameplayManager.IsMultiplayerActive && NetworkMatch.InGameplay())
                    {
                        MPAutoSelection.maybeSwapMissiles(true);
                    }
                }
            }
        }
    }
}
