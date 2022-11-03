﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GameMod.Metadata;
using GameMod.Objects;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod.Patches.Overload {
    /// <summary>
    /// Chooses the correct color for the team.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(UIManager), "ChooseMpColor")]
    public static class UIManager_ChooseMpColor {
        public static bool Prefix(MpTeam team, ref Color __result) {
            if (!NetworkMatch.IsTeamMode(NetworkMatch.GetMode()))
                return true;

            if (Teams.NetworkMatchTeamCount < (int)MpTeam.NUM_TEAMS) {
                if (Menus.mms_team_color_default)
                    return true;

                bool my_team = team == GameManager.m_local_player.m_mp_team;
                if (my_team) {
                    __result = Teams.TeamColorByIndex(Menus.mms_team_color_self, 2);
                } else {
                    __result = Teams.TeamColorByIndex(Menus.mms_team_color_enemy, 0);
                }

            } else {
                __result = Teams.TeamColor(team, 2);
            }

            return false;
        }
    }

    /// <summary>
    /// In team games, display a red arrow below enemy names.
    /// </summary>
    [Mod(Mods.EnemyPlayerArrows)]
    [HarmonyPatch(typeof(UIManager), "DrawMpPlayerArrow")]
    public static class UIManager_DrawMpPlayerArrow {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static void Postfix(Player player, Vector2 offset) {
            float a = (player.m_mp_team != GameManager.m_local_player.m_mp_team) ? (1f * player.m_mp_data.vis_fade) : 1f;
            float rotation = 4.712389f;

            if (player.m_mp_team != GameManager.m_local_player.m_mp_team) {
                Vector2 redOffset = new Vector2(0f, -0.8f);
                offset += redOffset;
                UIManager.DrawSpriteUIRotated(offset, 0.015f, 0.015f, rotation, Color.red, a, 81);
            }
        }
    }

    /// <summary>
    /// Disables the UI collision mesh.
    /// </summary>
    [Mod(Mods.UIMeshCollider)]
    [HarmonyPatch(typeof(UIManager), "GenerateUICollisionMesh")]
    public static class UIManager_GenerateUICollisionMesh {
        /// <remarks>
        /// url[4] seems to be the ui collision layer, it is never _meant_
        /// to be rendered at all, but only (mis?-)used by the game as a
        /// a helper to dynamically generate a Mesh for a MeshCollider
        /// which is later used for (CPU side) raycasting in
        /// UIManager.FindMousePosition().
        /// However, since this mesh is created in a render layer, it is
        /// actually rendered _every frame_ for no reason at all.
        ///
        /// NOTE: "UnityRenderLayer" is not an Unity concept, but something
        /// Luke Schneider came up with for his earlier games, see
        /// https://www.gamasutra.com/blogs/LukeSchneider/20120911/177488/XNAtoUnity_Part_1__The_Setup.php
        /// https://www.gamasutra.com/blogs/LukeSchneider/20120919/177963/XNAtoUnity_Part_2_Rendering.php
        /// It seems this stuff got re-used in Overload...
        /// </remarks>
        public static void Postfix() {
            UnityRenderLayer renderLayer = UIManager.url[4];

            // access the protected field "c_renderer" of the UnityRenderLayer
            Type renderLayerType = renderLayer.GetType();
            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo fieldInfo = renderLayerType.GetField("c_renderer", bindingFlags);
            Renderer r = (Renderer)fieldInfo.GetValue(renderLayer);
            r.enabled = false; // turn it off
        }
    }
}
