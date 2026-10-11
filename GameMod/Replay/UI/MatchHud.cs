using System.Collections.Generic;
using Overload;
using UnityEngine;

namespace GameMod.Replay
{
    public static class MatchHud
    {
        public static void Init(Recording replay)
        {
            Clear();
            teamScores = new int[(int)MPTeams.MPTEAM_NUM];
            for (int i = 0; i < replay.events.Count; i++)
            {
                var ev = replay.events[i];
                if (ev.type == MatchEventType.TeamScore)
                {
                    hasTeamScoreEvents = true;
                    NoteScoringTeam(ev.iA);
                    continue;
                }
                if (ev.type != MatchEventType.Kill)
                    continue;

                if (ev.iC == (int)MpTeam.ANARCHY || ev.iC < 0 || ev.iC >= teamScores.Length)
                    continue;

                teamKills.Add(new TeamKill
                {
                    t = ev.t,
                    team = ev.iC,
                    delta = ev.iA == ev.iB ? -1 : 1
                });
                NoteScoringTeam(ev.iC);
            }
            BuildObjectiveFeed(replay);

            weaponSelectedLabels = new string[(int)WeaponType.NUM];
            for (int i = 0; i < weaponSelectedLabels.Length; i++)
                weaponSelectedLabels[i] = string.Format(Loc.LS("{0} SELECTED"), Player.WeaponNames[i]);
            missileSelectedLabels = new string[(int)MissileType.NUM];
            for (int i = 0; i < missileSelectedLabels.Length; i++)
                missileSelectedLabels[i] = string.Format(Loc.LS("{0} SELECTED"), Player.MissileNames[i]);
        }

        public static void Clear()
        {
            teamKills.Clear();
            teamsThatScored.Clear();
            hasTeamScoreEvents = false;
            teamsPresent.Clear();
            ObjectiveLines.Clear();
            flagStatesFrame = -1;
            rankOrder.Clear();
            teamScores = null;
            weaponSelectedLabels = missileSelectedLabels = null;
        }

        public static bool TryMatchTimeRemaining(Recording replay, float clockSeconds,
            out float remainingSeconds)
        {
            remainingSeconds = 0f;
            if (replay == null || replay.matchTimeLimit <= 0f)
                return false;
            remainingSeconds = Mathf.Max(0f,
                replay.matchTimeLimit - (replay.matchStartOffset + clockSeconds));
            return true;
        }

        private const float LOW_ARMOR_HITPOINTS = 50f;
        private const float LOW_ARMOR_RING_RADIUS = 240f;
        private const float LOW_ARMOR_RING_THICKNESS = 1.5f;
        private const int SPRITE_LOW_ARMOR_ARC = 39;

        public static void DrawLowArmorWarning(UIElement ui, Vector2 pos, ShipState state,
            float maxHitpoints)
        {
            float hitpoints = state.Hitpoints(maxHitpoints);
            if (hitpoints >= LOW_ARMOR_HITPOINTS)
                return;

            float ramp = Mathf.Clamp01(0.15f + (LOW_ARMOR_HITPOINTS - hitpoints) / LOW_ARMOR_HITPOINTS);
            Color warning = Color.Lerp(UIManager.m_col_em5, UIManager.m_col_em7,
                ramp * (0.2f + ui.m_anim_state2 * 0.8f));
            warning.a = ui.m_alpha;
            int segments = (int)(ramp * 5.5f);
            UIManager.DrawRingArc(pos, Mathf.PI * 0.5f, LOW_ARMOR_RING_RADIUS,
                LOW_ARMOR_RING_THICKNESS, 1f, segments, warning, warning, SPRITE_LOW_ARMOR_ARC);
            UIManager.DrawRingArc(pos, Mathf.PI * 1.5f, LOW_ARMOR_RING_RADIUS,
                LOW_ARMOR_RING_THICKNESS, 1f, segments, warning, warning, SPRITE_LOW_ARMOR_ARC);
        }

