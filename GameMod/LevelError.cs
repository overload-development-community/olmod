using HarmonyLib;
using Overload;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameMod
{
    [HarmonyPatch(typeof(UserLevelLoader), "Awake")]
    class LevelError_UserLevelLoader_Awake
    {
        public static bool loadError = false;

        static void UserLevelError()
        {
            if (Server.IsActive())
                loadError = true;
        }

        // Hook call to UserLevelError() after the call to UnityEngine.Debug.LogErrorFormat("USERLEVEL ERROR: {0}", new object[] { ex.Message })
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldstr && (string)code.operand == "USERLEVEL ERROR: {0}")
                    state = 1;

                if (state == 1 && code.opcode == OpCodes.Ldarg_0)
                {
                    state = 2;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(LevelError_UserLevelLoader_Awake), "UserLevelError"));
                }

                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(GameplayManager), "OnSceneLoaded")]
    class LevelError_GameplayManager_OnSceneLoaded
    {
        static bool Prefix()
        {
            if (Server.IsActive() && LevelError_UserLevelLoader_Awake.loadError)
            {
                // End match, disconnect clients and do minor server cleanup
                LevelError_UserLevelLoader_Awake.loadError = false;
                Debug.Log("Server error loading map, killing match");
                AccessTools.Method(typeof(NetworkMatch), "NetSystemNotifyMatchOver").Invoke(null, null);
                NetworkMatch.ExitMatchToMainMenu();
                GameManager.m_player_ship = PlayerShip.Instantiate();
                GameManager.m_local_player = GameManager.m_player_ship.c_player;
                AccessTools.Field(typeof(GameplayManager), "m_level_is_loaded").SetValue(null, true);
                AccessTools.Field(typeof(GameplayManager), "m_async_operation").SetValue(null, SceneManager.LoadSceneAsync(String.Empty));
                return false;
            }

            return true;
        }
    }
}
