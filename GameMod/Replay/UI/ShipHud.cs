using System;
using Overload;
using UnityEngine;

namespace GameMod.Replay
{
    public static class ShipHud
    {
        private const float LOW_ARMOR_HITPOINTS = 50f;

        private const float LOW_ENERGY_THRESHOLD = LOW_ARMOR_HITPOINTS;
        private const float BAR_HALF_WIDTH = 120f;
        private const float BANNER_WIDTH = 284f;
        private const float WEAPON_BAR_WIDTH = 210f;
        private const float WEAPON_PANEL_HALF_WIDTH = 125f;
        private const int LOADOUT_SLOTS = 8;

        private const int SPRITE_WEAPON_ICONS = 26;
        private const int SPRITE_MISSILE_ICONS = 104;
        private const int SPRITE_ARMOR_ICON = 59;
        private const int SPRITE_ENERGY_ICON = 58;
        private const int SPRITE_AMMO_ICON = 57;
        private const int SPRITE_LOW_ARMOR_ARROW = 56;
        private const int SPRITE_BRACKET_WIDE = 42;
        private const int SPRITE_BRACKET = 41;

        private static float WeaponPanelDrop
        {
            get { return GameplayManager.VRActive ? 25f : 50f; }
        }

        private const int AMMO_PER_PIP = 20;

        public const float BANNER_STEP = 35f;
        public static float BlockY { get { return UIManager.UI_TOP - 128f; } }
        public static float BannerY { get { return BlockY - BANNER_STEP; } }

        public static void Draw(UIElement ui, ShipState state, MpTeam team, bool teamMatch,
            float maxHitpoints)
        {
            bool weaponsOnLeft = MenuManager.opt_hud_weapons != 0;

            Vector2 at = new Vector2(0f, BlockY);
            DrawArmor(ui, at, state, team, teamMatch, maxHitpoints);
            at.y += 35f;
            DrawEnergyAmmo(ui, at, state, maxHitpoints);
            at.y += 70f;
            if (state.HasOverdrive)
            {
                Color bar = Color.Lerp(UIManager.m_col_overdrive0, UIManager.m_col_overdrive1,
                    UnityEngine.Random.value * UIElement.FLICKER);
                Color labelColor = Color.Lerp(UIManager.m_col_overdrive1, UIManager.m_col_overdrive2,
                    UnityEngine.Random.value * UIElement.FLICKER);
                bar.a = ui.m_alpha;
                UIManager.DrawQuadBarHorizontal(at, 14f, 14f, BANNER_WIDTH, bar, 8);
                ui.DrawStringSmall(Loc.LS("OVERDRIVE"), new Vector2(at.x - 1f, at.y), 0.6f,
                    StringOffset.CENTER, labelColor, 1f);
            }

            at.y = BlockY;
            at.x = weaponsOnLeft ? -448f : 448f;
            DrawPrimaryWeapon(ui, at, state, team, teamMatch);
            at.x = weaponsOnLeft ? 448f : -448f;
            DrawSecondaryWeapon(ui, at, state, team, teamMatch);
        }