        private const float PANEL_ANCHOR_FROM_TOP = 96f;
        private const float PANEL_ANCHOR_NUDGE_Y = 5f;
        private const float HEADER_ROW_HEIGHT = 24f;

        private const float MATCH_CLOCK_GRACE_SECONDS = 10f;
        private const float MATCH_CLOCK_WARNING_SECONDS = 10f;

        private const float TEAM_SCORE_HALF_WIDTH = 98f;
        private const float TEAM_NAME_GAP = 9f;
        private const float TEAM_ROW_STRIDE = 28f;
        private const float TEAM_BLOCK_PADDING = 6f;

        private const float ANARCHY_RULE_HALF_WIDTH = 100f;
        private const float ANARCHY_RULE_THICKNESS = 1.2f;
        private const int SPRITE_ANARCHY_RULE = 21;
        private const float ANARCHY_RULE_OFFSET_X = 6f;
        private const float ANARCHY_NAME_GAP = 35f;
        private const float ANARCHY_ROW_STRIDE = 16f;

        private const float FLAG_ROW_STRIDE = 26f;
        private const float FLAG_BLOCK_PADDING = 4f;
        private const float FLAG_ICON_SIZE = 0.3f;
        private const float FLAG_CARRIED_ICON_SIZE = 0.18f;
        private const float FLAG_CARRIED_RING_SIZE = 0.4f;
        private const float FLAG_STATUS_WIDTH = 150f;
        private const float FLAG_NAMEPLATE_ICON_SIZE = 0.16f;

        private struct TeamKill
        {
            public float t;
            public int team;
            public int delta;
        }

        private static readonly List<TeamKill> teamKills = new List<TeamKill>();

        private static bool hasTeamScoreEvents;

        private static readonly List<int> teamsThatScored = new List<int>();
        private static readonly List<int> teamsPresent = new List<int>();
        private static int[] teamScores;

        private static readonly List<int> rankOrder = new List<int>();
        private static readonly System.Comparison<int> byAnarchyPoints = (a, b) =>
        {
            int pointsA = AnarchyPoints(a), pointsB = AnarchyPoints(b);
            if (pointsA != pointsB) return pointsB.CompareTo(pointsA);
            return Hud.ScoreDeaths[a].CompareTo(Hud.ScoreDeaths[b]);
        };

        private static int AnarchyPoints(int rosterIndex)
        {
            return Hud.ScoreKills[rosterIndex] * 3 + Hud.ScoreAssists[rosterIndex]; // 3 per kill, 1 per assist
        }

        private static void NoteScoringTeam(int team)
        {
            if (team == (int)MpTeam.ANARCHY || team < 0 || team >= teamScores.Length)
                return;
            if (!teamsThatScored.Contains(team))
                teamsThatScored.Add(team);
        }

        private static void BuildTeamsPresent(Recording replay)
        {
            teamsPresent.Clear();
            for (int i = 0; i < replay.players.Count; i++)
            {
                int team = replay.players[i].team;
                if (team >= 0 && team < teamScores.Length && !teamsPresent.Contains(team))
                    teamsPresent.Add(team);
            }
            foreach (int team in teamsThatScored)
                if (!teamsPresent.Contains(team))
                    teamsPresent.Add(team);
        }

        public static void DrawMatchScore(UIElement ui, Recording replay)
        {
            if (teamScores == null)
                return;
            if (!Hud.TryTallyScores(replay))
                return;

            Vector2 pos = new Vector2(UIManager.UI_RIGHT - 113f,
                                      UIManager.UI_TOP + PANEL_ANCHOR_FROM_TOP - PANEL_ANCHOR_NUDGE_Y);
            ui.DrawStringSmall(Session.ModeName(replay.matchMode),
                new Vector2(pos.x - 100f, pos.y), 0.4f, StringOffset.LEFT,
                UIManager.m_col_ub0, 1f, 130f);

            float clock = Playback.Clock;
            float remainingSeconds;
            if (TryMatchTimeRemaining(replay, clock, out remainingSeconds))
            {
                float elapsed = replay.matchStartOffset + clock;
                Color clockColor = (elapsed <= MATCH_CLOCK_GRACE_SECONDS ||
                                    remainingSeconds >= MATCH_CLOCK_WARNING_SECONDS)
                    ? UIManager.m_col_ui2 : UIManager.m_col_em5;
                ui.DrawDigitsTime(new Vector2(pos.x + 95f, pos.y), (int)remainingSeconds,
                    0.45f, clockColor, ui.m_alpha);
            }

            pos.y += HEADER_ROW_HEIGHT;
            if (Hud.IsTeamMode(replay.matchMode))
                DrawTeamScores(ui, ref pos, replay);
            else
                DrawAnarchyScores(ui, ref pos, replay);
            if (replay.HasFlagState)
                DrawFlagRows(ui, ref pos, replay);
        }

