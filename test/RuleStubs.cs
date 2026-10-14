using System;
using System.Collections.Generic;

// Stand-ins for the game/BepInEx types so TableRules.cs compiles outside Unity.
namespace Game.Common
{
    public class Card
    {
        public int Rank { get; private set; }
        public bool IsValid { get; private set; }
        public char Suit { get; private set; }

        public Card(int rank, bool valid = true, char suit = 's')
        {
            Rank = rank; IsValid = valid; Suit = suit;
        }

        // The real Card prints rank+suit ("Kc"); CountVisible uses that as its key
        public override string ToString() { return Rank.ToString() + Suit; }
    }
}

namespace BJCCheat
{
    public class StubLog
    {
        public readonly List<string> Lines = new List<string>();
        public void LogWarning(object m) { Lines.Add(m.ToString()); }
        public void LogInfo(object m) { Lines.Add(m.ToString()); }
        public void LogError(object m) { Lines.Add(m.ToString()); }
    }

    public static class Plugin
    {
        public static StubLog Log = new StubLog();
    }
}