        private static void DrawArmor(UIElement ui, Vector2 pos, ShipState state,
            MpTeam team, bool teamMatch, float maxHitpoints)
        {
            float hitpoints = state.Hitpoints(maxHitpoints);
            float lowArmorRamp = Mathf.Clamp01(0.2f + (LOW_ARMOR_HITPOINTS - hitpoints) / LOW_ARMOR_HITPOINTS);
            float lowArmorFlash = lowArmorRamp * (0.2f + ui.m_anim_state2 * 0.6f);

            Color frame = UIManager.m_col_ub1;
            Color digits = UIManager.m_col_ui2;
            Color fill = Color.Lerp(UIManager.m_col_armor0, UIManager.m_col_armor1,
                UnityEngine.Random.value * UIElement.FLICKER);

            bool showTeamBanner = teamMatch && team != MpTeam.ANARCHY && state.alive;

            if (state.IsCloaked && state.alive)
            {
                pos.y -= 35f;
                Color cloakBar = Color.Lerp(UIManager.m_col_overdrive0, UIManager.m_col_overdrive1,
                    UnityEngine.Random.value * UIElement.FLICKER);
                Color cloakLabel = Color.Lerp(UIManager.m_col_overdrive3, UIManager.m_col_overdrive4,
                    UnityEngine.Random.value * UIElement.FLICKER);
                cloakBar.a = ui.m_alpha;
                cloakLabel.a = ui.m_alpha;
                UIManager.DrawQuadBarHorizontal(pos, 14f, 14f, BANNER_WIDTH, cloakBar, 8);
                ui.DrawStringSmall(Loc.LS("CLOAK"), new Vector2(pos.x - 1f, pos.y), 0.6f,
                    StringOffset.CENTER, cloakLabel, 1f);
                pos.y += 35f;
            }
            else if (showTeamBanner)
            {
                pos.y -= BANNER_STEP;

                frame = Hud.TeamColor((int)team, 1);
                digits = Hud.TeamColor((int)team, 3);
                frame.a = ui.m_alpha;
                digits.a = ui.m_alpha;
                UIManager.DrawQuadBarHorizontal(pos, 14f, 14f, BANNER_WIDTH, frame, 7);

                Vector2 bannerAt = new Vector2(pos.x - 1f, pos.y);
                if (hitpoints < LOW_ARMOR_HITPOINTS)
                {
                    digits = Color.Lerp(UIManager.m_col_hi5, UIManager.m_col_em6, lowArmorFlash);
                    ui.DrawStringSmall(Loc.LS("LOW ARMOR"), bannerAt, 0.6f, StringOffset.CENTER,
                        digits, 1f, BANNER_WIDTH);
                }
                else
                {
                    ui.DrawStringSmall(MPTeams.TeamName(team) + " " + Loc.LS("TEAM"), bannerAt,
                        0.6f, StringOffset.CENTER, digits, 1f, BANNER_WIDTH);
                }
                pos.y += BANNER_STEP;
                frame = UIManager.m_col_ub1;
                digits = UIManager.m_col_ui2;
            }

            if (state.IsInvulnerable && state.alive)
            {
                Color invulnBar = Color.Lerp(UIManager.m_col_invuln2, UIManager.m_col_invuln3,
                    UnityEngine.Random.value * UIElement.FLICKER);
                Color invulnLabel = Color.Lerp(UIManager.m_col_invuln4, UIManager.m_col_invuln5,
                    UnityEngine.Random.value * UIElement.FLICKER);
                invulnBar.a = ui.m_alpha;
                UIManager.DrawQuadBarHorizontal(pos, 14f, 14f, BANNER_WIDTH, invulnBar, 8);
                ui.DrawStringSmall(Loc.LS("INVULNERABLE"), new Vector2(pos.x - 1f, pos.y), 0.6f,
                    StringOffset.CENTER, invulnLabel, 1f);
                return;
            }

            if (state.alive && !state.IsCloaked && !showTeamBanner && hitpoints < LOW_ARMOR_HITPOINTS)
            {
                pos.y -= 35f;
                Color warning = Color.Lerp(UIManager.m_col_hi5, UIManager.m_col_em6, lowArmorFlash);
                warning.a = ui.m_alpha;
                UIManager.DrawQuadBarHorizontal(pos, 14f, 14f, 224f, warning, 12);
                ui.DrawStringSmall(Loc.LS("LOW ARMOR"), pos, 0.6f, StringOffset.CENTER,
                    UIManager.m_col_black, 1f, 200f);
                UIManager.DrawSpriteUI(new Vector2(pos.x - 112f, pos.y), 0.15f, 0.15f,
                    UIManager.m_col_black, ui.m_alpha, SPRITE_LOW_ARMOR_ARROW);
                UIManager.DrawSpriteUI(new Vector2(pos.x + 112f, pos.y), 0.15f, 0.15f,
                    UIManager.m_col_black, ui.m_alpha, SPRITE_LOW_ARMOR_ARROW);
                pos.y += 35f;
            }

            pos.x -= 142f;
            UIManager.DrawQuadUI(pos, 14f, 14f, frame, ui.m_alpha, 8);
            UIManager.DrawSpriteUI(pos, 0.15f, 0.15f, fill, ui.m_alpha, SPRITE_ARMOR_ICON);
            pos.x += 300f;
            ui.DrawOutlineBackdrop(pos, 14f, 30f, frame, 2);

            ui.DrawDigitsThree(pos, state.alive ? Mathf.Max(Mathf.RoundToInt(hitpoints), 1) : 0,
                0.75f, StringOffset.CENTER, digits, ui.m_alpha);

            frame = UIManager.m_col_ub1;
            pos.x = 0f - BAR_HALF_WIDTH;
            float markStride = BAR_HALF_WIDTH * 0.0833333f;
            for (int mark = 0; mark < 25; mark++)
            {
                UIManager.DrawSpriteUIRotated(pos, 0.25f, 0.25f, (float)Math.PI / 2f, frame,
                    ui.m_alpha, (mark % 4 == 0) ? SPRITE_BRACKET_WIDE : SPRITE_BRACKET);
                pos.x += markStride;
            }

            pos.x = 0f;
            float fillWidth = BAR_HALF_WIDTH * (hitpoints / maxHitpoints);
            Color pulse = Color.Lerp(UIManager.m_col_em5, UIManager.m_col_em6,
                UnityEngine.Random.value * UIElement.FLICKER);
            if (hitpoints < LOW_ARMOR_HITPOINTS)
                frame = Color.Lerp(frame, pulse, lowArmorFlash);
            frame.a = ui.m_alpha;
            UIManager.DrawQuadBarHorizontal(pos, 14f, 14f, 224f, frame, 8);
            pos.x -= BAR_HALF_WIDTH - fillWidth;
            UIManager.DrawQuadUIInner(pos, fillWidth, 8f, fill, ui.m_alpha, 11, 0.9f);
        }

