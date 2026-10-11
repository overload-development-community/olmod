using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod.Replay
{
    public static class Hud
    {
        public static bool ShowHealth = true;

        public static bool InfoShown;

        private static Recording Replay => Playback.Replay;

        public static bool Suppressed { get { return Benchmark.HideHud; } }

        internal const int TEXTURE_SOLID = 11;
        internal const float SOLID_INNER = 0.5f;

        public static void Init(Recording replay)
        {
            Clear();
            for (int i = 0; i < replay.events.Count; i++)
            {
                var ev = replay.events[i];
                if (ev.type != MatchEventType.Kill)
                    continue;
                int killer = ev.iA;
                int victim = ev.iB;

                if (killer < 0 && !string.IsNullOrEmpty(ev.nameA)) killer = replay.IndexForName(ev.nameA);
                if (victim < 0 && !string.IsNullOrEmpty(ev.nameB)) victim = replay.IndexForName(ev.nameB);
                kills.Add(new KillEntry
                {
                    t = ev.t,
                    killer = killer,
                    victim = victim,
                    killType = (int)ev.fA,
                    killerName = ev.nameA,
                    victimName = ev.nameB
                });
            }
            int rosterSize = Mathf.Max(1, replay.players.Count);
            ScoreKills = new int[rosterSize];
            ScoreDeaths = new int[rosterSize];
            ScoreAssists = new int[rosterSize];

            int maxHitpoints = Mathf.Clamp(Mathf.CeilToInt(replay.maxHitpoints), 1, 1000); // cap the label table against a corrupt header
            hitpointLabels = new string[maxHitpoints + 1];
            for (int i = 0; i <= maxHitpoints; i++)
                hitpointLabels[i] = i.ToString("n0");

            MatchHud.Init(replay);
        }

        public static void Clear()
        {
            kills.Clear();
            ScoreKills = ScoreDeaths = ScoreAssists = null;
            hitpointLabels = null;
            InfoShown = false;
            RestoreInfoBlackout();
            cachedFps = int.MinValue;
            MatchHud.Clear();
        }

        internal static bool IsTeamMode(int mode)
        {
            return NetworkMatch.IsTeamMode((MatchMode)mode);
        }

        internal static Color TeamColor(int team, int mod)
        {
            if (team < 0 || team == (int)MpTeam.ANARCHY || team >= (int)MPTeams.MPTEAM_NUM)
                return UIManager.m_col_ui2;
            return MPTeams.TeamColor((MpTeam)team, mod);
        }

        internal static Color NameColor(int rosterIndex)
        {
            var replay = Replay;
            if (replay == null || rosterIndex < 0 || rosterIndex >= replay.players.Count)
                return UIManager.m_col_ui2;

            if (rosterIndex == Viewpoint.Watching)
                return UIManager.m_col_hi3;

            if (!IsTeamMode(replay.matchMode))
                return UIManager.ChooseMpColor(MpTeam.ANARCHY);
            return TeamColor(replay.players[rosterIndex].team, 2);
        }

        internal static bool TryTallyScores(Recording replay)
        {
            if (replay == null || ScoreKills == null)
                return false;
            replay.TallyScores(Playback.Clock, ScoreKills, ScoreDeaths, ScoreAssists);
            return true;
        }

        [HarmonyPatch(typeof(UIManager), "Draw")]
        class Hud_UIManager_Draw
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
            {
                var drawNames = AccessTools.Method(typeof(UIManager), "DrawMultiplayerNames");
                var endDrawing = AccessTools.Method(typeof(UIManager), "EndDrawing");
                int scanPhase = 0;
                foreach (var code in codes)
                {
                    if (scanPhase == 0 && code.opcode == OpCodes.Call &&
                        code.operand == drawNames)
                        scanPhase = 1;
                    if (scanPhase == 1 && code.opcode == OpCodes.Call &&
                        code.operand == endDrawing)
                    {
                        scanPhase = 2;

                        yield return new CodeInstruction(OpCodes.Call,
                            AccessTools.Method(typeof(Hud), nameof(DrawNameplates)))
                        { labels = code.labels };
                        code.labels = null;
                    }
                    yield return code;
                }
            }
        }

        private static Color Opaque(Color color) { color.a = 1f; return color; }
        private static readonly Color PLATE_BAR_BACK = Opaque(HSBColor.ConvertToColor(0.4f, 0.1f, 0.1f));
        private static readonly Color PLATE_FILL_LO = HSBColor.ConvertToColor(0.4f, 0.85f, 0.1f);
        private static readonly Color PLATE_FILL_HI = HSBColor.ConvertToColor(0.4f, 0.8f, 0.15f);

        private static string[] hitpointLabels;

        private const float PLATE_BAR_HALF_WIDTH = 2.4f;
        private const float PLATE_BAR_HALF_HEIGHT = 0.55f;

        public static void DrawNameplates()
        {
            if (!Playback.Ready || Suppressed || InfoShown)
                return;
            var replay = Replay;
            var ship = GameManager.m_player_ship;
            if (replay == null || ship == null || ship.c_transform == null || hitpointLabels == null)
                return;

            Vector3 camPos = ship.c_transform_position;
            Quaternion camOrient = ship.c_transform.localRotation;

            for (int rosterIndex = 0; rosterIndex < replay.players.Count; rosterIndex++)
            {
                if (rosterIndex == Viewpoint.Watching && !Viewpoint.IsFree)
                    continue;
                var state = Ships.StateOf(rosterIndex);
                if (!state.present || !state.alive)
                    continue;

                float distance = Mathf.Max(0.1f, (state.pos - camPos).magnitude);

                int quadStart = UIManager.m_quad_index;
                Vector2 offset = new Vector2(0f, -80f / distance);

                Color nameColor = NameColor(rosterIndex);
                UIManager.DrawStringAlignCenter(Playback.PlayerName(rosterIndex),
                    offset + Vector2.up * -2f, 0.8f, nameColor);

                MatchHud.DrawNameplateFlagCue(replay, offset + Vector2.up * -2f, rosterIndex);

                if (ShowHealth)
                {
                    float hitpoints = state.Hitpoints(replay.maxHitpoints);
                    offset.y -= 4.4f;
                    int labelIndex = Mathf.Clamp(Mathf.RoundToInt(hitpoints), 0, hitpointLabels.Length - 1);
                    UIManager.DrawStringAlignCenter(hitpointLabels[labelIndex],
                        offset + Vector2.up * -2.6f, 0.7f, nameColor);

                    UIManager.DrawQuadBarHorizontal(offset, PLATE_BAR_HALF_WIDTH + 0.2f,
                        PLATE_BAR_HALF_HEIGHT + 0.2f, 0f, PLATE_BAR_BACK, 7);
                    Color fill = Color.Lerp(PLATE_FILL_LO, PLATE_FILL_HI, Random.value * UIElement.FLICKER);
                    float fillWidth = Mathf.Clamp01(hitpoints / replay.maxHitpoints) * PLATE_BAR_HALF_WIDTH;
                    offset.x = PLATE_BAR_HALF_WIDTH - fillWidth;
                    UIManager.DrawQuadUIInner(offset, fillWidth, PLATE_BAR_HALF_HEIGHT, fill, 1f, 11, 1f);
                }

                WorldText.PreviousQuadsTransformText(state.pos, camOrient, distance, quadStart);
            }
        }

        private const float KILL_FEED_WINDOW_SECONDS = 6f;

        private struct KillEntry { public float t; public int killer; public int victim; public int killType; public string killerName; public string victimName; }

        private static readonly List<KillEntry> kills = new List<KillEntry>();

        private const int SPRITE_WEAPON_ICONS = 26;
        private const int SPRITE_MISSILE_ICONS = 104;
        private const int SPRITE_ENV_KILL_ICON = 128;

        private const int KILL_TYPE_FIRST_MISSILE = 8;
        private const int KILL_TYPE_LAST_MISSILE = 15;
        private const int KILL_TYPE_ENVIRONMENT = 16;

        private static int NewestKillAtOrBefore(float now)
        {
            int low = 0, high = kills.Count - 1, newest = -1;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                if (kills[mid].t <= now) { newest = mid; low = mid + 1; }
                else high = mid - 1;
            }
            return newest;
        }

        private static void DrawKillFeed(UIElement ui)
        {
            if (kills.Count == 0 && MatchHud.ObjectiveLines.Count == 0)
                return;
            float now = Playback.Clock;
            Vector2 pos = new Vector2(UIManager.UI_RIGHT - 15f, MatchHud.KillFeedTopY(Replay));

            int killCursor = NewestKillAtOrBefore(now);
            int objectiveCursor = MatchHud.NewestObjectiveLineAtOrBefore(now);
            for (int row = 0; row < 5; row++)
            {
                bool haveKill = killCursor >= 0 && now - kills[killCursor].t <= KILL_FEED_WINDOW_SECONDS;
                bool haveObjective = objectiveCursor >= 0 &&
                    now - MatchHud.ObjectiveLines[objectiveCursor].t <= KILL_FEED_WINDOW_SECONDS;
                if (!haveKill && !haveObjective)
                    return;
                bool drawKill = haveKill && (!haveObjective ||
                    kills[killCursor].t >= MatchHud.ObjectiveLines[objectiveCursor].t);
                if (drawKill)
                    DrawKillRow(ui, ref pos, kills[killCursor--], now);
                else
                    DrawObjectiveRow(ui, ref pos,
                        MatchHud.ObjectiveLines[objectiveCursor--], now);
            }
        }

        private const float KILL_FEED_FLASH_HOLD_SECONDS = 1.8f;

        private static void DrawKillRow(UIElement ui, ref Vector2 pos, KillEntry ev, float now)
        {
            float secondsLeft = KILL_FEED_WINDOW_SECONDS - (now - ev.t);
            float flash = Mathf.Clamp01(secondsLeft - KILL_FEED_FLASH_HOLD_SECONDS);
            float alpha = Mathf.Min(1f, secondsLeft * 2f);
            string victim = DisplayName(ev.victim, ev.victimName);

            bool killerKnown = ev.killer >= 0 || !string.IsNullOrEmpty(ev.killerName);
            bool suicide = (ev.killer >= 0 && ev.killer == ev.victim) ||
                           (!string.IsNullOrEmpty(ev.killerName) && ev.killerName == ev.victimName);
            string killer = killerKnown && !suicide ? DisplayName(ev.killer, ev.killerName) : null;
            DrawFeedRow(ui, ref pos, victim, FeedColor(ev.victim, flash), killer,
                FeedColor(ev.killer, flash), ev.killType, flash, alpha);
        }

        private static void DrawObjectiveRow(UIElement ui, ref Vector2 pos,
            MatchHud.ObjectiveFeedLine line, float now)
        {
            float secondsLeft = KILL_FEED_WINDOW_SECONDS - (now - line.t);
            float flash = Mathf.Clamp01(secondsLeft - KILL_FEED_FLASH_HOLD_SECONDS);
            float alpha = Mathf.Min(1f, secondsLeft * 2f);

            string actor = line.actorIndex >= 0 ? Playback.PlayerName(line.actorIndex) : null;
            DrawFeedRow(ui, ref pos, line.message, FeedColorForTeam(line.team, flash), actor,
                FeedColor(line.actorIndex, flash), -1 /* no kill icon */, flash, alpha);
        }

        private static void DrawFeedRow(UIElement ui, ref Vector2 pos, string rightText,
            Color rightColor, string leftText, Color leftColor, int killType, float flash,
            float alpha)
        {
            Color iconColor = Color.Lerp(UIManager.m_col_ui0, UIManager.m_col_ui3, flash);
            ui.DrawStringSmall(rightText, pos, 0.4f, StringOffset.RIGHT, rightColor, alpha);
            Vector2 iconPos = new Vector2(
                pos.x - (UIManager.GetStringWidth(rightText, 8f) + 10f), pos.y);
            int sprite = KillIconSprite(killType);
            if (sprite >= 0)
                UIManager.DrawSpriteUI(iconPos, 0.15f, 0.15f, iconColor, alpha, sprite);
            else
                ui.DrawStringSmall(">", iconPos, 0.4f, StringOffset.CENTER, iconColor, 1f);
            if (!string.IsNullOrEmpty(leftText))
                ui.DrawStringSmall(leftText, iconPos + Vector2.right * -12f,
                    0.4f, StringOffset.RIGHT, leftColor, alpha);
            pos.y += 18f;
        }

        private static int KillIconSprite(int killType)
        {
            if (killType == KILL_TYPE_ENVIRONMENT)
                return SPRITE_ENV_KILL_ICON;
            if (killType >= KILL_TYPE_FIRST_MISSILE && killType <= KILL_TYPE_LAST_MISSILE)
                return SPRITE_MISSILE_ICONS + (killType - KILL_TYPE_FIRST_MISSILE);
            if (killType >= 0)
                return SPRITE_WEAPON_ICONS + killType;
            return -1;
        }

        private static Color FeedColor(int rosterIndex, float flash)
        {
            var replay = Replay;
            if (rosterIndex >= 0 && rosterIndex == Viewpoint.Watching)
                return Color.Lerp(UIManager.m_col_hi2, UIManager.m_col_hi7, flash);
            bool teamColoured = replay != null && rosterIndex >= 0 &&
                                rosterIndex < replay.players.Count && IsTeamMode(replay.matchMode);
            if (!teamColoured)
                return Color.Lerp(UIManager.m_col_ui0, UIManager.m_col_ui3, flash);
            return FeedColorForTeam(replay.players[rosterIndex].team, flash);
        }

        internal static Color FeedColorForTeam(int team, float flash)
        {
            if (team < 0 || team == (int)MpTeam.ANARCHY || team >= (int)MPTeams.MPTEAM_NUM)
                return Color.Lerp(UIManager.m_col_ui0, UIManager.m_col_ui3, flash);
            return Color.Lerp(TeamColor(team, 1), TeamColor(team, 4), flash);
        }

        private static string DisplayName(int rosterIndex, string nameFromEvent)
        {
            var replay = Replay;
            bool inRoster = replay != null && rosterIndex >= 0 && rosterIndex < replay.players.Count;
            string rosterName = inRoster ? replay.players[rosterIndex].name : null;
            return Session.RosterName(rosterName, rosterIndex, nameFromEvent);
        }

        private const float CHAT_WINDOW_SECONDS = 8f;

        private static int NewestChatAtOrBefore(List<ChatLine> chat, float now)
        {
            int low = 0, high = chat.Count - 1, newest = -1;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                if (chat[mid].t <= now) { newest = mid; low = mid + 1; }
                else high = mid - 1;
            }
            return newest;
        }

        private const float TOP_LEFT_FIRST_Y = 72f;
        private static float topLeftY = TOP_LEFT_FIRST_Y;

        public static Vector2 TopLeftCursor
        {
            get { return new Vector2(UIManager.UI_LEFT + 20f, UIManager.UI_TOP + topLeftY); }
        }

        public static void AdvanceTopLeft(float height)
        {
            topLeftY += height;
        }

        private static void DrawChatFeed(UIElement ui)
        {
            var replay = Replay;
            if (replay == null || replay.chat.Count == 0)
                return;
            float now = Playback.Clock;

            int newestRow = -1, oldestRow = -1;
            for (int i = NewestChatAtOrBefore(replay.chat, now); i >= 0; i--)
            {
                float age = now - replay.chat[i].t;
                if (age > CHAT_WINDOW_SECONDS) break;
                if (newestRow < 0) newestRow = i;
                oldestRow = i;
                if (newestRow - oldestRow >= 7) break; // show at most 8 rows
            }
            if (newestRow < 0)
                return;

            Vector2 pos = TopLeftCursor;
            AdvanceTopLeft((newestRow - oldestRow + 1) * 16f + 8f);
            for (int i = oldestRow; i <= newestRow; i++)
            {
                var chatLine = replay.chat[i];
                float remaining = CHAT_WINDOW_SECONDS - (now - chatLine.t);
                float alpha = remaining > 0.5f ? 1f : remaining * 2f;
                Color senderColor = ChatSenderColor(chatLine);
                string sender = string.IsNullOrEmpty(chatLine.senderName)
                    ? DisplayName(chatLine.sender, null) : chatLine.senderName;
                float senderWidth = UIManager.GetStringWidth(sender, 7f);
                ui.DrawStringSmall(sender + ":", pos, 0.35f, StringOffset.LEFT, senderColor, alpha);
                ui.DrawStringSmall(chatLine.message, new Vector2(pos.x + senderWidth + 6f, pos.y),
                    0.35f, StringOffset.LEFT, UIManager.m_col_ui1, alpha, 350f - senderWidth);
                pos.y += 16f;
            }
        }

        private static Color ChatSenderColor(ChatLine chatLine)
        {
            if (chatLine.sender >= 0 && chatLine.sender == Viewpoint.Watching)
                return UIManager.m_col_ui6;
            var replay = Replay;
            if (replay != null && IsTeamMode(replay.matchMode))

                return TeamColor(chatLine.team, 3);
            return UIManager.m_col_ui3;
        }

        private static string fpsLabel = "";
        private static int cachedFps = int.MinValue;

        public static void ToggleInfo()
        {
            SetInfo(!InfoShown);
        }

        private static void SetInfo(bool shown)
        {
            bool wasShown = InfoShown;
            InfoShown = shown;
            if (shown && !wasShown)
            {
                blackoutActive = true;
                EnforceInfoBlackout();
            }
            else if (!shown && wasShown)
            {
                RestoreInfoBlackout();

                if (!ControlBar.Shown && Viewpoint.IsFree)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        private static bool blackoutActive;
        private static Camera disabledFlareCamera;
        private static float savedOverlayOrthoSize;

        private static void EnforceInfoBlackout()
        {
            if (!blackoutActive)
                return;

            if (GameManager.m_viewer != null)
                GameManager.m_viewer.MaybeUpdateHighlight(0f);

            if (disabledFlareCamera == null && GameManager.m_player_ship != null
                && GameManager.m_player_ship.c_camera != null)
            {
                var flare = GameManager.m_player_ship.c_camera.GetComponentInChildren<Camera>();
                if (flare != null && flare != GameManager.m_player_ship.c_camera && flare.enabled)
                {
                    flare.enabled = false;
                    disabledFlareCamera = flare;
                }
            }

            if (UIManager.m_overlay_camera != null)
            {
                if (!UIManager.m_overlay_camera.enabled)
                {
                    savedOverlayOrthoSize = UIManager.m_overlay_camera.orthographicSize;
                    UIManager.m_overlay_camera.enabled = true;
                }
                UIManager.m_overlay_camera.orthographicSize = 384f;
            }
        }

        private static void RestoreInfoBlackout()
        {
            if (!blackoutActive)
                return;
            blackoutActive = false;

            if (disabledFlareCamera != null)
            {
                disabledFlareCamera.enabled = true;
                disabledFlareCamera = null;
            }
            if (UIManager.m_overlay_camera != null)
            {
                if (savedOverlayOrthoSize > 0f)
                    UIManager.m_overlay_camera.orthographicSize = savedOverlayOrthoSize;
                UIManager.m_overlay_camera.enabled = false;
            }
        }

        [HarmonyPatch(typeof(UIManager), "DrawOverlay")]
        internal class Hud_UIManager_DrawOverlay
        {
            static void Postfix()
            {
                if (!Playback.Ready || Suppressed || !InfoShown)
                    return;
                EnforceInfoBlackout();
                var ui = UIManager.m_overlay_element[0];
                if (ui == null)
                    return;
                ui.m_alpha = 1f;

                UIManager.DrawQuadUIInner(Vector2.zero, 2000f, 1200f, Color.black, 1f,
                    TEXTURE_SOLID, SOLID_INNER);
                DrawScoreboard(ui);
            }
        }

        [HarmonyPatch(typeof(GameManager), "OpenPauseMenu")]
        class Hud_OpenPauseMenu
        {
            static bool Prefix()
            {
                if (Session.Mode != SessionMode.Playback || !InfoShown)
                    return true;
                ToggleInfo();
                return false;
            }
        }

        internal static int[] ScoreKills, ScoreDeaths, ScoreAssists;
        private static readonly List<int> rowOrder = new List<int>();

        private static readonly List<int> teamOrder = new List<int>();
        private static readonly System.Comparison<int> byScore = (a, b) =>
        {
            if (ScoreKills[a] != ScoreKills[b]) return ScoreKills[b].CompareTo(ScoreKills[a]);
            if (ScoreAssists[a] != ScoreAssists[b]) return ScoreAssists[b].CompareTo(ScoreAssists[a]);
            return ScoreDeaths[a].CompareTo(ScoreDeaths[b]);
        };

        private const float COL_PLAYER = -330f, COL_KILLS = 100f, COL_ASSISTS = 190f, COL_DEATHS = 280f;

        private const float TEAM_BAND_LEFT = COL_PLAYER - 10f;
        private const float TEAM_BAND_RIGHT = COL_KILLS - 45f;

        private static void DrawScoreboard(UIElement ui)
        {
            var replay = Replay;
            if (!TryTallyScores(replay))
                return;

            int rosterSize = replay.players.Count;
            rowOrder.Clear();
            for (int i = 0; i < rosterSize; i++)
                rowOrder.Add(i);
            rowOrder.Sort(byScore);

            UIManager.DrawQuadUIInner(new Vector2(0f, -50f), 420f, 230f, Color.black,
                0.7f, TEXTURE_SOLID, SOLID_INNER);
            Vector2 pos = new Vector2(0f, -240f);
            ui.DrawStringSmall(Session.ModeName(replay.matchMode), pos, 0.75f, StringOffset.CENTER, UIManager.m_col_ui5, 1f);
            pos.y += 25f;
            ui.DrawStringSmall(replay.sceneName.ToUpperInvariant(), pos, 0.5f, StringOffset.CENTER, UIManager.m_col_ui1, 1f);
            pos.y += 35f;

            ui.DrawStringSmall(Loc.LS("PLAYER"), pos + Vector2.right * COL_PLAYER, 0.4f, StringOffset.LEFT, UIManager.m_col_ui0, 1f);
            ui.DrawStringSmall(Loc.LS("KILLS"), pos + Vector2.right * COL_KILLS, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f);
            ui.DrawStringSmall(Loc.LS("ASSISTS"), pos + Vector2.right * COL_ASSISTS, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f);
            ui.DrawStringSmall(Loc.LS("DEATHS"), pos + Vector2.right * COL_DEATHS, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f);
            pos.y += 12f;
            ui.DrawVariableSeparator(pos, 350f);
            pos.y += 15f;

            if (!IsTeamMode(replay.matchMode))
            {
                foreach (int rosterIndex in rowOrder)
                    DrawScoreRow(ui, ref pos, rosterIndex);
                return;
            }

            teamOrder.Clear();
            foreach (int rosterIndex in rowOrder)
            {
                int team = replay.players[rosterIndex].team;
                if (!teamOrder.Contains(team))
                    teamOrder.Add(team);
            }
            foreach (int team in teamOrder)
            {
                DrawTeamHeader(ui, ref pos, replay, team, rosterSize);
                foreach (int rosterIndex in rowOrder)
                    if (replay.players[rosterIndex].team == team)
                        DrawScoreRow(ui, ref pos, rosterIndex);
                pos.y += 8f;
            }
        }

        private static void DrawTeamHeader(UIElement ui, ref Vector2 pos, Recording replay,
                                           int team, int rosterSize)
        {
            int teamScore = 0;
            for (int rosterIndex = 0; rosterIndex < rosterSize; rosterIndex++)
                if (replay.players[rosterIndex].team == team)
                    teamScore += ScoreKills[rosterIndex];

            Color bandColor = TeamColor(team, 1);
            Color textColor = TeamColor(team, 5);
            UIManager.DrawQuadUIInner(
                pos + Vector2.right * (TEAM_BAND_LEFT + TEAM_BAND_RIGHT) * 0.5f,
                (TEAM_BAND_RIGHT - TEAM_BAND_LEFT) * 0.5f, 10f, bandColor,
                0.35f, TEXTURE_SOLID, SOLID_INNER);
            ui.DrawStringSmall(MPTeams.TeamName((MpTeam)team), pos + Vector2.right * COL_PLAYER,
                0.45f, StringOffset.LEFT, textColor, 1f);
            ui.DrawDigitsVariable(pos + Vector2.right * COL_KILLS, teamScore, 0.55f,
                StringOffset.CENTER, textColor, 1f);
            pos.y += 25f;
        }

        private static void DrawScoreRow(UIElement ui, ref Vector2 pos, int rosterIndex)
        {
            bool spectated = rosterIndex == Viewpoint.Watching;
            if (spectated)
                UIManager.DrawQuadUIInner(pos + Vector2.right * -25f, 320f, 11f,
                    UIManager.m_col_ui2, 0.15f, TEXTURE_SOLID, SOLID_INNER);
            Color color = spectated ? UIManager.m_col_hi5 : UIManager.m_col_ui1;
            ui.DrawStringSmall(Playback.PlayerName(rosterIndex), pos + Vector2.right * COL_PLAYER,
                0.5f, StringOffset.LEFT, color, 1f);
            ui.DrawDigitsVariable(pos + Vector2.right * COL_KILLS, ScoreKills[rosterIndex], 0.65f, StringOffset.CENTER, color, 1f);
            ui.DrawDigitsVariable(pos + Vector2.right * COL_ASSISTS, ScoreAssists[rosterIndex], 0.65f, StringOffset.CENTER, color, 1f);
            ui.DrawDigitsVariable(pos + Vector2.right * COL_DEATHS, ScoreDeaths[rosterIndex], 0.65f, StringOffset.CENTER, color, 1f);
            pos.y += 25f;
        }

        [HarmonyPatch(typeof(UIElement), "DrawHUD")]
        class Hud_UIElement_DrawHUD
        {
            static bool Prefix(UIElement __instance)
            {
                if (!Playback.Ready)
                    return true;
                if (Suppressed)
                    return false;
                var ship = GameManager.m_player_ship;
                if (ship == null || ship.m_dying ||
                    GameplayManager.m_gameplay_state == GameplayState.EXIT ||
                    !GameplayManager.ShowHud)
                    return false;

                float savedAlpha = __instance.m_alpha;
                __instance.m_alpha *= UIElement.HUD_ALPHA;
                int pov = Viewpoint.Watching;
                var replay = Replay;

                if (replay != null)
                    MatchHud.DrawMatchScore(__instance, replay);

                if (pov >= 0 && replay != null && pov < replay.players.Count &&
                    !InfoShown &&
                    Viewpoint.Mode != CameraMode.Free)
                {
                    var state = Ships.StateOf(pov);
                    if (state.present)
                    {
                        if (!ControlBar.Shown)
                            ShipHud.Draw(__instance, state, (MpTeam)replay.players[pov].team,
                                IsTeamMode(replay.matchMode), replay.maxHitpoints);
                        Vector2 reticleAt = new Vector2(0f, UIManager.UI_BOTTOM + 128f);

                        if (Viewpoint.Mode == CameraMode.FirstPerson)
                        {
                            Reticle.Draw(__instance, reticleAt, state, replay.maxHitpoints);
                            ShipHud.DrawReticleReadouts(__instance, reticleAt, state);
                        }
                        MatchHud.DrawLowArmorWarning(__instance, reticleAt, state,
                            replay.maxHitpoints);
                        MatchHud.DrawWeaponSelectFlash(__instance, reticleAt, replay, pov);
                    }
                }

                __instance.DrawMessages();
                __instance.m_alpha = savedAlpha;
                return false;
            }

            static void Postfix(UIElement __instance)
            {
                if (!Playback.Ready)
                    return;

                if (Suppressed)
                    return;

                __instance.m_alpha = 1f;

                if (InfoShown)
                {
                    EnforceInfoBlackout();
                    return;
                }

                topLeftY = TOP_LEFT_FIRST_Y;
                int fps = Mathf.RoundToInt(Playback.RenderFps);
                if (fps != cachedFps)
                {
                    cachedFps = fps;
                    fpsLabel = "REC " + Playback.RecordedRateHz.ToString("0") +
                               "hz  FPS " + fps;
                }
                __instance.DrawStringSmall(fpsLabel, TopLeftCursor, 0.35f, StringOffset.LEFT,
                    UIManager.m_col_ui1, 1f, -1f);
                AdvanceTopLeft(24f);
                DrawChatFeed(__instance);

                DrawKillFeed(__instance);

                ControlBar.Draw(__instance);
            }
        }
    }
}