        private static void DrawTeamScores(UIElement ui, ref Vector2 pos, Recording replay)
        {
            BuildTeamsPresent(replay);
            if (hasTeamScoreEvents)
            {
                replay.TeamScoresAt(Playback.Clock, teamScores);
            }
            else
            {
                float upToTime = Playback.Clock;
                for (int i = 0; i < teamScores.Length; i++)
                    teamScores[i] = 0;
                for (int i = 0; i < teamKills.Count && teamKills[i].t <= upToTime; i++)
                    teamScores[teamKills[i].team] += teamKills[i].delta;
            }
            teamsPresent.Sort(byTeamScore);

            int pov = Viewpoint.Watching;
            int watchedTeam = pov >= 0 && pov < replay.players.Count ? replay.players[pov].team : -1;
            foreach (int team in teamsPresent)
            {
                DrawTeamScoreSmall(ui, pos, team, teamScores[team], team == watchedTeam);
                pos.y += TEAM_ROW_STRIDE;
            }
            pos.y -= TEAM_ROW_STRIDE - TEAM_BLOCK_PADDING;
        }

        private static readonly System.Comparison<int> byTeamScore = (a, b) =>
        {
            if (teamScores[a] != teamScores[b]) return teamScores[b].CompareTo(teamScores[a]);
            return a.CompareTo(b);
        };

        private static void DrawTeamScoreSmall(UIElement ui, Vector2 pos, int team, int score,
            bool highlighted)
        {
            Color bar = Hud.TeamColor(team, highlighted ? 2 : 0);
            Color text = Hud.TeamColor(team, highlighted ? 4 : 2);
            bar.a = ui.m_alpha;
            if (highlighted)
                UIManager.DrawQuadBarHorizontal(pos, 15f, 15f, TEAM_SCORE_HALF_WIDTH * 2f, bar, 7);
            UIManager.DrawQuadBarHorizontal(pos, 12f, 12f, TEAM_SCORE_HALF_WIDTH * 2f, bar, 7);
            ui.DrawDigitsVariable(pos + Vector2.right * TEAM_SCORE_HALF_WIDTH, score, 0.55f,
                StringOffset.RIGHT, text, ui.m_alpha);
            ui.DrawStringSmall(MPTeams.TeamName((MpTeam)team),
                pos - Vector2.right * (TEAM_SCORE_HALF_WIDTH + TEAM_NAME_GAP), 0.5f, StringOffset.LEFT,
                text, 1f);
        }