        private static void DrawEnergyAmmo(UIElement ui, Vector2 pos, ShipState state,
            float maxEnergy)
        {
            float energy = state.energy + 0.5f;
            float lowEnergyRamp = Mathf.Clamp01(0.2f + (LOW_ENERGY_THRESHOLD - energy) / LOW_ENERGY_THRESHOLD);
            bool hasOverdrive = state.HasOverdrive;

            Color frame = UIManager.m_col_ub1;
            Color digits = UIManager.m_col_ui2;
            Color fill = Color.Lerp(UIManager.m_col_hi5, UIManager.m_col_hi6,
                UnityEngine.Random.value * UIElement.FLICKER);
            if (energy < LOW_ENERGY_THRESHOLD)
            {
                Color warning = Color.Lerp(UIManager.m_col_em5, UIManager.m_col_em6,
                    UnityEngine.Random.value * UIElement.FLICKER);
                digits = Color.Lerp(UIManager.m_col_hi5, warning, lowEnergyRamp);
            }
            if (hasOverdrive)
            {
                frame = Color.Lerp(UIManager.m_col_overdrive0, UIManager.m_col_overdrive1,
                    UnityEngine.Random.value * UIElement.FLICKER);
                digits = Color.Lerp(UIManager.m_col_overdrive1, UIManager.m_col_overdrive2,
                    UnityEngine.Random.value * UIElement.FLICKER);
                fill = Color.Lerp(UIManager.m_col_overdrive3, UIManager.m_col_overdrive4,
                    UnityEngine.Random.value * UIElement.FLICKER);
            }

            pos.x -= 142f;
            UIManager.DrawQuadUI(pos, 14f, 14f, frame, ui.m_alpha, 8);
            UIManager.DrawSpriteUI(pos, 0.15f, 0.15f, fill, ui.m_alpha, SPRITE_ENERGY_ICON);
            pos.x += 300f;
            ui.DrawOutlineBackdrop(pos, 14f, 30f, frame, 2);
            ui.DrawDigitsThree(pos, (int)energy, 0.75f, StringOffset.CENTER, digits, ui.m_alpha);

            pos.x = 0f;
            float fillWidth = BAR_HALF_WIDTH * (energy / maxEnergy);
            for (int mark = -10; mark <= 10; mark++)
            {
                Vector2 tick = new Vector2(pos.x + BAR_HALF_WIDTH * 0.1f * mark, pos.y - 8f);
                UIManager.DrawQuadUI(tick, 1f, 3f, frame, ui.m_alpha, 12);
                tick.y = pos.y + 8f;
                UIManager.DrawQuadUI(tick, 1f, 3f, frame, ui.m_alpha, 12);
            }
            if (energy > 0f)
            {
                Vector2 barAt = new Vector2(pos.x - (BAR_HALF_WIDTH - fillWidth), pos.y);
                UIManager.DrawQuadUIInner(barAt, fillWidth, 4f, fill, ui.m_alpha, 11, 0.9f);
            }
            UIManager.DrawQuadUIInner(new Vector2(pos.x, pos.y - 11f), 125f, 2f, frame,
                ui.m_alpha, 11, 0.9f);
            UIManager.DrawQuadUIInner(new Vector2(pos.x, pos.y + 11f), 125f, 2f, frame,
                ui.m_alpha, 11, 0.9f);

            frame = hasOverdrive
                ? Color.Lerp(UIManager.m_col_overdrive0, UIManager.m_col_overdrive1,
                    UnityEngine.Random.value * UIElement.FLICKER)
                : UIManager.m_col_ub1;
            Color ammoColor = Color.Lerp(UIManager.m_col_ammo0, UIManager.m_col_ammo1,
                UnityEngine.Random.value * UIElement.FLICKER);
            pos.y += 35f;
            pos.x = -142f;
            UIManager.DrawQuadUI(pos, 14f, 14f, frame, ui.m_alpha, 8);
            UIManager.DrawSpriteUI(pos, 0.15f, 0.15f, ammoColor, ui.m_alpha, SPRITE_AMMO_ICON);
            if (hasOverdrive)
            {
                ammoColor = Color.Lerp(UIManager.m_col_overdrive1, UIManager.m_col_overdrive2,
                    UnityEngine.Random.value * UIElement.FLICKER);
            }
            pos.x += 300f;
            ui.DrawOutlineBackdrop(pos, 14f, 30f, frame, 2);
            ui.DrawDigitsThree(pos, state.ammo, 0.75f, StringOffset.CENTER,
                hasOverdrive ? ammoColor : UIManager.m_col_ui2, ui.m_alpha);

            int pipCount = Player.MAX_AMMO[state.AmmoCapacityLevel] / AMMO_PER_PIP;
            float pipHalfWidth = 4f - state.AmmoCapacityLevel * 0.5f;
            pos.x = (0f - BAR_HALF_WIDTH) * ((float)(pipCount - 1) / pipCount);
            for (int pip = 0; pip < pipCount; pip++)
            {
                int ammoInPip = state.ammo - pip * AMMO_PER_PIP;
                if (ammoInPip <= 0)
                {
                    UIManager.DrawQuadUI(pos, 1f, 4f, frame, ui.m_alpha * 0.5f, 13);
                }
                else if (ammoInPip < AMMO_PER_PIP)
                {
                    UIManager.DrawQuadUI(pos, 1f, 4f, frame, ui.m_alpha * 0.5f, 13);
                    UIManager.DrawQuadUI(pos, pipHalfWidth, 8f * ammoInPip / AMMO_PER_PIP,
                        ammoColor, ui.m_alpha, 4);
                }
                else
                {
                    UIManager.DrawQuadUI(pos, pipHalfWidth, 8f, ammoColor, ui.m_alpha, 4);
                }
                pos.x += BAR_HALF_WIDTH / (pipCount * 0.5f);
            }
        }

