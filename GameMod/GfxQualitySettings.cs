using System.Reflection;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod
{
    static class GfxQualitySettings
    {
        // setting options
        private static readonly int[] MaxQueuedValues = { 1, 2, 3, 4 };
        private static readonly int[] ParticleBudgetValues = { 8, 16, 64, 256, 1024, 4096 };
        private static readonly string[] ShadowModeNames = { "OFF", "HARD", "SOFT" };       // ShadowQuality
        private static readonly int[] ShadowDistanceValues = { 20, 35, 50, 75, 100 };
        private static readonly string[] ShadowResolutionNames = { "LOW", "MEDIUM", "HIGH", "VERY HIGH" };
        private static readonly int[] ShadowCascadeValues = { 1, 2, 4 };
        private static readonly string[] ShadowProjectionNames = { "CLOSE FIT", "STABLE FIT" };
        private static readonly float[] ShadowNearPlaneValues = { 0f, 1f, 2f, 3f };

        // loaded settings
        public static int maxQueuedIdx = -1;
        public static int softParticlesVal = -1;     
        public static int realtimeReflVal = -1;
        public static int particleBudgetIdx = -1;
        public static int shadowModeIdx = -1;         
        public static int shadowDistanceIdx = -1;
        public static int shadowResolutionIdx = -1;
        public static int shadowCascadeIdx = -1;
        public static int shadowProjectionIdx = -1;
        public static int shadowNearPlaneIdx = -1;

        private static bool initialized = false;
        private static int defMaxQueued, defParticleBudget;
        private static bool defSoftParticles, defRealtimeRefl;

        public static void ApplyQualitySettings(){
            if (GameplayManager.IsDedicatedServer())
                return;
            if (!initialized){
                defMaxQueued = QualitySettings.maxQueuedFrames;
                defSoftParticles = QualitySettings.softParticles;
                defRealtimeRefl = QualitySettings.realtimeReflectionProbes;
                defParticleBudget = QualitySettings.particleRaycastBudget;
                initialized = true;
            }
            QualitySettings.maxQueuedFrames = maxQueuedIdx >= 0 ? MaxQueuedValues[maxQueuedIdx] : defMaxQueued;
            QualitySettings.softParticles = softParticlesVal >= 0 ? softParticlesVal != 0 : defSoftParticles;
            QualitySettings.realtimeReflectionProbes = realtimeReflVal >= 0 ? realtimeReflVal != 0 : defRealtimeRefl;
            QualitySettings.particleRaycastBudget = particleBudgetIdx >= 0 ? ParticleBudgetValues[particleBudgetIdx] : defParticleBudget;
            MenuManager.SetGraphicsShadowQuality(MenuManager.gfx_shadow_quality);
            if (shadowModeIdx >= 0) QualitySettings.shadows = (ShadowQuality)shadowModeIdx;
            if (shadowDistanceIdx >= 0) QualitySettings.shadowDistance = ShadowDistanceValues[shadowDistanceIdx];
            if (shadowResolutionIdx >= 0) QualitySettings.shadowResolution = (ShadowResolution)shadowResolutionIdx;
            if (shadowCascadeIdx >= 0) QualitySettings.shadowCascades = ShadowCascadeValues[shadowCascadeIdx];
            if (shadowProjectionIdx >= 0) QualitySettings.shadowProjection = (ShadowProjection)shadowProjectionIdx;
            if (shadowNearPlaneIdx >= 0) QualitySettings.shadowNearPlaneOffset = ShadowNearPlaneValues[shadowNearPlaneIdx];
        }

        private const string DEFAULT = "DEFAULT";
        private static string Toggle(int v) { return v < 0 ? Loc.LS(DEFAULT) : MenuManager.GetToggleSetting(v); }

        public static string GetMaxQueued() { return maxQueuedIdx < 0 ? Loc.LS(DEFAULT) : MaxQueuedValues[maxQueuedIdx].ToString(); }
        public static string GetSoftParticles() { return Toggle(softParticlesVal); }
        public static string GetRealtimeReflections() { return Toggle(realtimeReflVal); }
        public static string GetParticleBudget() { return particleBudgetIdx < 0 ? Loc.LS(DEFAULT) : ParticleBudgetValues[particleBudgetIdx].ToString(); }
        public static string GetShadowMode() { return shadowModeIdx < 0 ? Loc.LS(DEFAULT) : Loc.LS(ShadowModeNames[shadowModeIdx]); }
        public static string GetShadowDistance() { return shadowDistanceIdx < 0 ? Loc.LS(DEFAULT) : ShadowDistanceValues[shadowDistanceIdx].ToString(); }
        public static string GetShadowResolution() { return shadowResolutionIdx < 0 ? Loc.LS(DEFAULT) : Loc.LS(ShadowResolutionNames[shadowResolutionIdx]); }
        public static string GetShadowCascades() { return shadowCascadeIdx < 0 ? Loc.LS(DEFAULT) : ShadowCascadeValues[shadowCascadeIdx].ToString(); }
        public static string GetShadowProjection() { return shadowProjectionIdx < 0 ? Loc.LS(DEFAULT) : Loc.LS(ShadowProjectionNames[shadowProjectionIdx]); }
        public static string GetShadowNearPlane() { return shadowNearPlaneIdx < 0 ? Loc.LS(DEFAULT) : ShadowNearPlaneValues[shadowNearPlaneIdx].ToString("0.##"); }

        public static int CycleDefault(int value, int dir, int len){
            int total = len + 1;
            return ((value + 1 + dir) % total + total) % total - 1;
        }

        [HarmonyPatch(typeof(MenuManager), "LoadPreferences")]
        class GfxQuality_LoadPreferences{
            static void Postfix(){
                maxQueuedIdx = Clamp(MenuManager.LocalGetInt("GFX_QS_MAXQUEUED", -1), MaxQueuedValues.Length);
                softParticlesVal = Clamp(MenuManager.LocalGetInt("GFX_QS_SOFT_PARTICLES", -1), 2);
                realtimeReflVal = Clamp(MenuManager.LocalGetInt("GFX_QS_RT_REFLECTIONS", -1), 2);
                particleBudgetIdx = Clamp(MenuManager.LocalGetInt("GFX_QS_PARTICLE_BUDGET", -1), ParticleBudgetValues.Length);
                shadowModeIdx = Clamp(MenuManager.LocalGetInt("GFX_QS_SHADOW_MODE", -1), ShadowModeNames.Length);
                shadowDistanceIdx = Clamp(MenuManager.LocalGetInt("GFX_QS_SHADOW_DIST", -1), ShadowDistanceValues.Length);
                shadowResolutionIdx = Clamp(MenuManager.LocalGetInt("GFX_QS_SHADOW_RES", -1), ShadowResolutionNames.Length);
                shadowCascadeIdx = Clamp(MenuManager.LocalGetInt("GFX_QS_SHADOW_CASCADES", -1), ShadowCascadeValues.Length);
                shadowProjectionIdx = Clamp(MenuManager.LocalGetInt("GFX_QS_SHADOW_PROJ", -1), ShadowProjectionNames.Length);
                shadowNearPlaneIdx = Clamp(MenuManager.LocalGetInt("GFX_QS_SHADOW_NEARPLANE", -1), ShadowNearPlaneValues.Length);
            }

            static int Clamp(int idx, int len) {
                 return Mathf.Clamp(idx, -1, len - 1); 
            }
        }

        [HarmonyPatch(typeof(MenuManager), "SavePreferences")]
        class GfxQuality_SavePreferences{
            static void Postfix(){
                MenuManager.LocalSetInt("GFX_QS_MAXQUEUED", maxQueuedIdx);
                MenuManager.LocalSetInt("GFX_QS_SOFT_PARTICLES", softParticlesVal);
                MenuManager.LocalSetInt("GFX_QS_RT_REFLECTIONS", realtimeReflVal);
                MenuManager.LocalSetInt("GFX_QS_PARTICLE_BUDGET", particleBudgetIdx);
                MenuManager.LocalSetInt("GFX_QS_SHADOW_MODE", shadowModeIdx);
                MenuManager.LocalSetInt("GFX_QS_SHADOW_DIST", shadowDistanceIdx);
                MenuManager.LocalSetInt("GFX_QS_SHADOW_RES", shadowResolutionIdx);
                MenuManager.LocalSetInt("GFX_QS_SHADOW_CASCADES", shadowCascadeIdx);
                MenuManager.LocalSetInt("GFX_QS_SHADOW_PROJ", shadowProjectionIdx);
                MenuManager.LocalSetInt("GFX_QS_SHADOW_NEARPLANE", shadowNearPlaneIdx);
            }
        }

        [HarmonyPatch(typeof(MenuManager), "ApplyAllGraphicsSettings")]
        class GfxQuality_ApplyAllGraphicsSettings{
            static void Postfix() { ApplyQualitySettings(); }
        }

        [HarmonyPatch(typeof(GameplayManager), "StartLevel")]
        class GfxQuality_StartLevel{
            static void Postfix() { ApplyQualitySettings(); }
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawGraphicsAdvancedMenu")]
    class GfxQuality_UIElement_DrawGraphicsAdvancedMenu
    {
        static bool Prefix(UIElement __instance)
        {
            UIManager.ui_bg_dark = true;
            Vector2 position = __instance.m_position;
            __instance.DrawMenuBG();
            UIElement.ToolTipActive = false;
            position.y = UIManager.UI_TOP + 20f;
            __instance.DrawHeaderMedium(position, Loc.LS("ADVANCED GRAPHICS OPTIONS"));
            switch (MenuManager.m_menu_micro_state)
            {
                case 0:
                    position.y += 40f;
                    __instance.DrawSmallHeader1(position, Loc.LS("FULL-SCREEN EFFECTS"), 250f);
                    position.y = -232.5f;
                    __instance.DrawMenuSeparator(position - Vector2.up * 40f);
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("ANISOTROPIC FILTER"), position, 2, MenuManager.GetToggleSetting(MenuManager.gfx_ani_filter), string.Empty);
                    position.y += 62f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("AMBIENT OCCLUSION"), position, 3, MenuManager.GetToggleSetting(MenuManager.gfx_ssao), string.Empty);
                    position.y += 62f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("ANTI-ALIASING"), position, 4, MenuManager.GetAntiAliasing(), string.Empty);
                    position.y += 62f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("BLOOM"), position, 5, MenuManager.GetBloom(), string.Empty);
                    position.y += 62f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("POST-PROCESSING"), position, 6, MenuManager.GetPostProcessing(), string.Empty);
                    position.y += 62f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("SCREEN-SPACE REFLECTIONS"), position, 7, MenuManager.GetToggleSetting(MenuManager.gfx_ssr), string.Empty);
                    position.y += 62f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("LENS FLARES"), position, 1, MenuManager.GetToggleSetting(MenuManager.gfx_lens_flare), (!GameplayManager.VRActive) ? string.Empty : Loc.LS("LENS FLARES DON'T WORK IN VR.  PLEASE DISABLE MANUALLY."));
                    position.y += 62f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("TEXTURE RESOLUTION"), position, 0, MenuManager.GetTextureLevel(), string.Empty);
                    break;
                case 1:
                    position.y += 40f;
                    __instance.DrawSmallHeader1(position, Loc.LS("CUSTOM OPTIMIZATIONS"), 250f);
                    position.y = -232.5f;
                    __instance.DrawMenuSeparator(position - Vector2.up * 40f);
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("POWERUP LIGHT FADING"), position, 5, MenuManager.GetLightPowerupFade(), Loc.LS("POWERUP-CREATED LIGHTS FADE AT A DISTANCE"));
                    position.y += 62f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("DISTANCE LIGHT FADING"), position, 3, MenuManager.GetToggleSetting(MenuManager.gfx_light_distance_fade), Loc.LS("LEVEL LIGHTS FADE AWAY AT A DISTANCE AND OFF-SCREEN"));
                    position.y += 62f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("REDUCED SHADERS"), position, 7, MenuManager.GetToggleSetting(MenuManager.gfx_shaders_simpler), Loc.LS("USES ALTERNATE DISTORTION AND ROBOT SHADER (GAME RESTART REQUIRED)"));
                    position.y += 62f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("REDUCED SHADOW LIGHTS"), position, 2, MenuManager.GetToggleSetting(MenuManager.gfx_reduced_shadows), Loc.LS("CHANGES SOME LEVEL LIGHTS TO NON-SHADOW-CASTING (LEVEL RESTART REQUIRED)"));
                    position.y += 62f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("LIGHT RANGE"), position, 4, MenuManager.GetGFXReduction(MenuManager.gfx_light_reduced_range), Loc.LS("ALL LEVEL LIGHTS HAVE A SLIGHTLY SMALLER RANGE (LEVEL RESTART REQUIRED)"));
                    position.y += 62f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("PARTICLE COUNT"), position, 6, MenuManager.GetGFXReduction(MenuManager.gfx_particles_reduced), Loc.LS("REDUCES COUNT OF MOST TYPES OF PARTICLES (GAME RESTART REQUIRED)"));
                    position.y += 62f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("ROBOT EXPLOSIONS"), position, 1, MenuManager.GetGFXReduction(MenuManager.gfx_explosions_reduced), Loc.LS("AFFECTS AMOUNT OF DEBRIS AND SOME PARTICLE TYPES FROM EXPLOSIONS"));
                    position.y += 62f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("EFFECTS LIGHTING"), position, 0, MenuManager.GetFXLighting(), Loc.LS("AFFECTS LIGHTS FOR PROJECTILES AND OTHER DYNAMIC LIGHTS"));
                    break;
                case 2:
                    position.y += 40f;
                    __instance.DrawSmallHeader1(position, Loc.LS("QUALITY SETTINGS"), 250f);
                    position.y = -232.5f;
                    __instance.DrawMenuSeparator(position - Vector2.up * 40f);
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("MAX QUEUED FRAMES"), position, 0, GfxQualitySettings.GetMaxQueued(), Loc.LS("FRAMES THE CPU MAY QUEUE AHEAD OF THE GPU; LOWER REDUCES INPUT LAG"));
                    position.y += 48f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("SOFT PARTICLES"), position, 1, GfxQualitySettings.GetSoftParticles(), Loc.LS("FADE PARTICLES NEAR GEOMETRY (COSTS DEPTH TEXTURE)"));
                    position.y += 48f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("REALTIME REFLECTIONS"), position, 2, GfxQualitySettings.GetRealtimeReflections(), Loc.LS("UPDATE REFLECTION PROBES EVERY FRAME"));
                    position.y += 48f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("PARTICLE RAYCAST BUDGET"), position, 3, GfxQualitySettings.GetParticleBudget(), Loc.LS("MAX RAYCASTS FOR APPROXIMATE PARTICLE COLLISIONS"));
                    position.y += 48f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("SHADOW MODE"), position, 4, GfxQualitySettings.GetShadowMode(), Loc.LS("OFF/HARD/SOFT SHADOWS (OVERRIDES SHADOW SETTINGS)"));
                    position.y += 48f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("SHADOW DISTANCE"), position, 5, GfxQualitySettings.GetShadowDistance(), Loc.LS("MAX DISTANCE SHADOWS ARE DRAWN (OVERRIDES SHADOW SETTINGS)"));
                    position.y += 48f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("SHADOW RESOLUTION"), position, 6, GfxQualitySettings.GetShadowResolution(), Loc.LS("SHADOW MAP RESOLUTION (OVERRIDES SHADOW SETTINGS)"));
                    position.y += 48f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("SHADOW CASCADES"), position, 7, GfxQualitySettings.GetShadowCascades(), Loc.LS("DIRECTIONAL SHADOW CASCADE COUNT (OVERRIDES SHADOW SETTINGS)"));
                    position.y += 48f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("SHADOW PROJECTION"), position, 8, GfxQualitySettings.GetShadowProjection(), Loc.LS("STABLE FIT REDUCES SHADOW EDGE SHIMMER"));
                    position.y += 48f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("SHADOW NEAR PLANE OFFSET"), position, 9, GfxQualitySettings.GetShadowNearPlane(), Loc.LS("REDUCES SHADOW SELF-SHADOWING ARTIFACTS"));
                    break;
            }
            __instance.DrawMenuSeparator(position + Vector2.up * 40f);
            __instance.DrawMenuToolTip(position + Vector2.up * 40f);
            __instance.DrawPageControls(position + Vector2.up * 85f, string.Format(Loc.LS("PAGE {0} OF {1}"), MenuManager.m_menu_micro_state + 1, 3), true, true, false, false, 420, 302);
            position.x = 0f;
            position.y = UIManager.UI_BOTTOM - 30f;
            __instance.SelectAndDrawItem(Loc.LS("BACK"), position, 100, false);
            __instance.MaybeShowMpStatus();
            return false;
        }
    }

    [HarmonyPatch(typeof(MenuManager), "GraphicsAdvancedUpdate")]
    class GfxQuality_MenuManager_GraphicsAdvancedUpdate
    {
        private static readonly MethodInfo _GoBack = AccessTools.Method(typeof(MenuManager), "GoBack");
        private static readonly MethodInfo _PlayHighlightSound = AccessTools.Method(typeof(MenuManager), "PlayHighlightSound");
        private static readonly FieldInfo _m_menu_state_timer = AccessTools.Field(typeof(MenuManager), "m_menu_state_timer");

        static bool Prefix()
        {
            MenuManager.UpdateMPStatus();
            UIManager.MouseSelectUpdate();
            switch (MenuManager.m_menu_sub_state)
            {
                case MenuSubState.INIT:
                    if ((float)_m_menu_state_timer.GetValue(null) > 0.25f)
                    {
                        UIManager.CreateUIElement(UIManager.SCREEN_CENTER, 7000, UIElementType.GRAPHICS_ADVANCED_MENU);
                        MenuManager.m_menu_sub_state = MenuSubState.ACTIVE;
                        MenuManager.SetDefaultSelection(0);
                        MenuManager.m_menu_micro_state = 0;
                    }
                    break;
                case MenuSubState.ACTIVE:
                    UIManager.ControllerMenu();
                    if (Controls.JustPressed(CCInput.MENU_SECONDARY))
                    {
                        MenuManager.m_mp_status_minimized = !MenuManager.m_mp_status_minimized;
                        MenuManager.PlayCycleSound();
                    }
                    if (Controls.JustPressed(CCInput.MENU_PGUP) || (UIManager.PushedSelect() && UIManager.m_menu_selection == 198))
                    {
                        _PlayHighlightSound.Invoke(null, new object[] { 0.4f, 0.05f });
                        MenuManager.UIPulse();
                        MenuManager.m_menu_micro_state = (MenuManager.m_menu_micro_state + 2) % 3;
                        break;
                    }
                    if (Controls.JustPressed(CCInput.MENU_PGDN) || (UIManager.PushedSelect() && UIManager.m_menu_selection == 199))
                    {
                        _PlayHighlightSound.Invoke(null, new object[] { 0.4f, 0.05f });
                        MenuManager.UIPulse();
                        MenuManager.m_menu_micro_state = (MenuManager.m_menu_micro_state + 1) % 3;
                        break;
                    }
                    if (UIManager.PushedSelect(100) || (MenuManager.option_dir && UIManager.PushedDir()))
                    {
                        MenuManager.MaybeReverseOption();
                        switch (MenuManager.m_menu_micro_state)
                        {
                            case 0:
                                switch (UIManager.m_menu_selection)
                                {
                                    case 0:
                                        MenuManager.gfx_tex_level = (MenuManager.gfx_tex_level + 3 + UIManager.m_select_dir) % 3;
                                        MenuManager.PlayCycleSound(1f, UIManager.m_select_dir);
                                        MenuManager.SetTextureLevel(MenuManager.gfx_tex_level);
                                        break;
                                    case 1:
                                        MenuManager.gfx_lens_flare = (MenuManager.gfx_lens_flare + 1) % 2;
                                        MenuManager.PlayCycleSound(1f, UIManager.m_select_dir);
                                        MenuManager.SetCameraGraphics();
                                        break;
                                    case 2:
                                        MenuManager.gfx_ani_filter = (MenuManager.gfx_ani_filter + 1) % 2;
                                        MenuManager.PlayCycleSound(1f, UIManager.m_select_dir);
                                        MenuManager.SetAniFilter(MenuManager.gfx_ani_filter == 1);
                                        break;
                                    case 3:
                                        MenuManager.gfx_ssao = (MenuManager.gfx_ssao + 1) % 2;
                                        MenuManager.PlayCycleSound(1f, UIManager.m_select_dir);
                                        MenuManager.SetCameraGraphics();
                                        break;
                                    case 4:
                                        MenuManager.gfx_smaa = (MenuManager.gfx_smaa + 1) % 2;
                                        MenuManager.PlayCycleSound(1f, UIManager.m_select_dir);
                                        MenuManager.SetCameraGraphics();
                                        break;
                                    case 5:
                                        MenuManager.gfx_bloom = (MenuManager.gfx_bloom + 3 + UIManager.m_select_dir) % 3;
                                        MenuManager.PlayCycleSound(1f, UIManager.m_select_dir);
                                        MenuManager.SetCameraGraphics();
                                        break;
                                    case 6:
                                        MenuManager.gfx_post = (MenuManager.gfx_post + 4 + UIManager.m_select_dir) % 4;
                                        MenuManager.PlayCycleSound(1f, UIManager.m_select_dir);
                                        MenuManager.SetCameraGraphics();
                                        break;
                                    case 7:
                                        MenuManager.gfx_ssr = (MenuManager.gfx_ssr + 1) % 2;
                                        MenuManager.PlayCycleSound(1f, UIManager.m_select_dir);
                                        MenuManager.SetCameraGraphics();
                                        break;
                                    case 100:
                                        _GoBack.Invoke(null, null);
                                        UIManager.DestroyAll();
                                        MenuManager.PlaySelectSound();
                                        break;
                                }
                                break;
                            case 1:
                                switch (UIManager.m_menu_selection)
                                {
                                    case 0:
                                        MenuManager.gfx_fx_lighting = (MenuManager.gfx_fx_lighting + 3 + UIManager.m_select_dir) % 3;
                                        MenuManager.PlayCycleSound(1f, UIManager.m_select_dir);
                                        break;
                                    case 1:
                                        MenuManager.gfx_explosions_reduced = (MenuManager.gfx_explosions_reduced + 2 + UIManager.m_select_dir) % 2;
                                        MenuManager.PlayCycleSound(1f, UIManager.m_select_dir);
                                        break;
                                    case 2:
                                        MenuManager.gfx_reduced_shadows = (MenuManager.gfx_reduced_shadows + 2 + UIManager.m_select_dir) % 2;
                                        MenuManager.PlayCycleSound(1f, UIManager.m_select_dir);
                                        break;
                                    case 3:
                                        MenuManager.gfx_light_distance_fade = (MenuManager.gfx_light_distance_fade + 2 + UIManager.m_select_dir) % 2;
                                        MenuManager.PlayCycleSound(1f, UIManager.m_select_dir);
                                        break;
                                    case 4:
                                        MenuManager.gfx_light_reduced_range = (MenuManager.gfx_light_reduced_range + 2 + UIManager.m_select_dir) % 2;
                                        MenuManager.PlayCycleSound(1f, UIManager.m_select_dir);
                                        break;
                                    case 5:
                                        MenuManager.gfx_light_powerup_fade = (MenuManager.gfx_light_powerup_fade + 3 + UIManager.m_select_dir) % 3;
                                        MenuManager.PlayCycleSound(1f, UIManager.m_select_dir);
                                        break;
                                    case 6:
                                        MenuManager.gfx_particles_reduced = (MenuManager.gfx_particles_reduced + 2 + UIManager.m_select_dir) % 2;
                                        MenuManager.PlayCycleSound(1f, UIManager.m_select_dir);
                                        break;
                                    case 7:
                                        MenuManager.gfx_shaders_simpler = (MenuManager.gfx_shaders_simpler + 2 + UIManager.m_select_dir) % 2;
                                        MenuManager.PlayCycleSound(1f, UIManager.m_select_dir);
                                        break;
                                    case 100:
                                        _GoBack.Invoke(null, null);
                                        UIManager.DestroyAll();
                                        MenuManager.PlaySelectSound();
                                        break;
                                }
                                break;
                            case 2:
                                switch (UIManager.m_menu_selection)
                                {
                                    case 0: GfxQualitySettings.maxQueuedIdx = GfxQualitySettings.CycleDefault(GfxQualitySettings.maxQueuedIdx, UIManager.m_select_dir, 4); break;
                                    case 1: GfxQualitySettings.softParticlesVal = GfxQualitySettings.CycleDefault(GfxQualitySettings.softParticlesVal, UIManager.m_select_dir, 2); break;
                                    case 2: GfxQualitySettings.realtimeReflVal = GfxQualitySettings.CycleDefault(GfxQualitySettings.realtimeReflVal, UIManager.m_select_dir, 2); break;
                                    case 3: GfxQualitySettings.particleBudgetIdx = GfxQualitySettings.CycleDefault(GfxQualitySettings.particleBudgetIdx, UIManager.m_select_dir, 6); break;
                                    case 4: GfxQualitySettings.shadowModeIdx = GfxQualitySettings.CycleDefault(GfxQualitySettings.shadowModeIdx, UIManager.m_select_dir, 3); break;
                                    case 5: GfxQualitySettings.shadowDistanceIdx = GfxQualitySettings.CycleDefault(GfxQualitySettings.shadowDistanceIdx, UIManager.m_select_dir, 5); break;
                                    case 6: GfxQualitySettings.shadowResolutionIdx = GfxQualitySettings.CycleDefault(GfxQualitySettings.shadowResolutionIdx, UIManager.m_select_dir, 4); break;
                                    case 7: GfxQualitySettings.shadowCascadeIdx = GfxQualitySettings.CycleDefault(GfxQualitySettings.shadowCascadeIdx, UIManager.m_select_dir, 3); break;
                                    case 8: GfxQualitySettings.shadowProjectionIdx = GfxQualitySettings.CycleDefault(GfxQualitySettings.shadowProjectionIdx, UIManager.m_select_dir, 2); break;
                                    case 9: GfxQualitySettings.shadowNearPlaneIdx = GfxQualitySettings.CycleDefault(GfxQualitySettings.shadowNearPlaneIdx, UIManager.m_select_dir, 4); break;
                                    case 100:
                                        _GoBack.Invoke(null, null);
                                        UIManager.DestroyAll();
                                        MenuManager.PlaySelectSound();
                                        break;
                                }
                                if (UIManager.m_menu_selection != 100)
                                {
                                    MenuManager.PlayCycleSound(1f, UIManager.m_select_dir);
                                    GfxQualitySettings.ApplyQualitySettings();
                                }
                                break;
                        }
                    }
                    MenuManager.UnReverseOption();
                    break;
            }
            return false;
        }
    }
}
