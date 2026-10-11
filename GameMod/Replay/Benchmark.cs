using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod.Replay
{
    public static class Benchmark
    {
        public static bool Armed;
        public static bool Running;
        public static bool ShowingResults;

        public static bool BlockReplayInput => Running || ShowingResults;

        public static bool HideHud => ShowingResults;

        private static string demoName = "";
        private static float demoDuration;

        private const float WARMUP_SECONDS = 2f;
        private static float warmupSecondsLeft;

        private const int MIN_SAMPLE_CAPACITY = 4096;
        private const int MAX_SAMPLE_CAPACITY = 1000000;

        private static readonly List<float> frameTimes = new List<float>(MIN_SAMPLE_CAPACITY);

        private static bool syncSettingsSaved;
        private static int savedVSyncCount;
        private static int savedTargetFps;

        private struct Row { public string label; public string value; public bool header; }
        private static readonly List<Row> performanceRows = new List<Row>();
        private static readonly List<Row> histogramRows = new List<Row>();
        private static readonly List<Row> hardwareRows = new List<Row>();
        private static readonly List<Row> settingsRows = new List<Row>();
        private static string averageFpsLabel = "-", low1PercentLabel = "-", low01PercentLabel = "-";
        private static string savedFilePath = "";

        private static readonly Color LOW1_COLOR = new Color(1f, 0.55f, 0.1f);
        private static readonly Color LOW01_COLOR = new Color(1f, 0.25f, 0.2f);

        private static float[] graphFps;
        private static float graphMaxFps;
        private static float graphAvgFps;
        private static float graph1PercentLowFps;
        private static float graph01PercentLowFps;
        private static float measuredSeconds;

        private static int targetPov;

        private static bool autoRun;
        private static bool autoQuit;
        private static string autoDemo = "";
        private static int autoPov;
        private static float autoStartDelaySeconds;

        private static void TryAutoStart()
        {
            if (GameplayManager.m_gameplay_state != GameplayState.MENUS ||
                MenuManager.m_menu_state != MenuState.MAIN_MENU)
                return;
            autoStartDelaySeconds -= Time.unscaledDeltaTime;
            if (autoStartDelaySeconds > 0f)
                return;
            autoRun = false;
            var demoPaths = OlrFile.ListPaths();
            string path = Commands.ResolveReplayPath(demoPaths, autoDemo);
            if (path == null)
            {
                Debug.LogErrorFormat("BENCH: auto-run demo not found: {0}", autoDemo);
                Application.Quit();
                return;
            }
            var info = OlrFile.ReadInfo(path);
            int playerCount = info.playerNames != null ? info.playerNames.Length : 0;
            if (playerCount == 0)
            {
                Debug.LogErrorFormat("BENCH: auto-run demo has no roster: {0}", autoDemo);
                Application.Quit();
                return;
            }
            targetPov = Mathf.Clamp(autoPov, 0, playerCount - 1);
            Debug.LogFormat("BENCH: auto-starting {0} following [{1}]", Path.GetFileName(path), targetPov);
            Start(path, targetPov);
        }

        private static float maxRunSeconds;

        public static void Start(string path, int povIndex, float maxSeconds = 0f)
        {
            maxRunSeconds = maxSeconds;
            demoName = Path.GetFileNameWithoutExtension(path);
            targetPov = povIndex;
            Armed = true;
            Running = false;
            ShowingResults = false;
            Playback.Start(path);
            if (Session.Mode != SessionMode.Playback)
                Armed = false;
        }

        private static void BeginRun()
        {
            Armed = false;
            Running = true;
            warmupSecondsLeft = WARMUP_SECONDS;
            demoDuration = Playback.Duration;

            frameTimes.Clear();

            int expectedSamples = (int)((demoDuration + 5f) * 500f); // 500 fps assumed peak
            int capacity = Mathf.Clamp(expectedSamples, MIN_SAMPLE_CAPACITY, MAX_SAMPLE_CAPACITY);
            if (frameTimes.Capacity < capacity)
                frameTimes.Capacity = capacity;

            savedVSyncCount = QualitySettings.vSyncCount;
            savedTargetFps = Application.targetFrameRate;
            syncSettingsSaved = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            Playback.Speed = 1f;
            Playback.Paused = false;
            Viewpoint.Watch(Mathf.Clamp(targetPov, 0,
                Mathf.Max(0, Playback.PlayerCount - 1)));

            ControlBar.Shown = false;
            ControlBar.DraggingSlider = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Debug.LogFormat("BENCH: run started ({0}, {1}s, warmup {2}s)", demoName, demoDuration.ToString("0.0"), WARMUP_SECONDS);
        }

        private static void RestoreSyncSettings()
        {
            if (!syncSettingsSaved)
                return;
            syncSettingsSaved = false;
            QualitySettings.vSyncCount = savedVSyncCount;
            Application.targetFrameRate = savedTargetFps;
        }

        public static void OnPlaybackReachedEnd()
        {
            if (!Running)
                return;
            Finish();
        }

        public static void OnPlaybackStopped()
        {
            if (Running)
                Abort("playback stopped");
            Armed = false;
            ShowingResults = false;
        }

        private static void Abort(string reason)
        {
            Running = false;
            RestoreSyncSettings();
            frameTimes.Clear();
            Debug.LogFormat("BENCH: aborted ({0})", reason);
            if (autoQuit)
                Application.Quit();
        }

        private static void Finish()
        {
            Running = false;
            RestoreSyncSettings();
            BuildReport();
            savedFilePath = WriteReportFile();
            if (autoQuit)
            {
                Debug.LogFormat("BENCH: auto-run complete{0} -- quitting", (savedFilePath != "" ? ", saved " + savedFilePath : ""));
                Application.Quit();
                return;
            }
            ShowingResults = true;

            Playback.BlankSceneForResults();
            Debug.LogFormat("BENCH: finished, {0} frames{1}", frameTimes.Count, (savedFilePath != "" ? ", saved " + savedFilePath : ""));
        }

        private static void Exit()
        {
            if (Running)
                Abort("cancelled by ESC");
            ShowingResults = false;
            try
            {
                GameplayManager.DoneLevel(GameplayManager.DoneReason.Quit);
            }
            catch (Exception e)
            {
                Debug.LogErrorFormat("BENCH: DoneLevel failed: {0}", e);
            }
            try
            {
                GameplayManager.SwitchToMenu(MenuState.MAIN_MENU);
            }
            catch (Exception e)
            {
                Debug.LogErrorFormat("BENCH: menu switch failed: {0}", e);
            }

            Time.timeScale = 1f;
        }

        private static void AddHeader(List<Row> rows, string label)
        {
            rows.Add(new Row { label = label, header = true });
        }

        private static void AddRow(List<Row> rows, string label, string value)
        {
            rows.Add(new Row { label = label, value = value });
        }

        private static float PercentileFrameTime(float[] sorted, float percentile)
        {
            int index = Mathf.Clamp(Mathf.CeilToInt(percentile * sorted.Length) - 1, 0, sorted.Length - 1);
            return sorted[index];
        }

        private static void BuildReport()
        {
            performanceRows.Clear();
            histogramRows.Clear();
            hardwareRows.Clear();
            settingsRows.Clear();
            averageFpsLabel = low1PercentLabel = low01PercentLabel = "-";
            graphFps = null;
            graphMaxFps = 0f;
            graphAvgFps = 0f;
            graph1PercentLowFps = 0f;
            graph01PercentLowFps = 0f;
            measuredSeconds = 0f;

            int sampleCount = frameTimes.Count;
            AddHeader(performanceRows, "PERFORMANCE");
            if (sampleCount == 0)
            {
                AddRow(performanceRows, "No samples", "demo shorter than the warmup?");
            }
            else
            {
                float totalSeconds = 0f;
                for (int i = 0; i < sampleCount; i++)
                    totalSeconds += frameTimes[i];
                float[] sorted = frameTimes.ToArray();
                Array.Sort(sorted);
                float p99 = PercentileFrameTime(sorted, 0.99f);
                float p999 = PercentileFrameTime(sorted, 0.999f);

                averageFpsLabel = (sampleCount / totalSeconds).ToString("0.0");
                low1PercentLabel = (1f / p99).ToString("0.0");
                low01PercentLabel = (1f / p999).ToString("0.0");
                graph1PercentLowFps = 1f / p99;
                graph01PercentLowFps = 1f / p999;
                measuredSeconds = totalSeconds;

                AddRow(performanceRows, "Average FPS", averageFpsLabel);
                AddRow(performanceRows, "1% low FPS", low1PercentLabel);
                AddRow(performanceRows, "0.1% low FPS", low01PercentLabel);
                AddRow(performanceRows, "Average frame time", (totalSeconds / sampleCount * 1000f).ToString("0.00") + " ms");
                AddRow(performanceRows, "Measured (excl. " + WARMUP_SECONDS.ToString("0") + "s warmup)",
                    totalSeconds.ToString("0.0") + " s, " + sampleCount + " frames");
                AddRow(performanceRows, "Demo length", Session.FormatTime(demoDuration));

                BuildHistogramRows(sorted);
                BuildGraph(totalSeconds);
            }

            AddHeader(hardwareRows, "HARDWARE");
            AddRow(hardwareRows, "GPU", SystemInfo.graphicsDeviceName);
            AddRow(hardwareRows, "CPU", SystemInfo.processorType);
            AddRow(hardwareRows, "Cores", SystemInfo.processorCount.ToString());
            AddRow(hardwareRows, "RAM", SystemInfo.systemMemorySize + " MB");
            AddRow(hardwareRows, "VRAM", SystemInfo.graphicsMemorySize + " MB");
            AddRow(hardwareRows, "OS", SystemInfo.operatingSystem);

            AddHeader(settingsRows, "SETTINGS");
            AddRow(settingsRows, "Resolution", Screen.width + "x" + Screen.height +
                (Screen.fullScreen ? " fullscreen" : " windowed"));

            AddRow(settingsRows, "VSync / Framelimit", MenuManager.GetVSyncSetting(savedVSyncCount) +
                " / " + (savedTargetFps <= 0 ? Loc.LS("OFF") : savedTargetFps.ToString()));
            AddRow(settingsRows, "Shadows", ShadowQualityName(MenuManager.gfx_shadow_quality));
            AddRow(settingsRows, "Reduced shadow lights",
                MenuManager.GetToggleSetting(MenuManager.gfx_reduced_shadows));
            AddRow(settingsRows, "Texture level", MenuManager.GetTextureLevel());
            AddRow(settingsRows, "FX lighting", MenuManager.GetFXLighting());

            AddRow(settingsRows, "Anti-aliasing", MenuManager.GetAntiAliasing());
            AddRow(settingsRows, "Screen-space reflections",
                MenuManager.GetToggleSetting(MenuManager.gfx_ssr));
            AddRow(settingsRows, "Reduced shaders",
                MenuManager.GetToggleSetting(MenuManager.gfx_shaders_simpler));
        }

        private static void BuildHistogramRows(float[] sorted)
        {
            AddHeader(histogramRows, "FRAME TIME HISTOGRAM");
            float[] bandEdges = { 1f / 240f, 1f / 120f, 1f / 60f, 1f / 30f, 1f / 15f };
            string[] bandLabels = { "> 240 fps", "120-240 fps", "60-120 fps",
                                    "30-60 fps", "15-30 fps", "< 15 fps" };
            int start = 0;
            for (int band = 0; band <= bandEdges.Length; band++)
            {
                bool lastBand = band == bandEdges.Length;
                int end = start;
                while (end < sorted.Length && (lastBand || sorted[end] < bandEdges[band]))
                    end++;
                int count = end - start;
                AddRow(histogramRows, bandLabels[band],
                    count + " (" + (100f * count / sorted.Length).ToString("0.0") + "%)");
                start = end;
            }
        }

        private static void BuildGraph(float totalSeconds)
        {
            int sampleCount = frameTimes.Count;
            int bucketCount = Mathf.Clamp(sampleCount, 1, 200);
            var sliceSeconds = new float[bucketCount];
            var sliceFrames = new int[bucketCount];
            float elapsed = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                int bucket = Mathf.Min(bucketCount - 1, (int)(elapsed / totalSeconds * bucketCount));
                sliceSeconds[bucket] += frameTimes[i];
                sliceFrames[bucket]++;
                elapsed += frameTimes[i];
            }
            graphFps = new float[bucketCount];
            float maxSliceFps = 0f;
            for (int bucket = 0; bucket < bucketCount; bucket++)
            {
                graphFps[bucket] = sliceSeconds[bucket] > 1e-6f
                    ? sliceFrames[bucket] / sliceSeconds[bucket] : 0f;
                if (graphFps[bucket] > maxSliceFps)
                    maxSliceFps = graphFps[bucket];
            }
            graphAvgFps = sampleCount / totalSeconds;

            graphMaxFps = Mathf.Max(graphAvgFps * 1.1f,
                                    Mathf.Min(maxSliceFps, graphAvgFps * 1.5f));
        }

        private static readonly UIElement nameLookupElement = new UIElement();

        private static string ShadowQualityName(int quality)
        {
            try
            {
                var shadowNameMethod = AccessTools.Method(typeof(UIElement), "GetShadowQualityName", new[] { typeof(int) });
                if (shadowNameMethod != null)
                    return (string)shadowNameMethod.Invoke(nameLookupElement, new object[] { quality });
            }
            catch (Exception e)
            {
                Debug.LogErrorFormat("BENCH: GetShadowQualityName lookup failed: {0}", e);
            }
            return quality.ToString();
        }

        private static string WriteReportFile()
        {
            try
            {
                OlrFile.EnsureDirectory();
                string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string path = Path.Combine(OlrFile.Directory,
                    "benchmark_" + demoName + "_" + stamp + ".txt");
                var report = new StringBuilder();
                report.AppendLine("olmod replay benchmark");
                report.AppendLine("Demo: " + demoName);
                report.AppendLine("Date: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                report.AppendLine();
                AppendRows(report, performanceRows);
                AppendRows(report, histogramRows);
                AppendRows(report, hardwareRows);
                report.AppendLine("Unity: " + Application.unityVersion);
                AppendRows(report, settingsRows);
                File.WriteAllText(path, report.ToString());
                return path;
            }
            catch (Exception e)
            {
                Debug.LogErrorFormat("BENCH: could not write report: {0}", e);
                return "";
            }
        }

        private static void AppendRows(StringBuilder report, List<Row> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.header)
                {
                    report.AppendLine();
                    report.AppendLine("[" + row.label + "]");
                }
                else
                {
                    report.AppendLine(row.label + ": " + row.value);
                }
            }
        }

        private static void DrawResults(UIElement ui)
        {
            Color bright = UIManager.m_col_ui3;
            Color dim = UIManager.m_col_ui0;
            Color accent = UIManager.m_col_hi5;

            Vector2 pos = new Vector2(576f, 576f);

            UIManager.DrawQuadUIInner(pos, 15f, 15f, Color.black, 1f, 11, 0.8f);

            pos.x = -532f; pos.y = -323f;
            ui.DrawStringSmall("RESULTS", pos, 0.42f, StringOffset.LEFT, accent, 1f);
            pos.y += 11f;
            DrawSolidLine(0f, pos.y, 535f, 0.5f, dim, 0.5f);

            pos.y += 22f;
            ui.DrawStringSmall("AVERAGE FPS", pos, 0.35f, StringOffset.LEFT, dim, 1f);
            pos.x += 100f;
            pos.y += 5f;
            ui.DrawStringSmall(averageFpsLabel, pos, 0.9f, StringOffset.LEFT, accent, 1f);
            pos.y -= 5f;
            pos.x += 152f;
            ui.DrawStringSmall("1% LOW FPS", pos, 0.35f, StringOffset.LEFT, dim, 1f);
            pos.x += 95f;
            ui.DrawStringSmall(low1PercentLabel, pos, 0.50f, StringOffset.LEFT, accent, 1f);
            pos.x += 115f;
            ui.DrawStringSmall("0.1% LOW FPS", pos, 0.35f, StringOffset.LEFT, dim, 1f);
            pos.x += 105f;
            ui.DrawStringSmall(low01PercentLabel, pos, 0.50f, StringOffset.LEFT, accent, 1f);

            DrawGraph(ui, -532f, 539f, 45f, 257f, bright, dim, accent);

            DrawInfoColumn(ui, hardwareRows, -532f, -232f, 473f, bright, dim, accent);
            DrawInfoColumn(ui, settingsRows, 0f, -232f, 539f, bright, dim, accent);

            pos.x = EXIT_BUTTON_X; pos.y = EXIT_BUTTON_Y;
            ui.DrawSmallItem("EXIT", pos, exitHover, false, EXIT_BUTTON_SCALE, 0.6f);
        }

        private static void DrawSolidLine(float centerX, float centerY, float halfW, float halfH,
                                          Color color, float alpha)
        {
            UIManager.DrawQuadUIInner(new Vector2(centerX, centerY), halfW, halfH, color, alpha, 11, 0.5f);
        }

        private const float EXIT_BUTTON_X = 0f;
        private const float EXIT_BUTTON_Y = 310;
        private const float EXIT_BUTTON_SCALE = 0.55f;
        private static bool exitHover;

        private static void DrawGraph(UIElement ui, float left, float right, float top, float bottom,
                                      Color bright, Color dim, Color accent)
        {
            float graphWidth = right - left, graphHeight = bottom - top;
            float centerX = (left + right) * 0.5f;
            Vector2 pos = new Vector2(centerX, (top + bottom) * 0.5f);

            DrawSolidLine(centerX, bottom, graphWidth * 0.5f, 0.7f, dim, 0.6f);
            DrawSolidLine(centerX, top, graphWidth * 0.5f, 0.5f, dim, 0.25f);

            if (graphFps == null || graphFps.Length == 0 || graphMaxFps <= 0f)
            {
                ui.DrawStringSmall("NO SAMPLES", pos, 0.4f, StringOffset.CENTER, dim, 1f);
                return;
            }

            int bucketCount = graphFps.Length;
            float slotWidth = graphWidth / bucketCount;
            float halfBarWidth = Mathf.Max(0.6f, slotWidth * 0.38f);
            for (int bucket = 0; bucket < bucketCount; bucket++)
            {
                float barHeight = Mathf.Clamp01(graphFps[bucket] / graphMaxFps) * graphHeight;
                if (barHeight < 0.5f)
                    continue;
                pos.x = left + (bucket + 0.5f) * slotWidth;
                pos.y = bottom - barHeight * 0.5f;
                UIManager.DrawQuadUIInner(pos, halfBarWidth, barHeight * 0.5f, bright, 1f, 11, 0.5f);
            }

            pos.x = right + 24f;
            float averageY = bottom - Mathf.Clamp01(graphAvgFps / graphMaxFps) * graphHeight;
            DrawSolidLine(centerX, averageY, graphWidth * 0.5f, 1f, accent, 0.55f);
            pos.y = averageY;
            ui.DrawStringSmall("AVG", pos, 0.25f, StringOffset.RIGHT, accent, 1f);
            if (graph1PercentLowFps > 0f)
            {
                float lowFpsY = bottom - Mathf.Clamp01(graph1PercentLowFps / graphMaxFps) * graphHeight;
                DrawSolidLine(centerX, lowFpsY, graphWidth * 0.5f, 0.5f, LOW1_COLOR, 0.55f);
                pos.y = lowFpsY;
                ui.DrawStringSmall("1%", pos, 0.25f, StringOffset.RIGHT, LOW1_COLOR, 1f);
            }
            if (graph01PercentLowFps > 0f)
            {
                float lowFpsY = bottom - Mathf.Clamp01(graph01PercentLowFps / graphMaxFps) * graphHeight;
                DrawSolidLine(centerX, lowFpsY, graphWidth * 0.5f, 0.5f, LOW01_COLOR, 0.55f);
                pos.y = lowFpsY;
                ui.DrawStringSmall("0.1%", pos, 0.25f, StringOffset.RIGHT, LOW01_COLOR, 1f);
            }

            pos.x = left - 10f; pos.y = top + 4f;
            ui.DrawStringSmall(graphMaxFps.ToString("0"), pos, 0.3f, StringOffset.RIGHT, dim, 1f);
            pos.y += graphHeight - 8f;
            ui.DrawStringSmall("0", pos, 0.3f, StringOffset.RIGHT, dim, 1f);
            pos.x += 10f; pos.y += 18f;
            ui.DrawStringSmall("0:00", pos, 0.3f, StringOffset.LEFT, dim, 1f);
            pos.x += graphWidth;
            ui.DrawStringSmall(Session.FormatTime(measuredSeconds), pos, 0.3f, StringOffset.RIGHT, dim, 1f);
        }

        private static void DrawInfoColumn(UIElement ui, List<Row> rows, float x, float y,
                                           float columnWidth, Color bright, Color dim, Color accent)
        {
            Vector2 pos = new Vector2(x, y);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.header)
                {
                    ui.DrawStringSmall(row.label, pos, 0.42f, StringOffset.LEFT, accent, 1f);
                    pos.y += 10f;
                    DrawSolidLine(x + columnWidth * 0.5f, pos.y, columnWidth * 0.5f, 0.5f, dim, 0.5f);
                    pos.y += 18f;
                }
                else
                {
                    ui.DrawStringSmall(row.label, pos, 0.35f, StringOffset.LEFT, dim, 1f);
                    pos.x += columnWidth;
                    ui.DrawStringSmall(row.value, pos, 0.35f, StringOffset.RIGHT, bright, 1f, columnWidth * 0.72f);
                    pos.x -= columnWidth;
                    pos.y += 25f;
                }
            }
        }

        [HarmonyPatch(typeof(GameManager), "Update")]
        class Benchmark_GameManager_Update
        {
            static void Postfix()
            {
                if (!Armed && !Running && !ShowingResults)
                {
                    if (autoRun && !GameplayManager.IsDedicatedServer())
                        TryAutoStart();
                    return;
                }
                if (GameplayManager.IsDedicatedServer())
                    return;

                if (ShowingResults)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    UIManager.FindMousePosition();
                    Vector2 mouse = UIManager.m_mouse_pos;
                    exitHover = Mathf.Abs(mouse.x - EXIT_BUTTON_X) <= 500f * EXIT_BUTTON_SCALE * 0.5f + 22f &&
                                Mathf.Abs(mouse.y - EXIT_BUTTON_Y) <= 24f;
                    if (exitHover && Input.GetMouseButtonDown(0))
                        Exit();
                    return;
                }

                if (Armed)
                {
                    if (Session.Mode != SessionMode.Playback)
                    {
                        Armed = false;
                        return;
                    }
                    if (Playback.Ready)
                        BeginRun();
                    return;
                }

                if (QualitySettings.vSyncCount != 0)
                    QualitySettings.vSyncCount = 0;
                if (Application.targetFrameRate != -1)
                    Application.targetFrameRate = -1;

                if (maxRunSeconds > 0f && Playback.Clock >= maxRunSeconds)
                {
                    Finish();
                    return;
                }

                float frameSeconds = Time.unscaledDeltaTime;
                if (warmupSecondsLeft > 0f)
                {
                    warmupSecondsLeft -= frameSeconds;
                    return;
                }
                if (frameSeconds > 1e-6f)
                    frameTimes.Add(frameSeconds);
            }
        }

        [HarmonyPatch(typeof(UIElement), "DrawHUD")]
        [HarmonyPriority(Priority.Last)]
        class Benchmark_UIElement_DrawHUD
        {
            static void Postfix(UIElement __instance)
            {
                if (!ShowingResults)
                    return;
                UIManager.ui_bg_dark = true;
                __instance.m_alpha = 1f;
                DrawResults(__instance);
            }
        }

        [HarmonyPatch(typeof(GameManager), "OpenPauseMenu")]
        class Benchmark_OpenPauseMenu
        {
            static bool Prefix()
            {
                if (!Running && !ShowingResults)
                    return true;
                Exit();
                return false;
            }
        }

        [HarmonyPatch(typeof(Console), "RegisterCommands")]
        class Benchmark_RegisterCommands
        {
            static void Postfix()
            {
                uConsole.RegisterCommand("replay_benchmark",
                    new uConsole.DebugCommand(CmdReplayBenchmark));

                if (!Core.GameMod.FindArgVal("-replay-benchmark", out autoDemo) || string.IsNullOrEmpty(autoDemo))
                    return;
                autoRun = true;
                autoQuit = true;
                autoStartDelaySeconds = 1.5f;
                if (Core.GameMod.FindArgVal("-replay-pov", out string povText))
                    int.TryParse(povText, out autoPov);
                Debug.LogFormat("BENCH: auto-run armed for '{0}' pov {1}", autoDemo, autoPov);
            }

            static void CmdReplayBenchmark()
            {
                var demoPaths = OlrFile.ListPaths();
                int paramCount = uConsole.GetNumParameters();
                if (paramCount == 0)
                {
                    Commands.LogReplayList(demoPaths, "Use: replay_benchmark <demo> <player#>");
                    return;
                }

                string demoArg = uConsole.GetString();
                string path = Commands.ResolveReplayPath(demoPaths, demoArg);
                if (path == null)
                {
                    uConsole.Log("Replay not found: " + demoArg);
                    return;
                }

                var info = OlrFile.ReadInfo(path);
                int playerCount = info.playerNames != null ? info.playerNames.Length : 0;

                if (paramCount < 2)
                {
                    uConsole.Log("Players in " + Path.GetFileName(path) + ":");
                    for (int i = 0; i < playerCount; i++)
                    {
                        uConsole.Log("  [" + i + "] " + Session.RosterName(info.playerNames[i], i));
                    }
                    if (playerCount == 0)
                        uConsole.Log("  (no roster found - is this a current-version demo?)");
                    uConsole.Log("Use: replay_benchmark " + demoArg + " <player#>");
                    return;
                }

                int povIndex;
                string povArgument = uConsole.GetString();
                if (!int.TryParse(povArgument, out povIndex) || povIndex < 0 || povIndex >= playerCount)
                {
                    uConsole.Log("Bad player index '" + povArgument + "' (0-" + (playerCount - 1) +
                                 " for this demo; run without it to list players).");
                    return;
                }

                if (Session.Mode != SessionMode.Off)
                {
                    uConsole.Log("Cannot start a benchmark while recording or playing a replay.");
                    return;
                }
                uConsole.Log("Benchmarking " + Path.GetFileName(path) + " following [" + povIndex + "] " +
                             Session.RosterName(info.playerNames[povIndex], povIndex));

                uConsole.TurnOff();
                Start(path, povIndex);
            }
        }
    }
}