        private static void DrawPrimaryWeapon(UIElement ui, Vector2 pos, ShipState state,
            MpTeam team, bool teamMatch)
        {
            Color frame = UIManager.m_col_ub1;
            Color line = UIManager.m_col_ui2;
            Color selectedColor = UIManager.m_col_ui5;
            Color nameColor = Color.Lerp(UIManager.m_col_hi4, UIManager.m_col_hi5,
                UnityEngine.Random.value * UIElement.FLICKER);
            var weapon = (WeaponType)state.weapon;
            WeaponUnlock weaponLevel = state.WeaponLevel(state.weapon);

            line.a = ui.m_alpha;
            pos.y += WeaponPanelDrop;
            pos.x += (MenuManager.opt_hud_weapons != 0) ? -35f : 35f;
            if (teamMatch && team != MpTeam.ANARCHY)
                DrawTeamOutline(ui, pos, WEAPON_BAR_WIDTH, team, 12.5f, 33f);

            UIManager.DrawQuadBarHorizontal(new Vector2(pos.x, pos.y - 14f), 1.2f, 1.2f,
                WEAPON_PANEL_HALF_WIDTH * 2f + 5f, line, SPRITE_BRACKET);
            UIManager.DrawQuadBarHorizontal(pos, 12f, 12f, WEAPON_BAR_WIDTH, line, 8);

            Vector2 labelAt = new Vector2(pos.x - WEAPON_BAR_WIDTH * 0.5f, pos.y);
            UIManager.DrawSpriteUI(labelAt, 0.18f, 0.18f, nameColor, ui.m_alpha,
                SPRITE_WEAPON_ICONS + (int)weapon);
            labelAt.x += 12f;
            ui.DrawStringSmall(WeaponNameForLevel(weapon, weaponLevel), labelAt, 0.5f,
                StringOffset.LEFT, nameColor, 1f, 145f);

            if (weaponLevel > WeaponUnlock.LEVEL_1)
            {
                Color tagBox = selectedColor;
                tagBox.a = ui.m_alpha;
                labelAt.x = pos.x + (WEAPON_BAR_WIDTH * 0.5f - 4f);
                labelAt.y = pos.y;
                UIManager.DrawQuadBarHorizontal(labelAt, 9f, 9f, 8f, tagBox, 13);
                labelAt.x -= 1f;
                ui.DrawStringSmall(WeaponTagFor(weapon, weaponLevel), labelAt, 0.45f,
                    StringOffset.CENTER, UIManager.m_col_ub3, 1f);
            }

            DrawPanelCorners(ui, pos, frame);

            pos.y += 25f;
            DrawPanelCorners(ui, pos, frame);
            UIManager.DrawQuadBarHorizontal(new Vector2(pos.x, pos.y + 14f), 1.2f, 1.2f,
                WEAPON_PANEL_HALF_WIDTH * 2f + 5f, line, SPRITE_BRACKET);

            pos.x -= WEAPON_BAR_WIDTH * 0.5f;
            for (int slot = 0; slot < LOADOUT_SLOTS; slot++)
            {
                bool isSelected = slot == (int)weapon;
                bool owned = state.WeaponLevel(slot) != WeaponUnlock.LOCKED;
                UIManager.DrawQuadUI(pos, 10f, 10f, isSelected ? selectedColor : frame,
                    ui.m_alpha * (owned ? 1f : 0.4f), 8);
                if (owned)
                {
                    UIManager.DrawSpriteUI(pos, 0.16f, 0.16f, isSelected ? nameColor : line,
                        ui.m_alpha, SPRITE_WEAPON_ICONS + slot);
                }
                pos.x += 30f;
            }
        }

