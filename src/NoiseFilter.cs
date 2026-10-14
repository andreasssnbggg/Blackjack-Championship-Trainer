using System;

namespace BJCCheat
{
    /// <summary>
    /// Decides which log lines are worthless. Kept free of BepInEx and Unity types so
    /// it can be tested: a filter that matches one character too widely swallows real
    /// errors, and nothing about that failure is visible from the outside.
    /// </summary>
    public static class NoiseFilter
    {
        /// <summary>BepInEx names its forwarded Unity messages this.</summary>
        public const string UnitySource = "Unity Log";

        /// <summary>
        /// Unity engine messages that repeat forever and say nothing we can act on.
        /// Matched as substrings, because Unity appends varying context to some of them.
        /// </summary>
        public static readonly string[] Patterns =
        {
            // One of the game's particle effects uses a stop action on a sub-emitter.
            // Unity complains about it per system, per frame, for the whole session.
            "Sub-emitters may not use stop actions",
        };

        /// <summary>
        /// The pattern this line matches, or null to let it through. Only messages that
        /// came from Unity itself are candidates - a plugin is never silenced, however
        /// much its text looks like one of the patterns.
        /// </summary>
        public static string Match(string sourceName, string text)
        {
            if (sourceName != UnitySource) return null;
            if (string.IsNullOrEmpty(text)) return null;

            for (int i = 0; i < Patterns.Length; i++)
                if (text.IndexOf(Patterns[i], StringComparison.Ordinal) >= 0)
                    return Patterns[i];

            return null;
        }
    }
}