        private static void DrawAnarchyScores(UIElement ui, ref Vector2 pos, Recording replay)
        {
            pos.y -= 12f;
            pos.x += ANARCHY_RULE_OFFSET_X;
            UIManager.DrawQuadUI(pos, ANARCHY_RULE_HALF_WIDTH, ANARCHY_RULE_THICKNESS,
                UIManager.m_col_ub1, ui.m_alpha, SPRITE_ANARCHY_RULE);
            pos.y += 10f;

            rankOrder.Clear();
            for (int i = 0; i < replay.players.Count; i++)
                rankOrder.Add(i);
            rankOrder.Sort(byAnarchyPoints);

            int pov = Viewpoint.Watching;
            Vector2 at = new Vector2(pos.x + 90f, pos.y);
            foreach (int rosterIndex in rankOrder)
            {
                at.y = pos.y;
                Color color = rosterIndex == pov ? UIManager.m_col_hi3 : UIManager.m_col_ui1;
                float rowAlpha = Ships.StateOf(rosterIndex).present ? 1f : 0.3f;
                ui.DrawDigitsVariable(at, AnarchyPoints(rosterIndex), 0.4f, StringOffset.RIGHT,
                    color, ui.m_alpha * rowAlpha);
                at.x -= ANARCHY_NAME_GAP;
                ui.DrawStringSmall(Playback.PlayerName(rosterIndex), at, 0.35f,
                    StringOffset.RIGHT, color, rowAlpha);
                at.x += ANARCHY_NAME_GAP;
                pos.y += ANARCHY_ROW_STRIDE;
            }

            pos.y -= 6f;
            UIManager.DrawQuadUI(pos, ANARCHY_RULE_HALF_WIDTH, ANARCHY_RULE_THICKNESS,
                UIManager.m_col_ub1, ui.m_alpha, SPRITE_ANARCHY_RULE);
            pos.x -= ANARCHY_RULE_OFFSET_X;
            pos.y -= 6f;
        }

        internal static float KillFeedTopY(Recording replay)
        {
            if (replay == null || teamScores == null)
                return UIManager.UI_TOP + 110f; // no score panel to clear

            float bottom = UIManager.UI_TOP + PANEL_ANCHOR_FROM_TOP - PANEL_ANCHOR_NUDGE_Y +
                           HEADER_ROW_HEIGHT;
            if (Hud.IsTeamMode(replay.matchMode))
            {
                BuildTeamsPresent(replay);
                bottom += TEAM_ROW_STRIDE * Mathf.Max(0, teamsPresent.Count - 1) + TEAM_BLOCK_PADDING;
            }
            else
            {
                bottom += ANARCHY_ROW_STRIDE * replay.players.Count - 14f;
            }
            if (replay.HasFlagState)
                bottom += FLAG_BLOCK_PADDING + FLAG_ROW_STRIDE * CTF.TeamCount;
            return bottom + 22f;
        }

        private const float WEAPON_SELECT_FLASH_SECONDS = 1.25f;
        private const float BANNER_BAR_HALF_HEIGHT = 16f;

        private static string[] weaponSelectedLabels, missileSelectedLabels;

        private static int FrameIndexAtOrBefore(Recording replay, float seconds)
        {
            int low = 0, high = replay.frames.Count - 1, found = -1;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                if (replay.frames[mid].t <= seconds) { found = mid; low = mid + 1; }
                else high = mid - 1;
            }
            return found;
        }

        private static bool CouldChangeWeapon(Frame frame, int pov)
        {
            return frame.ships != null && pov < frame.ships.Length &&
                   frame.ships[pov].present && frame.ships[pov].alive;
        }

        private static bool FindRecentSelection(Recording replay, int pov, float now,
            out string label, out float switchTime)
        {
            label = null;
            switchTime = 0f;
            for (int i = FrameIndexAtOrBefore(replay, now); i > 0; i--)
            {
                var frame = replay.frames[i];
                if (now - frame.t > WEAPON_SELECT_FLASH_SECONDS)
                    return false;
                var previous = replay.frames[i - 1];
                if (!CouldChangeWeapon(frame, pov) || !CouldChangeWeapon(previous, pov))
                    continue;
                var current = frame.ships[pov];
                var before = previous.ships[pov];
                if (current.weapon != before.weapon && current.weapon < weaponSelectedLabels.Length)
                {
                    label = weaponSelectedLabels[current.weapon];
                    switchTime = frame.t;
                    return true;
                }
                if (current.missile != before.missile && current.missile < missileSelectedLabels.Length)
                {
                    label = missileSelectedLabels[current.missile];
                    switchTime = frame.t;
                    return true;
                }
            }
            return false;
        }

