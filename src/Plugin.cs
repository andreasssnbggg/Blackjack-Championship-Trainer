using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace BJCCheat
{
    [BepInPlugin("com.bjc.cheat", "BJC Cheat Suite", "1.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Log;
        public static CardCounter Counter = new CardCounter();
        public static StrategyAdvisor Advisor = new StrategyAdvisor();

        public static bool ShowOverlay = true;
        public static bool AutoPlayEnabled = false;
        public static bool AutoBetEnabled = false;
        public static bool SpeedHack = false;

        public static string PlayState = "";
        public static int DealerUpCard = 0;
        public static int PlayerScore = 0;
        public static bool PlayerSoft = false;
        public static bool PlayerPair = false;
        public static bool CanSplit = false;
        public static bool CanDouble = false;
        public static bool CanSurrender = false;
        public static int NumDecks = 6;

        // Local seat stack and table limits, for the bet tip
        public static long Stack = 0;
        public static long BetMin = 0;
        public static long BetMax = 0;
        public static long BetStep = 0;

        void Awake()
        {
            Log = Logger;
            Log.LogInfo("BJC Cheat Suite Awake!");

            // Create our OWN persistent GameObject that survives scene loads
            var go = new GameObject("BJCCheat_Overlay");
            GameObject.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<CheatOverlay>();

            try
            {
                var harmony = new Harmony("com.bjc.cheat");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                Log.LogInfo("Harmony patches applied.");
            }
            catch (Exception ex)
            {
                Log.LogError($"Harmony error: {ex}");
            }

            // Repair MonoMod's corrupted assembly structs, otherwise the game crashes on exit
            Log.LogInfo($"MonoShutdownFix: repaired {MonoShutdownFix.Apply()} assemblies at startup");
            Application.quitting += () =>
                Log.LogInfo($"MonoShutdownFix: repaired {MonoShutdownFix.Apply()} assemblies on quit");
        }
    }

    /// <summary>
    /// Separate MonoBehaviour on its own DontDestroyOnLoad GameObject.
    /// This ensures Update() and OnGUI() always fire.
    /// </summary>
    public class CheatOverlay : MonoBehaviour
    {
        private const float WindowWidth = 340;
        private const int TitleHeight = 22;

        private static readonly Color Body = new Color(0.10f, 0.11f, 0.13f, 1f);
        private static readonly Color TitleBand = new Color(0.15f, 0.17f, 0.21f, 1f);
        private static readonly Color Gold = new Color(1f, 0.82f, 0.35f);
        private static readonly Color Emerald = new Color(0.15f, 0.95f, 0.55f);
        private static readonly Color Teal = new Color(0.2f, 0.75f, 1f);
        private static readonly Color[] FramePalette = { Emerald, Teal, Gold };

        private static readonly Color Accent = new Color(0.2f, 0.9f, 0.4f);
        private static readonly Color Muted = new Color(0.75f, 0.75f, 0.75f);
        private static readonly Color Bad = new Color(1f, 0.4f, 0.4f);
        private static readonly Color Blue = new Color(0.3f, 0.8f, 1f);
        private static readonly Color Orange = new Color(1f, 0.6f, 0.2f);

        private bool _showMenu = true;
        private bool _showCountHelp = false;
        // Placed on first OnGUI (needs the screen size); height follows the content
        private Rect _adviceRect;
        private Rect _menuRect;
        private bool _placed = false;

        // GUIStyles can only be built inside OnGUI, so they're created on first use
        private GUIStyle _window, _title, _center, _wrap, _header, _medium, _big;
        private Texture2D _windowBg, _frameH, _frameV;

        void Update()
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.F1))
                    _showMenu = !_showMenu;
                if (Input.GetKeyDown(KeyCode.F2))
                    Plugin.AutoPlayEnabled = !Plugin.AutoPlayEnabled;
                if (Input.GetKeyDown(KeyCode.F3))
                    Plugin.AutoBetEnabled = !Plugin.AutoBetEnabled;
                if (Input.GetKeyDown(KeyCode.F4))
                    SetSpeedHack(!Plugin.SpeedHack);
                if (Input.GetKeyDown(KeyCode.F5))
                    Lang.Toggle();

                // Auto-play: poll active table for pending turns every frame
                if (Plugin.AutoPlayEnabled)
                    ActiveTable.PollAutoPlay();

                // Auto-bet: poll for BET phase
                if (Plugin.AutoBetEnabled && Plugin.AutoPlayEnabled)
                    ActiveTable.PollAutoBet();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Update error: {ex}");
            }
        }

        void OnGUI()
        {
            try
            {
                if (_center == null || _windowBg == null || _frameH == null || _frameV == null) InitStyles();
                if (!_placed)
                {
                    // Right edge: the table has no HUD there, unlike the top left
                    float x = Screen.width - WindowWidth - 10;
                    _adviceRect = new Rect(x, 130, WindowWidth, 0);
                    _menuRect = new Rect(x, 300, WindowWidth, 0);
                    _placed = true;
                }

                GUI.depth = -1000;
                GUI.color = Color.green;
                GUI.Label(new Rect(10, Screen.height - 28, 900, 25), Lang.T("hotbar"));
                GUI.color = Color.white;

                // Titles are drawn inside the windows (DrawTitle), centred in the title band
                if (Plugin.ShowOverlay)
                    _adviceRect = GUILayout.Window(99902, _adviceRect, DrawAdvice, "", _window, GUILayout.Width(WindowWidth));

                if (_showMenu)
                    _menuRect = GUILayout.Window(99901, _menuRect, DrawMenu, "", _window, GUILayout.Width(WindowWidth));

                // Windows render on top of the rest of OnGUI, so the frames sit just outside them
                if (Event.current.type == EventType.Repaint)
                {
                    float phase = Mathf.Repeat(Time.unscaledTime * 0.2f, 1f);
                    if (Plugin.ShowOverlay) DrawGlowFrame(_adviceRect, phase);
                    if (_showMenu) DrawGlowFrame(_menuRect, phase + 0.5f);
                }
            }
            catch (Exception ex)
            {
                GUI.color = Color.red;
                GUI.Label(new Rect(10, 40, 600, 25), $"[BJC ERROR] {ex.Message}");
            }
        }

        void InitStyles()
        {
            // The default window skin is see-through; use a solid dark background with a title band.
            // The border keeps the band unstretched at the top, the body stretches below it.
            _windowBg = WindowTexture();
            _window = new GUIStyle(GUI.skin.window)
            {
                border = new RectOffset(0, 0, TitleHeight + 1, 1),
                padding = new RectOffset(10, 10, TitleHeight + 7, 10)
            };
            _window.normal.background = _windowBg;
            _window.onNormal.background = _windowBg;

            _frameH = FrameTexture(false);
            _frameV = FrameTexture(true);

            _center = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _wrap = new GUIStyle(GUI.skin.label) { wordWrap = true };
            _header = new GUIStyle(_center) { fontStyle = FontStyle.Bold };
            _title = new GUIStyle(_header) { fontSize = 13, wordWrap = false };
            _medium = new GUIStyle(_header) { fontSize = 18 };
            _big = new GUIStyle(_header) { fontSize = 22 };
        }

        // HideAndDontSave keeps the textures alive through scene loads and asset unloading

        static Texture2D WindowTexture()
        {
            // 1px wide: title band on top, accent line under it, body below (texture rows count from the bottom)
            const int height = 32;
            var tex = new Texture2D(1, height) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
            for (int y = 0; y < height; y++)
            {
                int fromTop = height - 1 - y;
                tex.SetPixel(0, y, fromTop < TitleHeight ? TitleBand : fromTop == TitleHeight ? Accent : Body);
            }
            tex.Apply();
            return tex;
        }

        static Texture2D FrameTexture(bool vertical)
        {
            const int n = 256;
            var tex = new Texture2D(vertical ? 1 : n, vertical ? n : 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            for (int i = 0; i < n; i++)
            {
                Color c = FrameColor(i / (float)n);
                if (vertical) tex.SetPixel(0, i, c);
                else tex.SetPixel(i, 0, c);
            }
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Colour at position u (0..1) around the frame: the palette blended in a loop,
        /// with two bright highlights at u = 0.25 and 0.75.
        /// </summary>
        static Color FrameColor(float u)
        {
            float x = u * FramePalette.Length;
            int i = (int)x % FramePalette.Length;
            Color c = Color.Lerp(FramePalette[i], FramePalette[(i + 1) % FramePalette.Length], Mathf.SmoothStep(0f, 1f, x - (int)x));

            float d = Mathf.Repeat(u * 2f, 1f) - 0.5f;
            float highlight = Mathf.Exp(-d * d / (2f * 0.06f * 0.06f));
            c = Color.Lerp(c * 0.6f, Color.Lerp(c, Color.white, 0.5f), highlight);
            c.a = 0.6f + 0.4f * highlight;
            return c;
        }

        static void SetSpeedHack(bool on)
        {
            Plugin.SpeedHack = on;
            Time.timeScale = on ? 5f : 1f;
        }

        // ── Menu: counter + controls ──

        void DrawMenu(int id)
        {
            DrawTitle(Lang.T("title_suite"));
            GUILayout.Space(5);
            DrawCounter();
            GUILayout.Space(8);

            Header(Lang.T("controls"));
            Plugin.AutoPlayEnabled = GUILayout.Toggle(Plugin.AutoPlayEnabled, Lang.T("autoplay"));
            Plugin.AutoBetEnabled = GUILayout.Toggle(Plugin.AutoBetEnabled, Lang.T("autobet"));
            bool speed = GUILayout.Toggle(Plugin.SpeedHack, Lang.T("speedhack"));
            if (speed != Plugin.SpeedHack)
                SetSpeedHack(speed);

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.Label(Lang.T("language"), GUILayout.ExpandWidth(false));
            if (GUILayout.Button(Lang.Current == "de" ? "Deutsch ▸ English" : "English ▸ Deutsch"))
                Lang.Toggle();
            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }

        void DrawCounter()
        {
            var counter = Plugin.Counter;
            float tc = counter.TrueCount(Plugin.NumDecks);
            float edge = counter.PlayerEdgePercent(tc);

            Header(Lang.T("counter_title"));

            // Verdict in plain words: is the rest of the shoe good for you?
            if (tc >= 2) Colored(Lang.T("good_for_you"), _big, Color.green);
            else if (tc > -1) Colored(Lang.T("neutral"), _big, Color.yellow);
            else Colored(Lang.T("bad_for_you"), _big, Bad);

            string edgeText = edge >= 0.05f ? Lang.T("your_edge", edge) :
                              edge <= -0.05f ? Lang.T("house_edge", -edge) : Lang.T("balanced");
            GUILayout.Label($"True Count {tc:+0.0;-0.0;0.0}  ·  {edgeText}", _center);

            // Bet tip: spread in table minimums
            GUILayout.Space(4);
            if (Plugin.BetMin > 0)
            {
                Colored(Lang.T("bet_tip", BetTip(tc)), _medium, tc >= 1.5f ? Color.green : Color.white);
                Colored(tc >= CardCounter.MaxBetTrueCount ? Lang.T("bet_max_level") :
                        tc >= 3 ? Lang.T("bet_16x") :
                        tc >= 2 ? Lang.T("bet_8x") :
                        tc >= 1.5f ? Lang.T("bet_1_5x") :
                        Lang.T("bet_min_level"), _center, Muted);
            }
            else
            {
                GUILayout.Label(Lang.T("bet_waiting"), _center);
            }

            // Shoe progress until the reshuffle
            float progress = counter.ShoeProgress;
            if (progress >= 0)
            {
                GUILayout.Space(4);
                DrawBar(progress, Blue);
                GUILayout.Label(Lang.T("shoe_progress", progress, counter.DecksRemaining(Plugin.NumDecks)), _center);
            }

            if (counter.JoinedMidShoe)
                Colored(Lang.T("joined_mid_shoe"), _wrap, Orange);

            Colored(Lang.T("running_count", counter.RunningCount, counter.CardsSeen), _center, Muted);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_showCountHelp ? Lang.T("close_explanation") : Lang.T("how_it_works")))
                _showCountHelp = !_showCountHelp;
            if (GUILayout.Button(Lang.T("reset_count")))
                counter.Reset();
            GUILayout.EndHorizontal();

            if (_showCountHelp)
            {
                GUILayout.Label(Lang.T("help1"), _wrap);
                GUILayout.Label(Lang.T("help2"), _wrap);
                GUILayout.Label(Lang.T("help3"), _wrap);
                GUILayout.Label(Lang.T("help4"), _wrap);
                GUILayout.Label(Lang.T("help5", StrategyAdvisor.InsuranceTrueCount), _wrap);
                GUILayout.Label(Lang.T("help6"), _wrap);
                GUILayout.Label(Lang.T("help7"), _wrap);
            }
        }

        // ── Advisor: what to do right now ──

        void DrawAdvice(int id)
        {
            DrawTitle(Lang.T("title_strategy"));
            float tc = Plugin.Counter.TrueCount(Plugin.NumDecks);

            if (Plugin.PlayState == "BET")
                DrawBetAdvice(tc);
            else if (Plugin.PlayState == "INSURANCE" && Plugin.DealerUpCard == 1)
                DrawInsuranceAdvice(tc);
            else
                DrawPlayAdvice();

            GUI.DragWindow();
        }

        void DrawBetAdvice(float tc)
        {
            long bet = BetTip(tc);
            string level = tc >= CardCounter.MaxBetTrueCount ? Lang.T("table_max") :
                           tc >= 1.5f ? $"{CardCounter.SpreadUnits(tc):0.#}× Min" : "Min";
            Colored(bet > 0 ? Lang.T("set_bet", bet) : Lang.T("set_minimum"), _big, tc >= 1.5f ? Color.green : Color.white);
            Colored($"{level} · True Count {tc:+0.0;-0.0;0.0}", _center, Muted);
        }

        void DrawInsuranceAdvice(float tc)
        {
            bool insure = Plugin.Advisor.ShouldInsure(tc);
            // With a blackjack the game offers even money instead; same rule
            string what = Plugin.PlayerScore == 21 ? Lang.T("even_money") : Lang.T("insurance");
            Colored($"{what}: {(insure ? Lang.T("yes") : Lang.T("no"))}", _big, insure ? Color.green : Bad);
            Colored(insure
                ? Lang.T("insurance_yes_hint", tc)
                : Lang.T("insurance_no_hint", tc, StrategyAdvisor.InsuranceTrueCount), _center, Muted);
        }

        void DrawPlayAdvice()
        {
            string advice = Plugin.Advisor.GetAdvice(
                Plugin.PlayerScore, Plugin.DealerUpCard, Plugin.PlayerSoft, Plugin.PlayerPair,
                Plugin.CanSplit, Plugin.CanDouble, Plugin.CanSurrender);

            Colored(advice == "WAIT" ? Lang.T("wait") : advice, _big, ActionColor(advice));
            Colored(ActionHint(advice), _center, Muted);

            if (Plugin.PlayerScore > 0)
                GUILayout.Label(Lang.T("you_vs_dealer", Plugin.PlayerScore, Plugin.PlayerSoft ? " soft" : "", RankName(Plugin.DealerUpCard)), _center);
        }

        static Color ActionColor(string advice)
        {
            switch (advice)
            {
                case "HIT": return Color.yellow;
                case "STAND": return Blue;
                case "DOUBLE": return Color.green;
                case "SPLIT": return Orange;
                case "SURRENDER": return Bad;
                default: return Color.white;
            }
        }

        static string ActionHint(string advice)
        {
            switch (advice)
            {
                case "HIT": return Lang.T("hint_hit");
                case "STAND": return Lang.T("hint_stand");
                case "DOUBLE": return Lang.T("hint_double");
                case "SPLIT": return Lang.T("hint_split");
                case "SURRENDER": return Lang.T("hint_surrender");
                default: return Lang.T("hint_wait");
            }
        }

        static string RankName(int rank)
        {
            if (rank == 0) return "?";
            if (rank == 1) return "A";
            return rank >= 10 ? "10" : rank.ToString();
        }

        static long BetTip(float trueCount)
        {
            return CardCounter.RecommendedBet(trueCount, Plugin.Stack, Plugin.BetMin, Plugin.BetMax, Plugin.BetStep);
        }

        // ── Drawing helpers ──

        void DrawTitle(string text)
        {
            GUI.color = Gold;
            GUI.Label(new Rect(0, 0, WindowWidth, TitleHeight), text, _title);
            GUI.color = Color.white;
        }

        /// <summary>
        /// Animated frame around a window: the gradient runs around the edge with two bright
        /// highlights, plus a soft, slowly pulsing glow fading outwards.
        /// </summary>
        void DrawGlowFrame(Rect r, float phase)
        {
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 2f);
            // Stacked frames overlap close to the window, so the glow is densest there
            for (int i = 3; i >= 1; i--)
                DrawFrame(r, 2f + i * 2.5f, 0.16f / i * pulse, phase);
            DrawFrame(r, 2f, pulse, phase);
        }

        /// <summary>
        /// A frame of the given thickness just outside r. The gradient goes once around the whole
        /// perimeter, clockwise from the top left, shifted by phase.
        /// </summary>
        void DrawFrame(Rect r, float thickness, float alpha, float phase)
        {
            float t = thickness;
            Rect o = new Rect(r.x - t, r.y - t, r.width + 2 * t, r.height + 2 * t);
            float w = o.width, h = o.height, p = 2 * (w + h);

            GUI.color = new Color(1f, 1f, 1f, alpha);
            // Top: left → right
            GUI.DrawTextureWithTexCoords(new Rect(o.x, o.y, w, t), _frameH, new Rect(phase, 0, w / p, 1));
            // Right: top → bottom (a GUI rect's top shows the texture's top, i.e. texCoords.yMax)
            GUI.DrawTextureWithTexCoords(new Rect(o.xMax - t, o.y + t, t, h - 2 * t), _frameV, new Rect(0, phase + (w + h) / p, 1, -h / p));
            // Bottom: right → left
            GUI.DrawTextureWithTexCoords(new Rect(o.x, o.yMax - t, w, t), _frameH, new Rect(phase + (2 * w + h) / p, 0, -w / p, 1));
            // Left: bottom → top
            GUI.DrawTextureWithTexCoords(new Rect(o.x, o.y + t, t, h - 2 * t), _frameV, new Rect(0, phase + (2 * w + h) / p, 1, h / p));
            GUI.color = Color.white;
        }

        void Header(string text)
        {
            Colored(text, _header, Accent);
        }

        static void Colored(string text, GUIStyle style, Color color)
        {
            GUI.color = color;
            GUILayout.Label(text, style);
            GUI.color = Color.white;
        }

        static void DrawBar(float value, Color color)
        {
            Rect r = GUILayoutUtility.GetRect(1, 8, GUILayout.ExpandWidth(true));
            GUI.color = new Color(1f, 1f, 1f, 0.15f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(value), r.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
