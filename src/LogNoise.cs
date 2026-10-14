using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;

namespace BJCCheat
{
    /// <summary>
    /// Keeps Unity's endlessly repeating engine warnings out of the console.
    ///
    /// Unity writes some warnings once per affected object per frame, and BepInEx
    /// forwards every Unity log line to its console, so a single misconfigured effect
    /// in the game buries everything else under thousands of identical lines.
    ///
    /// The filter sits on InternalLogEvent, the one point every log event passes
    /// through on its way to the listeners, so the console and the log file are
    /// covered by the same decision. Turning the whole Unity feed off in BepInEx.cfg
    /// (UnityLogListening) would do the same job in one line and cost far too much:
    /// game-side exceptions reach us only through that feed, and reading one of those
    /// is how the sit-down crash was found.
    ///
    /// Applied by hand rather than through PatchAll. It is the only patch that targets
    /// BepInEx instead of the game, so it is also the only one whose target could move
    /// under a different BepInEx build - and inside PatchAll that failure would take
    /// the game patches down with it. Here it can fail alone, and say so.
    /// </summary>
    public static class LogNoise
    {
        private static readonly HashSet<string> _announced = new HashSet<string>();
        private static readonly object _lock = new object();

        /// <summary>Guards against a log line emitted from inside the filter itself.</summary>
        [ThreadStatic] private static bool _inside;

        /// <summary>
        /// Hooks the filter in. Returns false and explains itself if BepInEx does not
        /// look the way we expect; the rest of the plugin is unaffected either way.
        /// </summary>
        public static bool Apply(Harmony harmony)
        {
            MethodInfo target = AccessTools.Method(typeof(Logger), "InternalLogEvent");
            if (target == null)
            {
                Plugin.Log.LogWarning("[NOISE] BepInEx.Logging.Logger.InternalLogEvent not found - " +
                                      "Unity log noise stays in the console");
                return false;
            }

            harmony.Patch(target, prefix: new HarmonyMethod(
                typeof(LogNoise).GetMethod(nameof(Prefix), BindingFlags.NonPublic | BindingFlags.Static)));
            return true;
        }

        static bool Prefix(LogEventArgs eventArgs)
        {
            if (_inside || eventArgs == null) return true;

            string pattern;
            try
            {
                string source = eventArgs.Source != null ? eventArgs.Source.SourceName : null;
                string text = eventArgs.Data as string;
                if (text == null && eventArgs.Data != null) text = eventArgs.Data.ToString();

                pattern = NoiseFilter.Match(source, text);
            }
            catch
            {
                return true; // never lose a line to a fault in the filter
            }

            if (pattern == null) return true;

            Announce(pattern);
            return false; // swallowed
        }

        /// <summary>
        /// Names each pattern once, the first time it is dropped. One line per session
        /// per pattern is cheap, and it means nothing ever disappears silently - the
        /// log says what is being hidden and that something is hiding it.
        /// </summary>
        static void Announce(string pattern)
        {
            lock (_lock)
            {
                if (!_announced.Add(pattern)) return;
            }

            _inside = true;
            try
            {
                if (Plugin.Log != null)
                    Plugin.Log.LogInfo($"[NOISE] hiding a repeating Unity message from here on: \"{pattern}\"");
            }
            catch { }
            finally { _inside = false; }
        }
    }
}
