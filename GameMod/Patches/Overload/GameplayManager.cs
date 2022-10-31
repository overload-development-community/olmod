using GameMod.Metadata;
using GameMod.Objects;
using HarmonyLib;
using Overload;

namespace GameMod.Patches.Overload {
    /// <summary>
    /// Determines whether to show rear view based on the gameplay state.
    /// </summary>
    [Mod(Mods.RearView)]
    [HarmonyPatch(typeof(GameplayManager), "ChangeGameplayState")]
    public class GameplayManager_ChangeGameplayState {
        public static void Prefix(GameplayState new_state) {
            if (new_state != GameplayState.PLAYING) {
                RearView.Pause();
            } else if (new_state == GameplayState.PLAYING) {
                if (RearView.Enabled)
                    RearView.Init();
                else
                    RearView.Pause();
            }
        }
    }
}
