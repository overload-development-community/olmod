using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod
{
    // Allow #kills < 0 when suiciding
    [HarmonyPatch(typeof(Player), "RpcAddKill")]
    class MPNegativeKills
    {
        private static void Prefix(Player __instance, int killer_connection_id, int killed_connection_id)
        {
            if (killer_connection_id == killed_connection_id && __instance.m_kills <= 0) // original code won't decrement in this case
                __instance.m_kills--;
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawDigitsVariable")]
    class DrawDigitsNeg
    {
        private static bool Prefix(UIElement __instance, ref Vector2 pos, ref int value, float scl, ref StringOffset offset, Color c, float a)
        {
            if (value >= 0)
                return true;
            value = -value;
            int len = (int)Mathf.Floor(Mathf.Log10(value) + 1f) + 1;
            switch (offset)
            {
                case StringOffset.CENTER:
                    pos.x -= 22f * scl * 0.5f * (len - 1);
                    break;
                case StringOffset.RIGHT:
                    pos.x -= 22f * scl * (len - 1);
                    break;
            }
            float width = 0.25f * scl;
            UIManager.DrawSpriteUI(pos, width, width, c, a, (int)AtlasIndex0.LINE_THICK1); // LINE_THICK1 has same width as digits
            pos.x += 22f * scl;
            offset = StringOffset.LEFT;
            return true;
        }
    }
}
