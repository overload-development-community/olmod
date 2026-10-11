using System;
using Overload;
using UnityEngine;

namespace GameMod.Replay
{
    public static class Reticle
    {
        private const int RETICLE_STYLE_FULL = 0;
        private const int RETICLE_STYLE_MINIMAL = 2;

        public static int Style = RETICLE_STYLE_MINIMAL;

        public static bool StyleIsFull { get { return Style == RETICLE_STYLE_FULL; } }

        private const float LOW_ARMOR_HITPOINTS = 50f;
        private const int RING_SPOKES = 12;
        private const float SIDE_BRACKET_OFFSET_X = 120f;
        private const int LOADOUT_SLOTS = 8;

        private const int SPRITE_READY_FILL = 4;
        private const int SPRITE_HUNTER_DIAMOND = 34;
        private const int SPRITE_MISSILE_DIAMOND = 35;
        private const int SPRITE_WING_TICK = 37;
        private const int SPRITE_VENT_TICK = 38;
        private const int SPRITE_BARREL_TICK = 39;
        private const int SPRITE_IMPULSE_TICK = 40;
        private const int SPRITE_BRACKET = 41;
        private const int SPRITE_BRACKET_WIDE = 42;
        private const int SPRITE_BRACKET_OUTER = 43;
        private const int SPRITE_LOW_ARMOR_ARROW = 56;

        public static void Draw(UIElement ui, Vector2 pos, ShipState state, float maxHitpoints)
        {
            DrawWeaponReticle(ui, pos, state, maxHitpoints);
            DrawMissileReticle(ui, pos, state);
        }

        private static void DrawWeaponReticle(UIElement ui, Vector2 pos, ShipState state,
            float maxHitpoints)
        {
            Color ringColor = UIManager.m_col_ub2;

            if (Style == RETICLE_STYLE_FULL)
                DrawArmorReadout(ui, pos, state, maxHitpoints, ref ringColor);

            if (Style < RETICLE_STYLE_MINIMAL)
            {
                for (int spoke = 1; spoke < RING_SPOKES; spoke++)
                {
                    if (spoke == 6) // leave the bottom of the ring open
                        continue;
                    float angle = spoke * ((float)Math.PI / (RING_SPOKES * 0.5f));
                    DrawMark(ui, pos, angle, 80f, 0.3f, 0.3f, ringColor, ui.m_alpha,
                        SPRITE_BRACKET);
                }
            }

            if (Style == RETICLE_STYLE_FULL)
            {
                UIManager.DrawSpriteUI(new Vector2(pos.x - SIDE_BRACKET_OFFSET_X, pos.y), 0.3f, 0.3f,
                    UIManager.m_col_ub1, ui.m_alpha, SPRITE_BRACKET_OUTER);
                UIManager.DrawSpriteUI(new Vector2(pos.x + SIDE_BRACKET_OFFSET_X, pos.y), 0.3f, 0.3f,
                    UIManager.m_col_ub1, ui.m_alpha, SPRITE_BRACKET_OUTER);
            }

            WeaponUnlock level = state.WeaponLevel(state.weapon);
            switch ((WeaponType)state.weapon)
            {
                case WeaponType.IMPULSE:
                    DrawImpulseReticle(ui, pos, level);
                    break;
                case WeaponType.CYCLONE:
                    DrawCycloneReticle(ui, pos);
                    break;
                case WeaponType.REFLEX:
                    DrawReflexReticle(ui, pos);
                    break;
                case WeaponType.CRUSHER:
                    DrawCrusherReticle(ui, pos);
                    break;
                case WeaponType.DRILLER:
                    DrawDrillerReticle(ui, pos, level);
                    break;
                case WeaponType.FLAK:
                    DrawFlakReticle(ui, pos);
                    break;
                case WeaponType.THUNDERBOLT:
                    DrawThunderboltReticle(ui, pos);
                    break;
                case WeaponType.LANCER:
                    DrawLancerReticle(ui, pos, level);
                    break;
            }
        }