        public static void DrawWeaponSelectFlash(UIElement ui, Vector2 reticleAt, Recording replay,
            int pov)
        {
            if (weaponSelectedLabels == null || replay.frames.Count < 2)
                return;
            string label;
            float switchTime;
            if (!FindRecentSelection(replay, pov, Playback.Clock, out label, out switchTime))
                return;

            float flashSecondsLeft = WEAPON_SELECT_FLASH_SECONDS - (Playback.Clock - switchTime);
            Vector2 at = new Vector2(0f, reticleAt.y + 100f);
            Color bar = Color.Lerp(UIManager.m_col_hi5, UIManager.m_col_hi6,
                Random.Range(0f, 0.5f * UIElement.FLICKER));
            bar.a = Mathf.Clamp01(1f - Mathf.Pow(1f - flashSecondsLeft, 7f));
            UIManager.DrawQuadBarHorizontal(at, BANNER_BAR_HALF_HEIGHT, BANNER_BAR_HALF_HEIGHT,
                340f, bar, 13);
            Color text = UIManager.m_col_black;
            text.a = Mathf.Clamp01(1f - Mathf.Pow(1f - flashSecondsLeft, 3f));
            ui.DrawStringSmallOverrideAlpha(label, at, 0.75f, StringOffset.CENTER, text, 360f);
        }

        internal struct ObjectiveFeedLine
        {
            public float t;
            public string message;
            public int actorIndex;
            public int team;
        }

        internal static readonly List<ObjectiveFeedLine> ObjectiveLines = new List<ObjectiveFeedLine>();

