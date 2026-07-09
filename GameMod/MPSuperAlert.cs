using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod{
    static class MPSuperAlert{ 
        public static bool active_countdown = false;
        public static float start_time = 0f;

        [HarmonyPatch(typeof(Player), "RpcShowWarningMessage")]
        class MPSuperAlert_Player_RpcShowWarningMessage{
            static void Postfix(int type){
                if (!NetworkMatch.InGameplay() || !Menus.mms_super_countdown)
                    return;

                if(type == 0){
                    MPSuperAlert.start_time = Time.time;
                    MPSuperAlert.active_countdown = true;
                }
                if(type == 1)
                    MPSuperAlert.active_countdown = false;
            }
        }

        [HarmonyPatch(typeof(UIElement), "DrawHUD")]
        class MPSuperAlert_UIElement_DrawHUD{
            static void Postfix(UIElement __instance){
                if (!Menus.mms_super_countdown || !MPSuperAlert.active_countdown 
                || GameManager.m_local_player == null || GameManager.m_local_player.m_spectator
                || (bool)GameManager.m_player_ship.m_dying || (bool)GameManager.m_player_ship.m_dead)
                    return;

                if (GameplayManager.IsMultiplayerActive && NetworkMatch.m_match_state != MatchState.PLAYING){
                    MPSuperAlert.active_countdown = false;
                    return;
                }

                float remaining_countdown = 10.0f + start_time - Time.time;
                if (remaining_countdown <= 0f){
                    MPSuperAlert.active_countdown = false;
                    return;
                }

                float pulse = 0.3f + 0.5f * Mathf.Cos(Time.time * Mathf.PI * 2f);
                float alpha = Mathf.Lerp(0.35f, 1f, pulse);
                Color col = Color.Lerp(UIManager.m_col_em2, UIManager.m_col_em5, pulse);

                Vector2 pos = Vector2.zero;
                pos.x = 0f;
                pos.y = UIManager.UI_TOP + 120f;
                __instance.DrawStringSmall(Loc.LS("SUPER IN"), pos, 0.6f, StringOffset.CENTER, col, alpha);
                pos.y += 40f;

                __instance.DrawStringSmall(Mathf.CeilToInt(remaining_countdown).ToString(), pos, 0.9f, StringOffset.CENTER, col, alpha);
            }
        }

    }
}