        private static void DrawSecondaryWeapon(UIElement ui, Vector2 pos, ShipState state,
            MpTeam team, bool teamMatch)
        {
            Color frame = UIManager.m_col_ub1;
            Color line = UIManager.m_col_ui2;
            Color selectedColor = UIManager.m_col_ui5;
            Color nameColor = Color.Lerp(UIManager.m_col_hi4, UIManager.m_col_hi5,
                UnityEngine.Random.value * UIElement.FLICKER);
            var missile = (MissileType)state.missile;
            WeaponUnlock missileLevel = state.MissileLevel(state.missile);

            line.a = ui.m_alpha;
            pos.y += WeaponPanelDrop;
            pos.x -= (MenuManager.opt_hud_weapons != 0) ? -35f : 35f;
            if (teamMatch && team != MpTeam.ANARCHY)
                DrawTeamOutline(ui, pos, WEAPON_BAR_WIDTH, team, 14.5f, 37f);

            UIManager.DrawQuadBarHorizontal(new Vector2(pos.x, pos.y - 14f), 1.2f, 1.2f,
                WEAPON_PANEL_HALF_WIDTH * 2f + 5f, line, SPRITE_BRACKET);
            UIManager.DrawQuadBarHorizontal(pos, 12f, 12f, WEAPON_BAR_WIDTH, line, 8);

            if ((int)missile < LOADOUT_SLOTS)
            {
                Vector2 labelAt = new Vector2(pos.x - WEAPON_BAR_WIDTH * 0.5f, pos.y);
                UIManager.DrawSpriteUI(labelAt, 0.18f, 0.18f, nameColor, ui.m_alpha,
                    SPRITE_MISSILE_ICONS + (int)missile);
                labelAt.x += 7f;
                ui.DrawStringSmall(MissileNameForLevel(missile, missileLevel), labelAt, 0.5f,
                    StringOffset.LEFT, nameColor, 1f, 145f);

                if (missileLevel > WeaponUnlock.LEVEL_1)
                {
                    Color tagBox = selectedColor;
                    tagBox.a = ui.m_alpha;
                    labelAt.x = pos.x + (WEAPON_BAR_WIDTH * 0.5f - 40f);
                    UIManager.DrawQuadBarHorizontal(labelAt, 9f, 9f, 8f, tagBox, 13);
                    labelAt.x -= 1f;
                    ui.DrawStringSmall(MissileTagFor(missile, missileLevel), labelAt, 0.45f,
                        StringOffset.CENTER, UIManager.m_col_ub3, 1f);
                }

                labelAt.x = pos.x + (WEAPON_BAR_WIDTH * 0.5f + 2f);
                labelAt.y = pos.y;
                ui.DrawDigitsThree(labelAt, state.MissileCount((int)missile), 0.5f,
                    StringOffset.RIGHT, line, ui.m_alpha);
            }

            DrawPanelCorners(ui, pos, frame);

            pos.y += 25f;
            DrawPanelCorners(ui, pos, frame);
            UIManager.DrawQuadBarHorizontal(new Vector2(pos.x, pos.y + 18f), 1.2f, 1.2f,
                WEAPON_PANEL_HALF_WIDTH * 2f + 5f, line, SPRITE_BRACKET);

            pos.x -= WEAPON_BAR_WIDTH * 0.5f;
            for (int slot = 0; slot < LOADOUT_SLOTS; slot++)
            {
                bool isSelected = slot == (int)missile;
                bool owned = state.MissileLevel(slot) != WeaponUnlock.LOCKED;
                int count = state.MissileCount(slot);
                UIManager.DrawQuadUI(pos, 10f, 10f, isSelected ? selectedColor : frame,
                    ui.m_alpha * (owned ? ((count <= 0) ? 0.5f : 1f) : 0.4f), 8);
                if (owned)
                {
                    UIManager.DrawSpriteUI(pos, 0.17f, 0.17f, isSelected ? nameColor : line,
                        ui.m_alpha * ((count <= 0) ? 0.15f : 1f), SPRITE_MISSILE_ICONS + slot);
                    pos.y += 13f;
                    UIManager.DrawQuadUI(pos, 11f, 1.5f, frame, 0.5f * ui.m_alpha, 11);
                    float carriedFraction = Mathf.Min(1f, (float)count /
                        MaxMissileAmmoFor((MissileType)slot, state.MissileLevel(slot)));
                    UIManager.DrawQuadUI(pos, 11f * carriedFraction, 1.5f, selectedColor, ui.m_alpha, 11);
                    pos.y -= 13f;
                }
                pos.x += 30f;
            }
        }