        private static void DrawArmorReadout(UIElement ui, Vector2 pos, ShipState state,
            float maxHitpoints, ref Color ringColor)
        {
            float hitpoints = state.Hitpoints(maxHitpoints);
            int armorValue = Mathf.Max((int)hitpoints, 1);
            pos.y -= 70f;

            if (state.IsInvulnerable)
            {
                ringColor = Color.Lerp(UIManager.m_col_invuln0, UIManager.m_col_invuln2,
                    UnityEngine.Random.value * UIElement.FLICKER);
                ui.DrawDigitsThree(pos, armorValue, 0.65f, StringOffset.CENTER, ringColor, ui.m_alpha);
                ui.DrawStringSmall("[       ]", new Vector2(pos.x - 1f, pos.y), 0.65f,
                    StringOffset.CENTER, ringColor, 1f);
                return;
            }

            if (hitpoints >= LOW_ARMOR_HITPOINTS)
            {
                ui.DrawDigitsThree(pos, armorValue, 0.65f, StringOffset.CENTER, UIManager.m_col_armor0,
                    ui.m_alpha * 0.5f);
                return;
            }

            float lowArmorRamp = Mathf.Clamp01(0.2f + (LOW_ARMOR_HITPOINTS - hitpoints) / LOW_ARMOR_HITPOINTS);
            float lowArmorFlash = lowArmorRamp * (0.2f + ui.m_anim_state2 * 0.6f);
            ringColor = Color.Lerp(UIManager.m_col_hi5, UIManager.m_col_em6, lowArmorFlash);
            ui.DrawDigitsThree(pos, armorValue, 0.65f, StringOffset.CENTER, ringColor, ui.m_alpha);
            UIManager.DrawSpriteUI(new Vector2(pos.x, pos.y - 25f), 0.15f, 0.15f,
                ringColor, ui.m_alpha, SPRITE_LOW_ARMOR_ARROW);
        }

        private static void DrawImpulseReticle(UIElement ui, Vector2 pos, WeaponUnlock level)
        {
            const float RADIUS = 45f;

            const float WIDE_SPREAD = 1.778f;
            bool wide = level == WeaponUnlock.LEVEL_2A;

            DrawMark(ui, pos, 2.0943952f, RADIUS, 0.3f, 0.3f, UIManager.m_col_ui4, ui.m_alpha,
                SPRITE_IMPULSE_TICK);
            if (wide)
            {
                DrawMark(ui, pos, 2.0943952f, RADIUS * WIDE_SPREAD, new Vector2(3f, -5f), 0.3f,
                    0.3f, UIManager.m_col_ui0, ui.m_alpha, SPRITE_BRACKET);
                DrawMark(ui, pos, 2.0943952f, RADIUS * WIDE_SPREAD, new Vector2(-3f, 5f), 0.3f,
                    0.3f, UIManager.m_col_ui0, ui.m_alpha, SPRITE_BRACKET);
            }

            DrawMark(ui, pos, 4.1887903f, RADIUS, 0.3f, 0.3f, UIManager.m_col_ui4, ui.m_alpha,
                SPRITE_IMPULSE_TICK);
            if (wide)
            {
                DrawMark(ui, pos, 4.1887903f, RADIUS * WIDE_SPREAD, new Vector2(3f, 5f), 0.3f,
                    0.3f, UIManager.m_col_ui0, ui.m_alpha, SPRITE_BRACKET);
                DrawMark(ui, pos, 4.1887903f, RADIUS * WIDE_SPREAD, new Vector2(-3f, -5f), 0.3f,
                    0.3f, UIManager.m_col_ui0, ui.m_alpha, SPRITE_BRACKET);
            }
        }

        private static void DrawCycloneReticle(UIElement ui, Vector2 pos)
        {
            float angle = 3.1415927f;
            for (int mark = 0; mark < 3; mark++)
            {
                DrawMark(ui, pos, angle, 30f, 0.3f, 0.3f, UIManager.m_col_ui0, ui.m_alpha,
                    SPRITE_BRACKET);
                angle += 2.0943952f;
            }

            angle = 0f;
            for (int mark = 0; mark < 3; mark++)
            {
                DrawMark(ui, pos, angle, 50f, 0.3f, 0.3f, UIManager.m_col_ui4, ui.m_alpha,
                    SPRITE_VENT_TICK);
                angle += 2.0943952f;
            }
        }

        private static void DrawReflexReticle(UIElement ui, Vector2 pos)
        {
            float brightAlpha = ui.m_alpha;
            float dimAlpha = ui.m_alpha * 0.6f;

            DrawMark(ui, pos, 2.0943952f, 65f, 0.3f, 0.3f, UIManager.m_col_ui4, brightAlpha,
                SPRITE_WING_TICK);
            DrawMark(ui, pos, 2.0943952f, 65f * 1.45f, 0.3f, 0.3f, UIManager.m_col_ui4,
                brightAlpha, SPRITE_WING_TICK);
            DrawMark(ui, pos, 4.1887903f, 65f, 0.3f, 0.3f, UIManager.m_col_ui4, dimAlpha,
                SPRITE_WING_TICK);
            DrawMark(ui, pos, 4.1887903f, 65f * 1.45f, 0.3f, 0.3f, UIManager.m_col_ui4,
                dimAlpha, SPRITE_WING_TICK);
        }

