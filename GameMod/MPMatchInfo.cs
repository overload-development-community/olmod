using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Overload;
using System.Reflection.Emit;

namespace GameMod
{
    public static class MPMatchInfo
    {
        private const float TEXT_SIZE = 0.3f;
        private const float LINE_SIZE = 18f;
        private const float LEFT_OFFSET = 200f;

        private static int powerup_bitmask = 0;
        private static bool[] primarypowerups = new bool[8];
        private static bool[] secondarypowerups = new bool[8];
        private static bool disabledpowerups = false;

        public static bool Displayed = false;

        public static void DrawMatchInfo(UIElement uie, Vector2 position)
        {
            if (powerup_bitmask != NetworkMatch.m_powerup_filter_bitmask)
            {
                disabledpowerups = false;
                powerup_bitmask = NetworkMatch.m_powerup_filter_bitmask;

                for (int i = 0; i < 8; i++)
                {
                    primarypowerups[i] = NetworkMatch.IsWeaponAllowed((WeaponType)i);
                    secondarypowerups[i] = NetworkMatch.IsMissileAllowed((MissileType)i);
                    if ((!primarypowerups[i] && (i != 2 || MPModPrivateData.ClassicSpawnsEnabled)) || !secondarypowerups[i]) // i == 2 if reflex -- don't mark it normally
                    {
                        disabledpowerups = true;
                    }
                }
            }

            bool show = false;

            position.y += 30f;
            float startY = position.y;
            position.y += 45f;

            if (NetworkMatch.m_match_time_limit_seconds != 900 && NetworkMatch.m_match_time_limit_seconds != 1200) // 15 and 20 minutes are expected, everything else is of note
            {
                show = true;
                if (NetworkMatch.m_match_time_limit_seconds < int.MaxValue)
                {
                    uie.DrawStringSmall("TIME LIMIT:", position - Vector2.right * LEFT_OFFSET, TEXT_SIZE, StringOffset.LEFT, UIManager.m_col_ui1, 1f, 120f);
                    uie.DrawStringSmall((NetworkMatch.m_match_time_limit_seconds / 60) + " min", position, TEXT_SIZE, StringOffset.RIGHT, UIManager.m_col_ui2, uie.m_alpha);
                }
                else
                {
                    uie.DrawStringSmall("NO TIME LIMIT", position - Vector2.right * LEFT_OFFSET, TEXT_SIZE, StringOffset.LEFT, UIManager.m_col_ui1, 1f, 120f);
                }
                position.y += LINE_SIZE;
            }
            if (NetworkMatch.m_match_score_limit != 0)
            {
                show = true;
                uie.DrawStringSmall("SCORE LIMIT:", position - Vector2.right * LEFT_OFFSET, TEXT_SIZE, StringOffset.LEFT, UIManager.m_col_ui1, 1f, 120f);
                uie.DrawStringSmall(NetworkMatch.m_match_score_limit.ToString(), position, TEXT_SIZE, StringOffset.RIGHT, UIManager.m_col_ui2, uie.m_alpha);
                position.y += LINE_SIZE;
            }
            if (MPModPrivateData.ShipMeshCollider != 0)
            {
                show = true;
                uie.DrawStringSmall("SHIP COLLIDER:", position - Vector2.right * LEFT_OFFSET, TEXT_SIZE, StringOffset.LEFT, UIManager.m_col_ui1, 1f, 120f);
                uie.DrawStringSmall(GetColliderName(MPModPrivateData.ShipMeshCollider), position, TEXT_SIZE, StringOffset.RIGHT, UIManager.m_col_ui2, uie.m_alpha);
                position.y += LINE_SIZE;
            }
            /*if (MPModPrivateData.ShipScale != 0)
            {
                show = true;
                uie.DrawStringSmall("SHIP SCALE:", position - Vector2.right * LEFT_OFFSET, TEXT_SIZE, StringOffset.LEFT, UIManager.m_col_ui1, 1f, 120f);
                uie.DrawStringSmall(MPModPrivateData.ShipScale * 100f + "%", position, TEXT_SIZE, StringOffset.RIGHT, UIManager.m_col_ui2, uie.m_alpha);
                position.y += LINE_SIZE;
            }*/
            if (MPModPrivateData.ClassicSpawnsEnabled)
            {
                show = true;
                uie.DrawStringSmall("CLASSIC SPAWNS ENABLED", position - Vector2.right * LEFT_OFFSET, TEXT_SIZE, StringOffset.LEFT, UIManager.m_col_ui1, 1f, 200f);
                position.y += LINE_SIZE;
            }
            if (NetworkMatch.m_team_damage)
            {
                show = true;
                uie.DrawStringSmall("FRIENDLY FIRE ENABLED", position - Vector2.right * LEFT_OFFSET, TEXT_SIZE, StringOffset.LEFT, UIManager.m_col_ui1, 1f, 200f);
                position.y += LINE_SIZE;
            }
            if (MPModPrivateData.ThunderboltPassthrough)
            {
                show = true;
                uie.DrawStringSmall("THUNDERBOLT PASSTHRU ENABLED", position - Vector2.right * LEFT_OFFSET, TEXT_SIZE, StringOffset.LEFT, UIManager.m_col_ui1, 1f, 200f);
                position.y += LINE_SIZE;
            }
            if (MPModPrivateData.AllowSmash)
            {
                show = true;
                uie.DrawStringSmall("SMASH ATTACK ENABLED", position - Vector2.right * LEFT_OFFSET, TEXT_SIZE, StringOffset.LEFT, UIManager.m_col_ui1, 1f, 200f);
                position.y += LINE_SIZE;
            }
            if (MPModPrivateData.CtfCarrierBoostEnabled)
            {
                show = true;
                uie.DrawStringSmall("CTF CARRIER BOOSTING ENABLED", position - Vector2.right * LEFT_OFFSET, TEXT_SIZE, StringOffset.LEFT, UIManager.m_col_ui1, 1f, 200f);
                position.y += LINE_SIZE;
            }
            if (MPModPrivateData.AlwaysCloaked)
            {
                show = true;
                uie.DrawStringSmall("SHIPS ALWAYS CLOAKED", position - Vector2.right * LEFT_OFFSET, TEXT_SIZE, StringOffset.LEFT, UIManager.m_col_ui1, 1f, 200f);
                position.y += LINE_SIZE;
            }
            if (NetworkMatch.m_force_loadout != 0)
            {
                show = true;
                uie.DrawStringSmall("FORCED LOADOUT:", position - Vector2.right * LEFT_OFFSET, TEXT_SIZE, StringOffset.LEFT, UIManager.m_col_ui1, 1f, 100f);
                if (NetworkMatch.m_force_w1 != WeaponType.NUM)
                {
                    uie.DrawStringSmall(Player.WeaponNames[(int)NetworkMatch.m_force_w1], position, TEXT_SIZE, StringOffset.RIGHT, UIManager.m_col_ui2, uie.m_alpha);
                    position.y += LINE_SIZE;
                }
                if (NetworkMatch.m_force_w2 != WeaponType.NUM)
                {
                    uie.DrawStringSmall(Player.WeaponNames[(int)NetworkMatch.m_force_w2], position, TEXT_SIZE, StringOffset.RIGHT, UIManager.m_col_ui2, uie.m_alpha);
                    position.y += LINE_SIZE;
                }
                if (NetworkMatch.m_force_m1 != MissileType.NUM)
                {
                    uie.DrawStringSmall(Player.MissileNames[(int)NetworkMatch.m_force_m1], position, TEXT_SIZE, StringOffset.RIGHT, UIManager.m_col_ui2, uie.m_alpha);
                    position.y += LINE_SIZE;
                }
                if (NetworkMatch.m_force_m2 != MissileType.NUM)
                {
                    uie.DrawStringSmall(Player.MissileNames[(int)NetworkMatch.m_force_m2], position, TEXT_SIZE, StringOffset.RIGHT, UIManager.m_col_ui2, uie.m_alpha);
                    position.y += LINE_SIZE;
                }
            }
            if (NetworkMatch.m_force_modifier1 != 4 || (NetworkMatch.m_force_modifier2 != 4))
            {
                show = true;
                uie.DrawStringSmall("FORCED MODS:", position - Vector2.right * LEFT_OFFSET, TEXT_SIZE, StringOffset.LEFT, UIManager.m_col_ui1, 1f, 100f);

                if (NetworkMatch.m_force_modifier1 != 4)
                {
                    uie.DrawStringSmall(Player.GetMpModifierName(NetworkMatch.m_force_modifier1, true), position, TEXT_SIZE, StringOffset.RIGHT, UIManager.m_col_ui2, uie.m_alpha);
                    position.y += LINE_SIZE;
                }
                if (NetworkMatch.m_force_modifier2 != 4)
                {
                    uie.DrawStringSmall(Player.GetMpModifierName(NetworkMatch.m_force_modifier2, false), position, TEXT_SIZE, StringOffset.RIGHT, UIManager.m_col_ui2, uie.m_alpha);
                    position.y += LINE_SIZE;
                }
            }
            if (disabledpowerups)
            {
                show = true;
                uie.DrawStringSmall("DISABLED POWERUPS:", position - Vector2.right * LEFT_OFFSET, TEXT_SIZE, StringOffset.LEFT, UIManager.m_col_ui1, 1f, 100f);
                for (int p = 0; p < 8; p++)
                {
                    if (!primarypowerups[p])
                    {
                        uie.DrawStringSmall(Player.WeaponNames[p], position, TEXT_SIZE, StringOffset.RIGHT, UIManager.m_col_ui2, uie.m_alpha);
                        position.y += LINE_SIZE;
                    }
                }
                for (int s = 0; s < 8; s++)
                {
                    if (!secondarypowerups[s])
                    {
                        uie.DrawStringSmall(Player.MissileNames[s], position, TEXT_SIZE, StringOffset.RIGHT, UIManager.m_col_ui2, uie.m_alpha);
                        position.y += LINE_SIZE;
                    }
                }
            }

            if (show)
            {
                position.y = startY;
                UIManager.DrawQuadUI(position - Vector2.right * 100f, 110f, 1f, UIManager.m_col_ub1, uie.m_alpha, 21);
                position.y += 15f;
                uie.DrawStringSmall(Loc.LS("MATCH OPTIONS"), position, 0.4f, StringOffset.RIGHT, UIManager.m_col_ui2, 1f, 200f);
                position.y += 15f;
                UIManager.DrawQuadUI(position - Vector2.right * 100f, 110f, 1f, UIManager.m_col_ub1, uie.m_alpha, 21);
            }

            Displayed = show;
        }

        public static string GetColliderName(int collider)
        {
            switch (collider)
            {
                case 0:
                default:
                    return "Sphere";
                case 1:
                    return "100% Mesh";
                case 2:
                    return "105% Mesh";
                case 3:
                    return "110% Mesh";
            }
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawMpPreMatchMenu")]
    public static class MPRoundInfo_UIElement_DrawMpPreMatchMenu
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;

            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(UIElement), "DrawDigitsTimeNoHours"))
                {
                    state = 1;
                }
                else if (state == 1 && code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(UIElement), "DrawStringSmall"))
                {
                    state = 2;
                }

                yield return code;

                if (state == 2) // draw the new round info stuff
                {
                    state = 3;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldloc_0); // Vector2 position
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPMatchInfo), "DrawMatchInfo"));
                }
            }
        }
    }
}