        public static void DrawReticleReadouts(UIElement ui, Vector2 pos, ShipState state)
        {
            if (!Reticle.StyleIsFull)
                return;

            bool weaponsOnLeft = MenuManager.opt_hud_weapons != 0;
            Color frame = UIManager.m_col_ub1;
            Color digits = UIManager.m_col_ui2;
            if (state.HasOverdrive)
            {
                frame = Color.Lerp(UIManager.m_col_overdrive0, UIManager.m_col_overdrive1,
                    UnityEngine.Random.value * UIElement.FLICKER);
                digits = Color.Lerp(UIManager.m_col_overdrive2, UIManager.m_col_overdrive3,
                    UnityEngine.Random.value * UIElement.FLICKER);
            }

            pos.x = weaponsOnLeft ? -200f : 200f;
            ui.DrawOutlineBackdrop(pos, 14f, 45f, frame, 1);
            bool usesAmmo = Player.WeaponUsesAmmo2((WeaponType)state.weapon);
            Vector2 at = new Vector2(pos.x - 25f, pos.y);
            UIManager.DrawSpriteUI(at, 0.15f, 0.15f, frame, ui.m_alpha,
                usesAmmo ? SPRITE_AMMO_ICON : SPRITE_ENERGY_ICON);
            at.x += 50f;
            ui.DrawDigitsThree(at, usesAmmo ? state.ammo : (int)(state.energy + 0.5f), 0.75f,
                StringOffset.RIGHT, digits, ui.m_alpha);

            if (state.missile >= LOADOUT_SLOTS)
                return;
            frame = UIManager.m_col_ub1;
            digits = UIManager.m_col_ui2;
            pos.x = weaponsOnLeft ? 200f : -200f;
            ui.DrawOutlineBackdrop(pos, 14f, 45f, frame, 1);
            at = new Vector2(pos.x - 25f, pos.y);
            UIManager.DrawSpriteUI(at, 0.24f, 0.24f, frame, ui.m_alpha,
                SPRITE_MISSILE_ICONS + state.missile);
            at.x += 50f;
            ui.DrawDigitsThree(at, state.MissileCount(state.missile), 0.75f, StringOffset.RIGHT,
                digits, ui.m_alpha);
        }