        private static void DrawCrusherReticle(UIElement ui, Vector2 pos)
        {
            DrawMark(ui, pos, 2.0943952f, 85f, 0.3f, 0.3f, UIManager.m_col_ui4, ui.m_alpha,
                SPRITE_BARREL_TICK);
            DrawMark(ui, pos, 4.1887903f, 85f, 0.3f, 0.3f, UIManager.m_col_ui4, ui.m_alpha,
                SPRITE_BARREL_TICK);
            DrawMark(ui, pos, 1.0471976f, 85f, 0.3f, 0.3f, UIManager.m_col_ui4, ui.m_alpha,
                SPRITE_BARREL_TICK);
            DrawMark(ui, pos, 5.2359877f, 85f, 0.3f, 0.3f, UIManager.m_col_ui4, ui.m_alpha,
                SPRITE_BARREL_TICK);
        }

        private static void DrawDrillerReticle(UIElement ui, Vector2 pos, WeaponUnlock level)
        {
            DrawMark(ui, pos, 2.6179938f, 20f, 0.3f, 0.3f, UIManager.m_col_ui0,
                ui.m_alpha, SPRITE_BRACKET);
            DrawMark(ui, pos, 3.6651917f, 20f, 0.3f, 0.3f, UIManager.m_col_ui0,
                ui.m_alpha, SPRITE_BRACKET);

            if (level == WeaponUnlock.LEVEL_2B)
            {
                DrawMark(ui, pos, 3.1415927f, 20f, 0.18f, 0.3f, UIManager.m_col_ui0, ui.m_alpha,
                    SPRITE_BRACKET);
                DrawMark(ui, pos, 3.1415927f, 20f, new Vector2(0f, 10f), 0.18f, 0.3f,
                    UIManager.m_col_ui0, ui.m_alpha, SPRITE_BRACKET);
            }
            else
            {
                DrawMark(ui, pos, 3.1415927f, 25f, 0.3f, 0.3f, UIManager.m_col_ui0, ui.m_alpha,
                    SPRITE_BRACKET_WIDE);
            }

            DrawMark(ui, pos, 1.5707964f, 30f, 0.3f, 0.3f, UIManager.m_col_ui4, ui.m_alpha,
                SPRITE_VENT_TICK);
            DrawMark(ui, pos, 4.712389f, 30f, 0.3f, 0.3f, UIManager.m_col_ui4, ui.m_alpha,
                SPRITE_VENT_TICK);
        }

        private static void DrawFlakReticle(UIElement ui, Vector2 pos)
        {
            Color color = UIManager.m_col_ui3;
            const float MARK_WIDTH = 0.3f;
            const float RADIUS = 105f;

            DrawMark(ui, pos, 0.7853982f, RADIUS, MARK_WIDTH, 0.3f, color, ui.m_alpha,
                SPRITE_WING_TICK);
            DrawMark(ui, pos, 2.3561945f, RADIUS, MARK_WIDTH, 0.3f, color, ui.m_alpha,
                SPRITE_WING_TICK);
            DrawMark(ui, pos, 3.926991f, RADIUS, MARK_WIDTH, 0.3f, color, ui.m_alpha,
                SPRITE_WING_TICK);
            DrawMark(ui, pos, 5.4977875f, RADIUS, MARK_WIDTH, 0.3f, color, ui.m_alpha,
                SPRITE_WING_TICK);
            DrawMark(ui, pos, 4.712389f, RADIUS, MARK_WIDTH, 0.3f, color, ui.m_alpha,
                SPRITE_WING_TICK);
            DrawMark(ui, pos, 1.5707964f, RADIUS, MARK_WIDTH, 0.3f, color, ui.m_alpha,
                SPRITE_WING_TICK);
        }

