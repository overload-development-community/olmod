using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod.Replay
{
    public static class Playback
    {
        public static bool Launching;
        internal static Recording Replay;
        private static bool setupDone;

        public static bool IsActive { get { return Session.Mode == SessionMode.Playback && Replay != null; } }
        public static bool Ready { get { return IsActive && setupDone; } }

        public static float Speed = 1f;
        public static bool Paused;
        private static readonly float[] SPEED_STEPS = { 0.1f, 0.25f, 0.5f, 1f, 2f, 4f };

        private static float playClock;
        public static float Clock { get { return playClock; } }
        public static float Duration { get { return Replay != null ? Replay.Duration : 0f; } }
        public static int PlayerCount { get { return Replay != null ? Replay.players.Count : 0; } }

        public const float SEEK_STEP_SECONDS = 5f;

        internal static int FrameBefore = -1, FrameAfter = -1;
        internal static float FrameBlend;
        private static int frameCursor;
        private static int eventCursor;

        private static bool seekPending;

        internal static bool JustSeeked;

        private static int[] baselineTeam;
        private static Color[] baselineColor;

        private static float smoothedFps;
        public static float RenderFps { get { return smoothedFps; } }
        public static float RecordedRateHz { get; private set; }

        public static string PlayerName(int rosterIndex)
        {
            if (Replay == null || rosterIndex < 0 || rosterIndex >= Replay.players.Count)
                return "";
            return Session.RosterName(Replay.players[rosterIndex].name, rosterIndex);
        }

        public static void Start(string path)
        {
            try
            {
                Replay = OlrFile.Load(path);
            }
            catch (Exception e)
            {
                GameplayManager.AddHUDMessage("Replay load failed");
                Debug.LogErrorFormat("REPLAY: load failed: {0}", e);
                return;
            }
            if (Replay.frames.Count == 0)
            {
                GameplayManager.AddHUDMessage("Replay is empty");
                Replay = null;
                return;
            }
            CorrectRemoteMissileCounts();

            Mission mission;
            int levelIndex = ResolveLevel(Replay, out mission);
            if (levelIndex < 0)
            {
                GameplayManager.AddHUDMessage("Replay level not found: " + Replay.sceneName);
                Debug.LogErrorFormat("REPLAY: level not found: '{0}' hash='{1}'", Replay.sceneName, Replay.levelIdHash);
                Replay = null;
                return;
            }

            Session.Mode = SessionMode.Playback;
            ResetTransport();

            RecordedRateHz = Replay.Duration > 0.01f ? Replay.frames.Count / Replay.Duration : 0f;

            Launching = true;
            try
            {
                Debug.LogFormat("REPLAY: loading level index {0} ({1}) with {2} players, {3} frames", levelIndex, Replay.sceneName, Replay.players.Count, Replay.frames.Count);

                GameplayManager.CreateNewGame(mission, levelIndex);

                GameplayManager.m_game_type = GameType.MISSION;

                GameplayManager.SwitchToMenu(MenuState.PLAY_GAME);
            }
            catch (Exception e)
            {
                Debug.LogErrorFormat("REPLAY: level launch failed: {0}", e);
                Session.Mode = SessionMode.Off;
                Replay = null;
            }
            Launching = false;
        }

        private static int ResolveLevel(Recording replay, out Mission mission)
        {
            mission = GameManager.MultiplayerMission;
            if (mission == null)
                return -1;

            if (!string.IsNullOrEmpty(replay.levelIdHash))
            {
                int addOnIndex = mission.FindAddOnLevelNumByIdStringHash(replay.levelIdHash);
                if (addOnIndex >= 0)
                    return addOnIndex;
            }
            int levelIndex = mission.FindLevelIndex(replay.sceneName);
            if (levelIndex < 0)
                mission = null;
            return levelIndex;
        }

        private static void Setup()
        {
            setupDone = true;
            MPObserver.Enable();

            if (GameManager.m_shake_manager != null)
                GameManager.m_shake_manager.KillAllShakes();

            Hud.Init(Replay);
            World.Begin(Replay);
            Shots.Begin();

            baselineTeam = new int[Replay.players.Count];
            baselineColor = new Color[Replay.players.Count];
            for (int i = 0; i < Replay.players.Count; i++)
            {
                baselineTeam[i] = Replay.players[i].team;
                baselineColor[i] = Replay.players[i].color;
            }

            var shipMesh = GameManager.m_player_ship != null
                ? GameManager.m_player_ship.c_trans_ship : null;
            if (shipMesh == null)
            {
                Debug.LogError("REPLAY: no ship mesh to clone");
                return;
            }
            Ships.Create(Replay, shipMesh);

            Viewpoint.HideShip();

            if (!Benchmark.Armed && !Benchmark.Running)
                GameplayManager.AddHUDMessage(
                    "Replay: N controls | K pause | G pov | TAB scores | M camera");

            ControlBar.Reset();

            if (Replay.players.Count > 0)
                Viewpoint.Watch(Viewpoint.FirstWatchablePlayer());
        }

        public static void Stop()
        {
            World.End();
            Ships.Destroy();
            Shots.End();
            Hud.Clear();
            Benchmark.OnPlaybackStopped();
            baselineTeam = null;
            baselineColor = null;
            Replay = null;
            ResetTransport();

            Time.timeScale = 1f;
            SetAudioPaused(silent: false);

            GameManager.UnlockCursor();
            if (Session.Mode == SessionMode.Playback)
                Session.Mode = SessionMode.Off;
        }

        private static void ResetTransport()
        {
            setupDone = false;
            playClock = 0f;
            Speed = 1f;
            Paused = false;
            frameCursor = 0;
            FrameBefore = FrameAfter = -1;
            FrameBlend = 0f;
            eventCursor = 0;
            seekPending = false;
            JustSeeked = false;
            smoothedFps = 0f;
            Viewpoint.Reset();
            ControlBar.Reset();
        }

        public static void BlankSceneForResults()
        {
            Paused = true;
            Ships.HideAll();
            Shots.ClearAll();
            World.HideAll();

            ParticleManager.DestroyAll();
            ExplosionManager.ResetQueuedExplosions();
            if (GameManager.m_shake_manager != null)
                GameManager.m_shake_manager.KillAllShakes();
            Time.timeScale = 0f;
        }

        private static void Tick()
        {
            float realDeltaSeconds = Time.unscaledDeltaTime;
            if (realDeltaSeconds > 1e-5f)
            {
                float instantFps = 1f / realDeltaSeconds;
                smoothedFps = smoothedFps <= 0f ? instantFps : Mathf.Lerp(smoothedFps, instantFps, 0.1f);
            }

            if (!Paused && !MenuPaused)
                playClock += Time.unscaledDeltaTime * Speed;
            float duration = Replay.Duration;
            if (playClock >= duration)
            {
                playClock = duration;
                Paused = true;

                Benchmark.OnPlaybackReachedEnd();
            }
            if (playClock < 0f)
                playClock = 0f;

            if (Benchmark.ShowingResults)
            {
                Time.timeScale = GameplayManager.m_gameplay_state == GameplayState.PLAYING ? 0f : 1f;
                Viewpoint.KeepShipInert();
                return;
            }

            if (GameplayManager.m_gameplay_state == GameplayState.PLAYING)
                Time.timeScale = Paused ? 0f : Speed;
            else if (MenuPaused)
                Time.timeScale = 0f;
            else
                Time.timeScale = 1f;

            bool silent = Paused || MenuPaused;
            bool engineJustUnpaused = lastGameplayState != GameplayState.PLAYING &&
                                      GameplayManager.m_gameplay_state == GameplayState.PLAYING;
            lastGameplayState = GameplayManager.m_gameplay_state;
            if (silent != audioPaused || (silent && engineJustUnpaused))
                SetAudioPaused(silent);

            if (seekPending)
            {
                ApplySeek();
                seekPending = false;
            }

            FindBracketingFrames(playClock);

            Ships.Drive();
            World.Drive();

            int eventCount = Replay.events.Count;

            bool lastAppliedIsInTheFuture = eventCursor > 0 && eventCursor <= eventCount &&
                                            Replay.events[eventCursor - 1].t > playClock;
            if (lastAppliedIsInTheFuture)
            {
                ApplySeek();
            }
            else
            {
                bool allowFx = !Paused;
                while (eventCursor < eventCount && Replay.events[eventCursor].t <= playClock)
                    ApplyEvent(Replay.events[eventCursor++], allowFx);
            }

            Shots.Drive();
            Viewpoint.Drive();
            RouteMouseInput();

            JustSeeked = false;
        }

        private static bool MenuPaused
        {
            get
            {
                return GameplayManager.m_gameplay_state == GameplayState.MENUS || GameplayManager.m_gameplay_state == GameplayState.AUTOMAP;
            }
        }

        private static bool audioPaused;
        private static GameplayState lastGameplayState = GameplayState.PLAYING;

        private static void SetAudioPaused(bool silent)
        {
            var audio = GameManager.m_audio;
            if (audio == null)
                return;
            audioPaused = silent;
            lastGameplayState = GameplayManager.m_gameplay_state;
            if (silent)
                audio.PauseAllSounds();
            else
                audio.UnPauseAllSounds();
        }

        private static void FindBracketingFrames(float time)
        {
            FrameBefore = FrameAfter = -1;
            FrameBlend = 0f;
            int frameCount = Replay.frames.Count;
            if (frameCount == 0)
                return;

            frameCursor = Mathf.Clamp(frameCursor, 0, frameCount - 1);
            while (frameCursor < frameCount - 1 && Replay.frames[frameCursor + 1].t <= time) frameCursor++;
            while (frameCursor > 0 && Replay.frames[frameCursor].t > time) frameCursor--;

            FrameBefore = frameCursor;
            FrameAfter = (frameCursor < frameCount - 1) ? frameCursor + 1 : frameCursor;
            float intervalSeconds = Replay.frames[FrameAfter].t - Replay.frames[FrameBefore].t;
            FrameBlend = intervalSeconds > 1e-6f
                ? Mathf.Clamp01((time - Replay.frames[FrameBefore].t) / intervalSeconds)
                : 0f;
        }

        private static void RouteMouseInput()
        {
            if (MenuPaused)
                return;
            if (Hud.InfoShown)
            {
                ControlBar.FreeCursor();
            }
            else if (ControlBar.Shown)
            {
                ControlBar.ReadMouseInput();
            }
            else if (Viewpoint.IsFree && !uConsole.IsOn() && !PlayerShip.m_typing_in_chat)
            {
                Viewpoint.FlyFree();
            }
            else if (!Viewpoint.IsFree)
            {
                ControlBar.FreeCursor();
            }
        }

        private static void ApplySeek()
        {
            frameCursor = FrameIndexAt(playClock);
            Shots.ClearAll();
            if (baselineTeam != null && Replay != null)
                for (int i = 0; i < Replay.players.Count && i < baselineTeam.Length; i++)
                {
                    Replay.players[i].team = baselineTeam[i];
                    Replay.players[i].color = baselineColor[i];
                }
            eventCursor = 0;

            while (eventCursor < Replay.events.Count && Replay.events[eventCursor].t <= playClock)
            {
                var ev = Replay.events[eventCursor++];
                if (ev.type == MatchEventType.TeamChange || ev.type == MatchEventType.Respawn)
                    ApplyEvent(ev, allowFx: false);
            }
            Ships.ResetDeathRollAt(playClock);
            World.RebuildAt(playClock);
            JustSeeked = true;
        }

        private static void ApplyEvent(MatchEvent ev, bool allowFx)
        {
            switch (ev.type)
            {
                case MatchEventType.Fire:
                    if (allowFx)
                        Shots.Inject(ev);
                    break;
                case MatchEventType.ProjDespawn:
                    Shots.Terminate(ev.iA, ev.iB != 0, ev.pos);
                    break;
                case MatchEventType.ItemSpawn:
                    World.SpawnItem(ev.iA);
                    break;
                case MatchEventType.ItemPickup:
                    World.DestroyItem(ev.iA);
                    if (allowFx)
                        World.PlayPickupFx(ev);
                    break;
                case MatchEventType.SuperAlert:
                    if (allowFx)
                    {
                        bool appearing = ev.iA == 0; // RpcShowWarningMessage type: 0 = appearing in 10 s
                        GameplayManager.AddHUDMessage(Loc.LS(appearing
                            ? "SUPER POWERUP APPEARING IN 10 SECONDS"
                            : "A SUPER POWERUP HAS APPEARED"), -1, true);
                        SFXCueManager.PlayRawSoundEffect2D(SoundEffect.sfx_warning_loop1, 1f,
                            appearing ? UnityEngine.Random.Range(0f, 0.02f)
                                      : UnityEngine.Random.Range(0.1f, 0.12f));
                    }
                    break;
                case MatchEventType.DamageImpact:
                    if (allowFx)
                        PlayDamageImpactFx(ev.iA, (DamageType)ev.iB, ev.fA, ev.pos);
                    break;
                case MatchEventType.MonsterballGoal:
                    if (allowFx)
                        World.PlayGoalFx(ev.pos, ev.rot);
                    break;
                case MatchEventType.Respawn:
                    Ships.ClearDeathRoll(ev.iA);
                    break;
                case MatchEventType.TeamChange:
                    if (Replay != null && ev.iA >= 0 && ev.iA < Replay.players.Count)
                    {
                        var rosterEntry = Replay.players[ev.iA];
                        rosterEntry.team = ev.iB;
                        rosterEntry.color = new Color(ev.pos.x, ev.pos.y, ev.pos.z, 1f);
                    }
                    break;
            }
        }

        private static void PlayDamageImpactFx(int victimIndex, DamageType damageType,
            float damage, Vector3 pos)
        {
            float loudness = Mathf.Min(1.2f, damage * 0.05f);
            SFXCueManager.PlayRawSoundEffectPos(SoundEffect.player_hit5, pos,
                loudness * 0.9f + 0.1f, -0.1f);
            SFXCueManager.PlayRawSoundEffectPos(SoundEffect.player_hit02, pos,
                loudness * 0.8f + 0.2f, -0.1f);
            SFXCueManager.PlayCuePos(
                damageType == DamageType.ENERGY ? SFXCue.impact_energy_bot : SFXCue.impact_projectile,
                pos, UnityEngine.Random.Range(0.3f, 0.5f) + Mathf.Min(0.5f, damage * 0.03f));
            ExplosionManager.CreateMinorExplosion(pos);
            if (victimIndex == Viewpoint.Watching && GameManager.m_shake_manager != null)
            {
                float shake = damage * 0.05f;
                GameManager.m_shake_manager.PlayCameraShake(CameraShakeType.PLAYER_DAMAGE,
                    0.25f + shake, 0.5f + shake);
            }
        }

        public static void TogglePause()
        {
            Paused = !Paused;
        }

        public static void StepSpeed(int steps)
        {
            int currentStepIndex = 3; // the 1x entry
            for (int i = 0; i < SPEED_STEPS.Length; i++)
                if (Mathf.Approximately(SPEED_STEPS[i], Speed)) { currentStepIndex = i; break; }
            Speed = SPEED_STEPS[Mathf.Clamp(currentStepIndex + steps, 0, SPEED_STEPS.Length - 1)];
            GameplayManager.AddHUDMessage("Replay speed " + Speed + "x");
        }

        public static void SeekBy(float seconds)
        {
            SeekTo(playClock + seconds);
        }

        public static void SeekTo(float seconds)
        {
            if (Replay == null)
                return;
            playClock = Mathf.Clamp(seconds, 0f, Replay.Duration);
            frameCursor = FrameIndexAt(playClock);
            seekPending = true;
        }

        internal static int FrameIndexAt(float seconds)
        {
            if (Replay == null || Replay.frames.Count == 0)
                return 0;
            int low = 0, high = Replay.frames.Count - 1, found = 0;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                if (Replay.frames[mid].t <= seconds) { found = mid; low = mid + 1; }
                else high = mid - 1;
            }
            return found;
        }

        private static void ReadHotkeys()
        {
            if (Input.GetKeyDown(KeyCode.K))
                TogglePause();

            if (Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.Equals))
                StepSpeed(1);
            if (Input.GetKeyDown(KeyCode.KeypadMinus) || Input.GetKeyDown(KeyCode.Minus))
                StepSpeed(-1);

            if (Input.GetKeyDown(KeyCode.Comma) || Input.GetKeyDown(KeyCode.LeftArrow))
                SeekBy(-SEEK_STEP_SECONDS);
            if (Input.GetKeyDown(KeyCode.Period) || Input.GetKeyDown(KeyCode.RightArrow))
                SeekBy(SEEK_STEP_SECONDS);

            if (Controls.JustPressed(CCInput.VIEW_MAP) || Input.GetKeyDown(KeyCode.Tab))
                Hud.ToggleInfo();

            if (Input.GetKeyDown(KeyCode.G))
                Viewpoint.CycleWatched(ShiftHeld ? -1 : 1);

            if (Input.GetKeyDown(KeyCode.M))
                Viewpoint.CycleMode();

            if (Input.GetKeyDown(KeyCode.H))
            {
                Hud.ShowHealth = !Hud.ShowHealth;
            }

            if (Input.GetKeyDown(KeyCode.N))
                ControlBar.SetShown(!ControlBar.Shown);
        }

        private static bool ShiftHeld
        {
            get { return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift); }
        }

        private const int MISSILE_SLOTS = 8;

        private static void CorrectRemoteMissileCounts()
        {
            int playerCount = Replay.players.Count;
            if (playerCount == 0 || Replay.frames.Count == 0)
                return;

            var corrected = new int[playerCount, MISSILE_SLOTS];
            var previousRecorded = new int[playerCount, MISSILE_SLOTS];
            var seeded = new bool[playerCount];
            int eventIndex = 0;

            foreach (var frame in Replay.frames)
            {
                while (eventIndex < Replay.events.Count && Replay.events[eventIndex].t <= frame.t)
                {
                    var ev = Replay.events[eventIndex++];
                    if (ev.type != MatchEventType.Fire || ev.iA < 0 || ev.iA >= playerCount)
                        continue;
                    int slot = MissileSlotForProjectile((ProjPrefab)ev.iB);
                    if (slot < 0 || !seeded[ev.iA])
                        continue;
                    corrected[ev.iA, slot] = Mathf.Max(0, corrected[ev.iA, slot] - 1);
                }

                for (int rosterIndex = 0; rosterIndex < frame.ships.Length; rosterIndex++)
                {
                    if (!frame.ships[rosterIndex].present)
                        continue;
                    for (int slot = 0; slot < MISSILE_SLOTS; slot++)
                    {
                        int recorded = frame.ships[rosterIndex].MissileCount(slot);
                        if (!seeded[rosterIndex] || recorded != previousRecorded[rosterIndex, slot])
                            corrected[rosterIndex, slot] = recorded;
                        previousRecorded[rosterIndex, slot] = recorded;
                        frame.ships[rosterIndex].SetMissileCount(slot, corrected[rosterIndex, slot]);
                    }
                    seeded[rosterIndex] = true;
                }
            }
        }

        private static int MissileSlotForProjectile(ProjPrefab projectile)
        {
            switch (projectile)
            {
                case ProjPrefab.missile_falcon: return (int)MissileType.FALCON;
                case ProjPrefab.missile_pod: return (int)MissileType.MISSILE_POD;
                case ProjPrefab.missile_hunter: return (int)MissileType.HUNTER;
                case ProjPrefab.missile_creeper: return (int)MissileType.CREEPER;
                case ProjPrefab.missile_smart: return (int)MissileType.NOVA;
                case ProjPrefab.missile_devastator: return (int)MissileType.DEVASTATOR;
                case ProjPrefab.missile_timebomb: return (int)MissileType.TIMEBOMB;
                case ProjPrefab.missile_vortex: return (int)MissileType.VORTEX;
                default: return -1;
            }
        }

        [HarmonyPatch(typeof(GameManager), "Update")]
        class Playback_GameManager_Update
        {
            private static int faultsReported;

            static void Postfix()
            {
                if (!IsActive || GameplayManager.IsDedicatedServer())
                    return;

                if (!setupDone)
                {
                    if (GameplayManager.m_gameplay_state == GameplayState.PLAYING &&
                        GameManager.m_player_ship != null &&
                        GameManager.m_player_ship.c_trans_ship != null)
                        RunGuarded(Setup);
                    return;
                }

                if (!uConsole.IsOn() && !PlayerShip.m_typing_in_chat &&
                    !Benchmark.BlockReplayInput && !MenuPaused)
                    RunGuarded(ReadHotkeys);
                RunGuarded(Tick);
            }

            private static void RunGuarded(Action step)
            {
                try
                {
                    step();
                }
                catch (Exception e)
                {
                    if (faultsReported++ < 3)
                        Debug.LogErrorFormat("REPLAY: playback fault in {0}: {1}", step.Method.Name, e);
                }
            }
        }
    }




    [HarmonyPatch]
    class Playback_PlayerShip_Update
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(PlayerShip), "MaybeFireWeapon");
            yield return AccessTools.Method(typeof(PlayerShip), "MaybeFireMissile");
            yield return AccessTools.Method(typeof(PlayerShip), "FireFlare");
            yield return AccessTools.Method(typeof(PlayerShip), "DrawEffectMesh");
            yield return AccessTools.Method(typeof(PlayerShip), "FixedUpdateProcessControlsInternal");

            yield return AccessTools.Method(typeof(PlayerShip), "MaybeCreateGuideBot");

            yield return AccessTools.Method(typeof(PlayerShip), "ProcessFlareAndHeadlightControls");
        }

        static bool Prefix() { return !Playback.IsActive; }
    }

    [HarmonyPatch(typeof(GameplayManager), "DoneLevel")]
    class Playback_DoneLevel
    {
        static void Prefix()
        {
            if (Session.Mode == SessionMode.Playback)
                Playback.Stop();
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawPauseMenu")]
    class Playback_DrawPauseMenu
    {
        private const int SELECTION_RESUME = 0;
        private const int SELECTION_QUIT = 10;
        private const int SELECTION_CANCEL = 100;

        static bool Prefix(UIElement __instance)
        {
            if (Session.Mode != SessionMode.Playback)
                return true;

            UIManager.ui_bg_dark = true;
            UIManager.DrawQuadUI(new Vector2(0f, -20f), 300f, 240f, Color.black, 0.7f, 11);

            Vector2 position = __instance.m_position;
            if (MenuManager.m_menu_micro_state == 0)
            {
                position.y = -80f;
                __instance.DrawStringSmall(Loc.LS("REPLAY PAUSED"), position - Vector2.up * 46f,
                    0.75f, StringOffset.CENTER, UIManager.m_col_ui5, 1f);
                __instance.SelectAndDrawItem(Loc.LS("RESUME"), position, SELECTION_RESUME, false);
                position.y += 62f;
                __instance.SelectAndDrawItem(Loc.LS("EXIT REPLAY"), position, SELECTION_QUIT, false);
            }
            else
            {
                position.y = -62f;
                __instance.DrawStringSmall(Loc.LS("ARE YOU SURE?"), position - Vector2.up * 40f,
                    0.5f, StringOffset.CENTER, UIManager.m_col_ui1, 1f);
                position.y += 62f;
                __instance.SelectAndDrawItem(Loc.LS("QUIT"), position, SELECTION_RESUME, false);
                position.y += 62f;
                __instance.SelectAndDrawItem(Loc.LS("CANCEL"), position, SELECTION_CANCEL, false);
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(GameplayManager), "CreateRestartGame")]
    class Playback_CreateRestartGame
    {
        static bool Prefix()
        {
            return Session.Mode != SessionMode.Playback;
        }
    }

    [HarmonyPatch(typeof(CameraShakeManager), "PlayCameraShake")]
    class Playback_PlayCameraShake
    {
        static bool Prefix()
        {
            return Session.Mode != SessionMode.Playback;
        }
    }

    [HarmonyPatch(typeof(GameplayManager), "OpenAutomap")]
    class Playback_OpenAutomap
    {
        static bool Prefix()
        {
            return Session.Mode != SessionMode.Playback;
        }
    }
}