        private static string WeaponNameForLevel(WeaponType weapon, WeaponUnlock level)
        {
            if (weapon == WeaponType.NUM)
                return "-";
            if (level == WeaponUnlock.LEVEL_1)
                return Player.WeaponNames[weapon] + "+";
            if (level > WeaponUnlock.LEVEL_1)
                return Player.WeaponNames[weapon] + "++";
            return Player.WeaponNames[weapon];
        }

        private static string MissileNameForLevel(MissileType missile, WeaponUnlock level)
        {
            if (missile == MissileType.NUM)
                return "-";
            if (level == WeaponUnlock.LEVEL_1)
                return Player.MissileNames[missile] + "+";
            if (level > WeaponUnlock.LEVEL_1)
                return Player.MissileNames[missile] + "++";
            return Player.MissileNames[missile];
        }

        private static string WeaponTagFor(WeaponType weapon, WeaponUnlock level)
        {
            if (level == WeaponUnlock.LEVEL_2A)
                return Player.WEAPON_2A_TAG[(int)weapon];
            if (level == WeaponUnlock.LEVEL_2B)
                return Player.WEAPON_2B_TAG[(int)weapon];
            return "-";
        }

        private static string MissileTagFor(MissileType missile, WeaponUnlock level)
        {
            if (level == WeaponUnlock.LEVEL_2A)
                return Player.MISSILE_2A_TAG[(int)missile];
            if (level == WeaponUnlock.LEVEL_2B)
                return Player.MISSILE_2B_TAG[(int)missile];
            return "-";
        }

        private static int MaxMissileAmmoFor(MissileType missile, WeaponUnlock level)
        {
            int slot = (int)missile;
            if (level < WeaponUnlock.LEVEL_1)
                return Player.MAX_MISSILE_AMMO[slot];
            bool upgraded;
            switch (missile)
            {
                case MissileType.FALCON:
                    upgraded = true;
                    break;
                case MissileType.MISSILE_POD:
                case MissileType.CREEPER:
                    upgraded = level == WeaponUnlock.LEVEL_2A;
                    break;
                case MissileType.HUNTER:
                    upgraded = level == WeaponUnlock.LEVEL_2B;
                    break;
                case MissileType.NOVA:
                case MissileType.DEVASTATOR:
                case MissileType.TIMEBOMB:
                case MissileType.VORTEX:
                    upgraded = level > WeaponUnlock.LEVEL_1;
                    break;
                default:
                    upgraded = false;
                    break;
            }
            return upgraded ? Player.MAX_MISSILE_AMMO_UP[slot] : Player.MAX_MISSILE_AMMO[slot];
        }

        private static void DrawTeamOutline(UIElement ui, Vector2 pos, float width, MpTeam team,
            float offsetY, float height)
        {
            pos.y += offsetY;

            Color outline = Hud.TeamColor((int)team, 1);
            outline.a = ui.m_alpha;
            UIManager.DrawFrameEmptyCenter(pos, 14f, 14f, width + 25f, height, outline, 7);
        }

        private static void DrawPanelCorners(UIElement ui, Vector2 pos, Color color)
        {
            UIManager.DrawSpriteUI(new Vector2(pos.x - WEAPON_PANEL_HALF_WIDTH, pos.y), 0.2f, 0.2f,
                color, ui.m_alpha, SPRITE_BRACKET);
            UIManager.DrawSpriteUI(new Vector2(pos.x + WEAPON_PANEL_HALF_WIDTH, pos.y), 0.2f, 0.2f,
                color, ui.m_alpha, SPRITE_BRACKET);
        }
    }
}