        // The engine ramps the four ring pairs with the thunderbolt's charge, but charge is
        // not in a recorded frame -- draw the uncharged shape rather than fake a ramp.
        private static void DrawThunderboltReticle(UIElement ui, Vector2 pos)
        {
            Color uncharged = UIManager.m_col_ui0;
            DrawMark(ui, pos, 1.0471976f, 40f, 0.3f, 0.3f, uncharged, ui.m_alpha, SPRITE_BRACKET);
            DrawMark(ui, pos, 5.2359877f, 40f, 0.3f, 0.3f, uncharged, ui.m_alpha, SPRITE_BRACKET);
            DrawMark(ui, pos, 1.3089969f, 40f, 0.3f, 0.3f, uncharged, ui.m_alpha,
                SPRITE_BRACKET);
            DrawMark(ui, pos, 4.9741883f, 40f, 0.3f, 0.3f, uncharged, ui.m_alpha,
                SPRITE_BRACKET);
            DrawMark(ui, pos, 1.5707964f, 40f, 0.3f, 0.3f, uncharged, ui.m_alpha, SPRITE_BRACKET);
            DrawMark(ui, pos, 4.712389f, 40f, 0.3f, 0.3f, uncharged, ui.m_alpha, SPRITE_BRACKET);
            DrawMark(ui, pos, 1.8325957f, 40f, 0.3f, 0.3f, uncharged, ui.m_alpha,
                SPRITE_BRACKET);
            DrawMark(ui, pos, 4.4505897f, 40f, 0.3f, 0.3f, uncharged, ui.m_alpha,
                SPRITE_BRACKET);

            Color barrels = UIManager.m_col_ui4;
            DrawMark(ui, pos, 2.0943952f, 45f, 0.3f, 0.3f, barrels, ui.m_alpha, SPRITE_BARREL_TICK);
            DrawMark(ui, pos, 4.1887903f, 45f, 0.3f, 0.3f, barrels, ui.m_alpha, SPRITE_BARREL_TICK);
        }

        private static void DrawLancerReticle(UIElement ui, Vector2 pos, WeaponUnlock level)
        {
            float dimAlpha = (level == WeaponUnlock.LEVEL_2B) ? ui.m_alpha * 0.15f : ui.m_alpha;

            DrawMark(ui, pos, 2.0943952f, 35f, 0.3f, 0.3f, UIManager.m_col_ui0, ui.m_alpha,
                SPRITE_BRACKET_OUTER);
            DrawMark(ui, pos, 4.1887903f, 35f, 0.3f, 0.3f, UIManager.m_col_ui0, ui.m_alpha,
                SPRITE_BRACKET_OUTER);
            DrawMark(ui, pos, 2.0943952f, 70f, 0.3f, 0.3f, UIManager.m_col_ui4, ui.m_alpha,
                SPRITE_BARREL_TICK);
            DrawMark(ui, pos, 4.1887903f, 70f, 0.3f, 0.3f, UIManager.m_col_ui4, dimAlpha,
                SPRITE_BARREL_TICK);
        }

