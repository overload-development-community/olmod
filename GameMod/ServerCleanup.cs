using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/* NOTE: This class is meant as the central place for "cleaning up" server
 *       behavior, mainly by disabling stuff which is only neccessary on
 *       the client.
 *
 *       Currently, it only disables audio playback on the server, but
 *       I plan to add further patches later.
 */
namespace GameMod {
    [HarmonyPatch(typeof(GameManager), "SetupDedicatedServer")]
    class ServerCleanup_GameManager_SetupDedicatedServer {
        static void Postfix() {
            if (GameplayManager.IsDedicatedServer()) {
                AudioListener.pause = true;
                Debug.Log("Dedicated Server: paused AudioListener");
            }
        }
    }

    /* enable this to hear the server's audio... 
    [HarmonyPatch(typeof(GameManager), "Update")]
    class ServerCleanup_GameManager_OverrideAudioVolume {
        static void Postfix() {
            if (GameplayManager.IsDedicatedServer()) {
                AudioListener.volume=1.0f;
            }
        }
    }
    */

    [HarmonyPatch(typeof(UnityAudio), "FindNextOpenAudioSlot")]
    class ServerCleanup_NoOpenAudioSlot {
        static bool Prefix(ref int __result) {
            // tell the server we are sorry, but we don't have any audio channels left...
            if (GameplayManager.IsDedicatedServer()) {
                __result = -1;
                //Debug.Log("XXXXX audio playback suppressed on server");
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(UnityAudio), "PlayMusic")]
    class ServerCleanup_NoPlayMusic {
        static bool Prefix() {
            // suppress PlayMusic requests on the server...
            if (GameplayManager.IsDedicatedServer()) {
                return false;
            }
            return true;
        }
    }

    // This patch is commented out since it causes network audio to not work (ie: energy center sound)

    //[HarmonyPatch(typeof(UnityAudio), "UpdateAudio")]
    //class ServerCleanup_NoUpdateAudio {
    //    static bool Prefix() {
    //        // don't do UpdateAudio on the Server
    //        if (GameplayManager.IsDedicatedServer()) {
    //            return false;
    //        }
    //        return true;
    //    }
    //}

    // Don't show level loading screen on server, which is prone to crashing.
    [HarmonyPatch(typeof(MenuManager), "PlayGameUpdate")]
    class ServerCleanup_MenuManager_PlayGameUpdate {
        private static FieldInfo _MenuManager_m_seconds_waiting_for_gi_covergence = typeof(MenuManager).GetField("m_seconds_waiting_for_gi_covergence", BindingFlags.Static | BindingFlags.NonPublic);
        private static MethodInfo _MenuManager_ResetBackStack = AccessTools.Method(typeof(MenuManager), "ResetBackStack");
        static bool Prefix(bool returning_from_secret) {
            if (!NetworkManager.IsServer() || MenuManager.m_menu_sub_state != MenuSubState.INIT) {
                return true;
            }

            MenuManager.m_returning_from_secret = returning_from_secret;
            GameplayManager.m_game_time_mission = (float)GameplayManager.m_game_time_mission + (Time.realtimeSinceStartup - GameplayManager.m_between_level_start);
            if (GameplayManager.LevelIsLoading()) {
                GameplayManager.CompleteLevelLoad();
            }
            if (GameplayManager.LevelIsLoading()) {
                GameplayManager.CompleteLevelLoad();
            } else {
                GameplayManager.LoadLevel(GameplayManager.m_level_info);
                GameplayManager.AllowSceneActivation();
            }
            MenuManager.m_menu_sub_state = MenuSubState.ACTIVE;
            _MenuManager_m_seconds_waiting_for_gi_covergence.SetValue(null, 0f);
            _MenuManager_ResetBackStack.Invoke(null, new object[] { });

            return false;
        }
    }
}