        internal static int NewestObjectiveLineAtOrBefore(float now)
        {
            int low = 0, high = ObjectiveLines.Count - 1, newest = -1;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                if (ObjectiveLines[mid].t <= now) { newest = mid; low = mid + 1; }
                else high = mid - 1;
            }
            return newest;
        }

        private static void AddObjectiveLine(float t, string message, int actorIndex, int team)
        {
            ObjectiveLines.Add(new ObjectiveFeedLine
            {
                t = t, message = message, actorIndex = actorIndex, team = team
            });
        }

        private static bool DiedAround(Recording replay, int rosterIndex, float t)
        {
            if (rosterIndex < 0)
                return false;
            for (int i = FirstEventAtOrAfter(replay, t - CARRIER_DEATH_WINDOW_SECONDS);
                 i < replay.events.Count; i++)
            {
                var ev = replay.events[i];
                if (ev.t > t + CARRIER_DEATH_WINDOW_SECONDS)
                    return false;
                if (ev.type == MatchEventType.Death && ev.iA == rosterIndex)
                    return true;
            }
            return false;
        }

        private static int FirstEventAtOrAfter(Recording replay, float seconds)
        {
            int low = 0, high = replay.events.Count - 1, found = replay.events.Count;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                if (replay.events[mid].t >= seconds) { found = mid; high = mid - 1; }
                else low = mid + 1;
            }
            return found;
        }

        private const float CARRIER_DEATH_WINDOW_SECONDS = 0.25f;

        private static void BuildObjectiveFeed(Recording replay)
        {
            var previousState = new FlagState[CTF.TeamCount];
            var previousCarrier = new int[CTF.TeamCount];
            var previousTime = new float[CTF.TeamCount];
            var previousReturnSecondsLeft = new float[CTF.TeamCount];
            var seenFlag = new bool[CTF.TeamCount];

            for (int i = 0; i < replay.events.Count; i++)
            {
                var ev = replay.events[i];
                if (ev.type == MatchEventType.MonsterballGoal)
                {
                    AddObjectiveLine(ev.t, GoalMessage(ev.iA, ev.iC == 1), ev.iB, ev.iA);
                    continue;
                }
                if (ev.type != MatchEventType.FlagState)
                    continue;
                int flag = ev.iA;
                if (flag < 0 || flag >= CTF.TeamCount)
                    continue;
                var state = (FlagState)ev.iB;

                if (seenFlag[flag])
                {
                    int actor = state == FlagState.PICKEDUP ? ev.iC : previousCarrier[flag];
                    string message = FlagTransitionMessage(replay, flag, previousState[flag],
                        state, actor, previousTime[flag], previousReturnSecondsLeft[flag], ev.t);
                    if (message != null)
                        AddObjectiveLine(ev.t, message, actor, (int)CarrierTeam(flag));
                }
                seenFlag[flag] = true;
                previousState[flag] = state;
                previousCarrier[flag] = ev.iC;
                previousTime[flag] = ev.t;
                previousReturnSecondsLeft[flag] = ev.fA;
            }
        }

        private static MpTeam CarrierTeam(int flag)
        {
            return MPTeams.AllTeams[CTF.TeamCount - 1 - flag];
        }

        private static MpTeam FlagOwnerTeam(int flag)
        {
            return MPTeams.AllTeams[flag];
        }

        private static string GoalMessage(int scoringTeam, bool ownGoal)
        {
            string teamName = scoringTeam >= 0 ? MPTeams.TeamName((MpTeam)scoringTeam) : "";
            return ownGoal ? "OWN GOAL -- " + teamName + " SCORED!" : teamName + " SCORED!";
        }

        private static string FlagTransitionMessage(Recording replay, int flag,
            FlagState from, FlagState to, int actor, float previousTime,
            float previousReturnSecondsLeft, float now)
        {
            string flagTeam = MPTeams.TeamName(FlagOwnerTeam(flag));
            string carrierTeam = MPTeams.TeamName(CarrierTeam(flag));
            string actorName = ActorName(replay, actor);
            if (to == FlagState.PICKEDUP)
                return from == FlagState.HOME
                    ? string.Format(Loc.LS("{0} ({1}) PICKS UP THE {2} FLAG!"),
                        actorName, carrierTeam, flagTeam)
                    : string.Format(Loc.LS("{0} ({1}) FINDS THE {2} FLAG AMONG SOME DEBRIS!"),
                        actorName, carrierTeam, flagTeam);
            if (to == FlagState.HOME && from == FlagState.PICKEDUP)
                return string.Format(Loc.LS("{0} ({1}) CAPTURES THE {2} FLAG!"),
                    actorName, carrierTeam, flagTeam);
            if (to == FlagState.HOME && from == FlagState.LOST)
            {
                bool timerExpired = previousReturnSecondsLeft > 0f &&
                                    previousReturnSecondsLeft - (now - previousTime) <= 0f;
                return timerExpired
                    ? string.Format(Loc.LS("LOST {0} FLAG RETURNED AFTER TIMER EXPIRED!"), flagTeam)
                    : "THE " + flagTeam + " FLAG HAS BEEN RETURNED!";
            }
            if (to == FlagState.LOST && from == FlagState.PICKEDUP)
            {
                if (DiedAround(replay, actor, now))
                    return null;
                return actor >= 0
                    ? string.Format(Loc.LS("{0} ({1}) DROPPED THE {2} FLAG!"),
                        actorName, carrierTeam, flagTeam)
                    : string.Format(Loc.LS("THE {0} FLAG HAS BEEN DROPPED!"), flagTeam);
            }
            return null;
        }

        private static string ActorName(Recording replay, int rosterIndex)
        {
            if (rosterIndex < 0 || rosterIndex >= replay.players.Count)
                return "";
            return Session.RosterName(replay.players[rosterIndex].name, rosterIndex);
        }

        private static readonly FlagSnapshot[] flagStates = new FlagSnapshot[CTF.TeamCount];
        private static int flagStatesFrame = -1;

        internal static void RefreshFlagStates(Recording replay)
        {
            if (flagStatesFrame == Time.frameCount || replay == null || !replay.HasFlagState)
                return;
            flagStatesFrame = Time.frameCount;
            float now = Playback.Clock;
            for (int flag = 0; flag < flagStates.Length; flag++)
                flagStates[flag] = replay.FlagStateAt(now, flag);
        }

        internal static int FlagCarriedBy(Recording replay, int rosterIndex)
        {
            if (rosterIndex < 0 || replay == null || !replay.HasFlagState)
                return -1;
            RefreshFlagStates(replay);
            for (int flag = 0; flag < flagStates.Length; flag++)
                if (flagStates[flag].known && flagStates[flag].state == FlagState.PICKEDUP &&
                    flagStates[flag].carrierIndex == rosterIndex)
                    return flag;
            return -1;
        }

        internal static void DrawNameplateFlagCue(Recording replay, Vector2 offset, int rosterIndex)
        {
            int flag = FlagCarriedBy(replay, rosterIndex);
            if (flag < 0)
                return;
            UIManager.DrawSpriteUI(offset + Vector2.right * 9f,
                FLAG_NAMEPLATE_ICON_SIZE, FLAG_NAMEPLATE_ICON_SIZE,
                Hud.TeamColor((int)FlagOwnerTeam(flag), 4), 1f,
                (int)AtlasIndex0.ICON_SECURITY_KEY1);
        }

        private static void DrawFlagRows(UIElement ui, ref Vector2 pos, Recording replay)
        {
            RefreshFlagStates(replay);
            pos.y += FLAG_BLOCK_PADDING;
            float clock = Playback.Clock;
            for (int flag = 0; flag < flagStates.Length; flag++)
            {
                var flagState = flagStates[flag];

                if (!flagState.known)
                {
                    pos.y += FLAG_ROW_STRIDE;
                    continue;
                }
                Color flagColor = Hud.TeamColor((int)FlagOwnerTeam(flag), 4);
                Vector2 iconAt = new Vector2(
                    pos.x - TEAM_SCORE_HALF_WIDTH - TEAM_NAME_GAP + 8f, pos.y);

                if (flagState.state == FlagState.PICKEDUP)
                {
                    UIManager.DrawSpriteUIRotated(iconAt, FLAG_CARRIED_RING_SIZE, FLAG_CARRIED_RING_SIZE,
                        Mathf.PI * 0.5f, Hud.TeamColor((int)CarrierTeam(flag), 4), ui.m_alpha,
                        (int)AtlasIndex0.RING_MED0);
                    UIManager.DrawSpriteUIRotated(iconAt, FLAG_CARRIED_ICON_SIZE, FLAG_CARRIED_ICON_SIZE,
                        Mathf.PI * 0.5f, flagColor * 0.5f, ui.m_alpha,
                        (int)AtlasIndex0.ICON_SECURITY_KEY1);
                }
                else
                {
                    UIManager.DrawSpriteUIRotated(iconAt, FLAG_ICON_SIZE, FLAG_ICON_SIZE,
                        Mathf.PI * 0.5f,
                        flagColor * (flagState.state == FlagState.LOST ? 0.2f : 1f),
                        ui.m_alpha, (int)AtlasIndex0.ICON_SECURITY_KEY1);
                }

                Vector2 statusAt = new Vector2(pos.x + TEAM_SCORE_HALF_WIDTH, pos.y);
                if (flagState.state == FlagState.PICKEDUP)
                {
                    ui.DrawStringSmall(Playback.PlayerName(flagState.carrierIndex), statusAt,
                        0.4f, StringOffset.RIGHT, Hud.TeamColor((int)CarrierTeam(flag), 4), 1f,
                        FLAG_STATUS_WIDTH);
                }
                else if (flagState.state == FlagState.LOST)
                {
                    float returnSeconds = flagState.ReturnSecondsLeftAt(clock);
                    bool counting = returnSeconds > 0f;
                    if (counting)
                        ui.DrawDigitsTimeNoHours(statusAt, returnSeconds, 0.4f, flagColor,
                            ui.m_alpha);
                    Vector2 labelAt = statusAt;
                    if (counting)
                        labelAt.x -= 46f; // clear the return-timer digits
                    ui.DrawStringSmall("DROPPED", labelAt, 0.4f, StringOffset.RIGHT,
                        flagColor, 1f, FLAG_STATUS_WIDTH);
                }
                else
                {
                    ui.DrawStringSmall("HOME", statusAt, 0.4f, StringOffset.RIGHT,
                        flagColor, 1f, FLAG_STATUS_WIDTH);
                }
                pos.y += FLAG_ROW_STRIDE;
            }
        }
    }
}