        private static void DrawMissileReticle(UIElement ui, Vector2 pos, ShipState state)
        {
            int slot = state.missile;
            if (slot >= LOADOUT_SLOTS)
                return;

            Color ready = Color.Lerp(UIManager.m_col_ui6, UIManager.m_col_ui7,
                UnityEngine.Random.value * UIElement.FLICKER);
            Color outline = UIManager.m_col_ui2;
            float alpha = ui.m_alpha;
            bool hasAmmo = state.MissileCount(slot) > 0;
            const float DIAMOND_SCALE = 10.799999f;
            const float READY_SCALE = 6f;
            const float POD_DIAMOND_SCALE = 8.4f;
            const float POD_READY_SCALE = 4.8f;
            const float FORWARD_OFFSET_Y = 48f;
            const float SIDE_OFFSET_X = 62f;
            const float DIAGONAL = 0.7853982f;

            switch ((MissileType)slot)
            {
                case MissileType.NOVA:
                case MissileType.DEVASTATOR:
                case MissileType.TIMEBOMB:
                case MissileType.VORTEX:
                {
                    if ((MissileType)slot == MissileType.DEVASTATOR)
                    {
                        outline = Color.Lerp(UIManager.m_col_hi5, UIManager.m_col_hi6,
                            UnityEngine.Random.value * UIElement.FLICKER);
                    }
                    Vector2 at = new Vector2(pos.x, pos.y + FORWARD_OFFSET_Y);
                    UIManager.DrawQuadUIRotated(at, DIAMOND_SCALE, DIAMOND_SCALE, DIAGONAL, outline,
                        alpha, SPRITE_MISSILE_DIAMOND);
                    if (hasAmmo)
                    {
                        UIManager.DrawQuadUIRotated(at, READY_SCALE, READY_SCALE, DIAGONAL, ready,
                            alpha, SPRITE_READY_FILL);
                    }
                    break;
                }
                case MissileType.HUNTER:
                {
                    Vector2 left = new Vector2(pos.x - SIDE_OFFSET_X, pos.y);
                    Vector2 right = new Vector2(pos.x + SIDE_OFFSET_X, pos.y);
                    UIManager.DrawQuadUIRotated(left, POD_DIAMOND_SCALE, POD_DIAMOND_SCALE, DIAGONAL,
                        outline, alpha, SPRITE_HUNTER_DIAMOND);
                    UIManager.DrawQuadUIRotated(right, POD_DIAMOND_SCALE, POD_DIAMOND_SCALE, DIAGONAL,
                        outline, alpha, SPRITE_HUNTER_DIAMOND);
                    if (hasAmmo)
                    {
                        UIManager.DrawQuadUIRotated(right, POD_READY_SCALE, POD_READY_SCALE, DIAGONAL,
                            ready, alpha, SPRITE_READY_FILL);
                        UIManager.DrawQuadUIRotated(left, POD_READY_SCALE, POD_READY_SCALE, DIAGONAL,
                            ready, alpha, SPRITE_READY_FILL);
                    }

                    if (state.MissileLevel(slot) == WeaponUnlock.LEVEL_2A)
                    {
                        Vector2 forward = new Vector2(pos.x, pos.y + FORWARD_OFFSET_Y);
                        UIManager.DrawQuadUIRotated(forward, POD_DIAMOND_SCALE, POD_DIAMOND_SCALE,
                            DIAGONAL, outline, alpha, SPRITE_MISSILE_DIAMOND);
                        if (hasAmmo)
                        {
                            UIManager.DrawQuadUIRotated(forward, POD_READY_SCALE, POD_READY_SCALE,
                                DIAGONAL, ready, alpha, SPRITE_READY_FILL);
                        }
                    }
                    break;
                }
                case MissileType.MISSILE_POD:
                {
                    Vector2 left = new Vector2(pos.x - SIDE_OFFSET_X, pos.y);
                    Vector2 right = new Vector2(pos.x + SIDE_OFFSET_X, pos.y);
                    UIManager.DrawQuadUIRotated(left, POD_DIAMOND_SCALE, POD_DIAMOND_SCALE, DIAGONAL,
                        outline, alpha, SPRITE_MISSILE_DIAMOND);
                    UIManager.DrawQuadUIRotated(right, POD_DIAMOND_SCALE, POD_DIAMOND_SCALE, DIAGONAL,
                        outline, alpha, SPRITE_MISSILE_DIAMOND);
                    if (hasAmmo)
                    {
                        UIManager.DrawQuadUIRotated(left, POD_READY_SCALE, POD_READY_SCALE, DIAGONAL,
                            ready, alpha, SPRITE_READY_FILL);
                    }
                    break;
                }
                case MissileType.FALCON:
                case MissileType.CREEPER:
                {
                    Vector2 left = new Vector2(pos.x - SIDE_OFFSET_X, pos.y);
                    Vector2 right = new Vector2(pos.x + SIDE_OFFSET_X, pos.y);
                    UIManager.DrawQuadUIRotated(left, DIAMOND_SCALE, DIAMOND_SCALE, DIAGONAL, outline,
                        alpha, SPRITE_MISSILE_DIAMOND);
                    UIManager.DrawQuadUIRotated(right, DIAMOND_SCALE, DIAMOND_SCALE, DIAGONAL, outline,
                        alpha, SPRITE_MISSILE_DIAMOND);
                    if (hasAmmo)
                    {
                        UIManager.DrawQuadUIRotated(left, READY_SCALE, READY_SCALE, DIAGONAL, ready,
                            alpha, SPRITE_READY_FILL);
                    }
                    break;
                }
            }
        }

        private static void DrawMark(UIElement ui, Vector2 center, float angleRadians, float radius,
            float widthScale, float heightScale, Color color, float alpha, int sprite)
        {
            DrawMark(ui, center, angleRadians, radius, Vector2.zero, widthScale, heightScale, color,
                alpha, sprite);
        }

        private static void DrawMark(UIElement ui, Vector2 center, float angleRadians, float radius,
            Vector2 offset, float widthScale, float heightScale, Color color, float alpha,
            int sprite)
        {
            Vector2 at = new Vector2(
                center.x + Mathf.Sin(angleRadians) * radius + offset.x,
                center.y - Mathf.Cos(angleRadians) * radius + offset.y);
            UIManager.DrawSpriteUIRotated(at, widthScale, heightScale, angleRadians + (float)Math.PI / 2f,
                color, alpha, sprite);
        }
    }
}
