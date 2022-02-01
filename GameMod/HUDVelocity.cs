using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod
{
    class HUDVelocity
    {
        public static bool MenuManagerEnabled = false;    
    }

    [HarmonyPatch(typeof(UIElement), "DrawHUD")]
    class HUDVelocity_UIElement_DrawHud
    {
        static Max max = new Max { vel = 0f, time = 0f };
        static float historyDuration = 5f;

        class Max
        {
            public float vel;
            public float time;
        }

        static void Postfix(UIElement __instance)
        {
            if (!HUDVelocity.MenuManagerEnabled)
                return;

            Vector2 pos = default(Vector2);
            pos.x = 0f;
            pos.y = UIManager.UI_TOP - 210f;
            var vel = GameManager.m_player_ship.c_rigidbody.velocity;
            if (vel.magnitude > max.vel || max.time < GameplayManager.m_total_time - historyDuration)
                max = new Max { vel = vel.magnitude, time = GameplayManager.m_total_time };

            __instance.DrawStringSmall($"Vel: {vel.magnitude.ToString("n2")}", pos, 0.5f, StringOffset.CENTER, UIManager.m_col_ui4, 1f);
            pos.y -= 24f;
            __instance.DrawStringSmall($"Max: {max.vel.ToString("n2")}", pos, 0.5f, StringOffset.CENTER, UIManager.m_col_ui4, 1f);

        }
    }
}