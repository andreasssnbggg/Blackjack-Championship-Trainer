using System.Collections.Generic;

namespace BJCCheat
{
    public static class Lang
    {
        public static string Current = "en"; // "de" or "en"

        private static readonly Dictionary<string, Dictionary<string, string>> Strings = new Dictionary<string, Dictionary<string, string>>
        {
            // Bottom bar
            {"hotbar", new Dictionary<string, string> {
                {"de", "[BJC] F1 = Menü · F2 = Auto-Play · F3 = Auto-Bet · F4 = Speed"},
                {"en", "[BJC] F1 = Menu · F2 = Auto-Play · F3 = Auto-Bet · F4 = Speed"}
            }},
            // Window titles
            {"title_suite", new Dictionary<string, string> {
                {"de", "♠  BJC CHEAT SUITE  ♠"},
                {"en", "♠  BJC CHEAT SUITE  ♠"}
            }},
            {"title_strategy", new Dictionary<string, string> {
                {"de", "STRATEGIE"},
                {"en", "STRATEGY"}
            }},
            // Controls section
            {"controls", new Dictionary<string, string> {
                {"de", "── STEUERUNG ──"},
                {"en", "── CONTROLS ──"}
            }},
            {"autoplay", new Dictionary<string, string> {
                {"de", "  Auto-Play [F2]"},
                {"en", "  Auto-Play [F2]"}
            }},
            {"autobet", new Dictionary<string, string> {
                {"de", "  Auto-Bet [F3]  (setzt empfohlenen Einsatz)"},
                {"en", "  Auto-Bet [F3]  (places recommended bet)"}
            }},
            {"speedhack", new Dictionary<string, string> {
                {"de", "  Speed Hack 5x [F4]"},
                {"en", "  Speed Hack 5x [F4]"}
            }},
            {"language", new Dictionary<string, string> {
                {"de", "  Sprache / Language: "},
                {"en", "  Language / Sprache: "}
            }},
            // Card counter
            {"counter_title", new Dictionary<string, string> {
                {"de", "── KARTENZÄHLER ──"},
                {"en", "── CARD COUNTER ──"}
            }},
            {"good_for_you", new Dictionary<string, string> {
                {"de", "GUT FÜR DICH"},
                {"en", "GOOD FOR YOU"}
            }},
            {"neutral", new Dictionary<string, string> {
                {"de", "NEUTRAL"},
                {"en", "NEUTRAL"}
            }},
            {"bad_for_you", new Dictionary<string, string> {
                {"de", "SCHLECHT FÜR DICH"},
                {"en", "BAD FOR YOU"}
            }},
            // Edge text
            {"your_edge", new Dictionary<string, string> {
                {"de", "dein Vorteil +{0:0.0} %"},
                {"en", "your edge +{0:0.0} %"}
            }},
            {"house_edge", new Dictionary<string, string> {
                {"de", "Bank im Vorteil {0:0.0} %"},
                {"en", "house edge {0:0.0} %"}
            }},
            {"balanced", new Dictionary<string, string> {
                {"de", "ausgeglichen"},
                {"en", "balanced"}
            }},
            // Bet tip
            {"bet_tip", new Dictionary<string, string> {
                {"de", "Einsatz-Tipp: {0:N0}"},
                {"en", "Bet Tip: {0:N0}"}
            }},
            {"bet_max_level", new Dictionary<string, string> {
                {"de", "Tischmaximum · höchste Stufe"},
                {"en", "Table maximum · highest level"}
            }},
            {"bet_16x", new Dictionary<string, string> {
                {"de", "16× Minimum · Tischmaximum ab True Count +4"},
                {"en", "16× minimum · table max from True Count +4"}
            }},
            {"bet_8x", new Dictionary<string, string> {
                {"de", "8× Minimum · 16× ab True Count +3"},
                {"en", "8× minimum · 16× from True Count +3"}
            }},
            {"bet_1_5x", new Dictionary<string, string> {
                {"de", "1,5× Minimum · 8× ab True Count +2"},
                {"en", "1.5× minimum · 8× from True Count +2"}
            }},
            {"bet_min_level", new Dictionary<string, string> {
                {"de", "Tischminimum · 1,5× ab True Count +1,5"},
                {"en", "Table minimum · 1.5× from True Count +1.5"}
            }},
            {"bet_waiting", new Dictionary<string, string> {
                {"de", "Einsatz-Tipp: wartet auf Tischdaten …"},
                {"en", "Bet tip: waiting for table data …"}
            }},
            // Shoe progress
            {"shoe_progress", new Dictionary<string, string> {
                {"de", "Schuh {0:P0} gespielt · noch {1:0.0} Decks"},
                {"en", "Shoe {0:P0} dealt · {1:0.0} decks remaining"}
            }},
            {"joined_mid_shoe", new Dictionary<string, string> {
                {"de", "Mitten im Schuh eingestiegen – die Zählung ist erst nach dem nächsten Mischen verlässlich."},
                {"en", "Joined mid-shoe – the count is only reliable after the next shuffle."}
            }},
            {"running_count", new Dictionary<string, string> {
                {"de", "Running Count {0:+0;-0;0}  ·  {1} Karten gezählt"},
                {"en", "Running Count {0:+0;-0;0}  ·  {1} cards counted"}
            }},
            // Buttons
            {"how_it_works", new Dictionary<string, string> {
                {"de", "Wie funktioniert das?"},
                {"en", "How does it work?"}
            }},
            {"close_explanation", new Dictionary<string, string> {
                {"de", "Erklärung schließen"},
                {"en", "Close explanation"}
            }},
            {"reset_count", new Dictionary<string, string> {
                {"de", "Zählung zurücksetzen"},
                {"en", "Reset count"}
            }},
            // Help lines
            {"help1", new Dictionary<string, string> {
                {"de", "• Jede Karte zählt: 2–6 = +1, 7–9 = 0, 10/B/D/K/A = −1. Die Summe ist der Running Count."},
                {"en", "• Each card counts: 2–6 = +1, 7–9 = 0, 10/J/Q/K/A = −1. The sum is the Running Count."}
            }},
            {"help2", new Dictionary<string, string> {
                {"de", "• True Count = Running Count ÷ übrige Decks. Nur der True Count ist aussagekräftig."},
                {"en", "• True Count = Running Count ÷ decks remaining. Only the True Count matters."}
            }},
            {"help3", new Dictionary<string, string> {
                {"de", "• Hoch: viele Zehner und Asse übrig → mehr Blackjacks für dich → höher setzen."},
                {"en", "• High: many tens and aces left → more blackjacks for you → bet higher."}
            }},
            {"help4", new Dictionary<string, string> {
                {"de", "• Niedrig: viele kleine Karten übrig → Vorteil für die Bank → minimal setzen."},
                {"en", "• Low: many small cards left → house advantage → bet minimum."}
            }},
            {"help5", new Dictionary<string, string> {
                {"de", "• Versicherung nur ab True Count +{0:0}, sonst nie."},
                {"en", "• Insurance only from True Count +{0:0}, otherwise never."}
            }},
            {"help6", new Dictionary<string, string> {
                {"de", "• Einsatz-Tipp: True Count unter +2 → Minimum, +2 → 8×, +3 → 16× Minimum, ab +4 → Tischmaximum."},
                {"en", "• Bet tip: True Count below +1.5 → minimum, +1.5 → 1.5×, +2 → 8×, +3 → 16× minimum, +4+ → table max."}
            }},
            {"help7", new Dictionary<string, string> {
                {"de", "• Beim Mischen und beim Tischwechsel startet die Zählung automatisch neu."},
                {"en", "• The count resets automatically on shuffle and table change."}
            }},
            // Strategy advice window
            {"set_bet", new Dictionary<string, string> {
                {"de", "SETZE {0:N0}"},
                {"en", "BET {0:N0}"}
            }},
            {"set_minimum", new Dictionary<string, string> {
                {"de", "SETZE MINIMUM"},
                {"en", "BET MINIMUM"}
            }},
            {"table_max", new Dictionary<string, string> {
                {"de", "Tischmaximum"},
                {"en", "Table maximum"}
            }},
            // Insurance
            {"even_money", new Dictionary<string, string> {
                {"de", "EVEN MONEY"},
                {"en", "EVEN MONEY"}
            }},
            {"insurance", new Dictionary<string, string> {
                {"de", "VERSICHERUNG"},
                {"en", "INSURANCE"}
            }},
            {"yes", new Dictionary<string, string> {
                {"de", "JA"},
                {"en", "YES"}
            }},
            {"no", new Dictionary<string, string> {
                {"de", "NEIN"},
                {"en", "NO"}
            }},
            {"insurance_yes_hint", new Dictionary<string, string> {
                {"de", "True Count {0:+0.0;-0.0;0.0} – genug Zehner übrig, lohnt sich"},
                {"en", "True Count {0:+0.0;-0.0;0.0} – enough tens left, worth it"}
            }},
            {"insurance_no_hint", new Dictionary<string, string> {
                {"de", "True Count {0:+0.0;-0.0;0.0} – lohnt sich erst ab +{1:0}"},
                {"en", "True Count {0:+0.0;-0.0;0.0} – only worth it from +{1:0}"}
            }},
            // Play advice
            {"wait", new Dictionary<string, string> {
                {"de", "WARTEN"},
                {"en", "WAITING"}
            }},
            {"hint_hit", new Dictionary<string, string> {
                {"de", "Karte ziehen"},
                {"en", "Draw a card"}
            }},
            {"hint_stand", new Dictionary<string, string> {
                {"de", "Stehen bleiben"},
                {"en", "Stand"}
            }},
            {"hint_double", new Dictionary<string, string> {
                {"de", "Einsatz verdoppeln, genau 1 Karte"},
                {"en", "Double down, exactly 1 card"}
            }},
            {"hint_split", new Dictionary<string, string> {
                {"de", "Paar in zwei Hände teilen"},
                {"en", "Split pair into two hands"}
            }},
            {"hint_surrender", new Dictionary<string, string> {
                {"de", "Aufgeben, halber Einsatz zurück"},
                {"en", "Surrender, half bet back"}
            }},
            {"hint_wait", new Dictionary<string, string> {
                {"de", "Warte auf die Karten …"},
                {"en", "Waiting for cards …"}
            }},
            {"you_vs_dealer", new Dictionary<string, string> {
                {"de", "Du: {0}{1}  ·  Dealer: {2}"},
                {"en", "You: {0}{1}  ·  Dealer: {2}"}
            }},
        };

        public static string T(string key)
        {
            if (Strings.TryGetValue(key, out var langs))
                if (langs.TryGetValue(Current, out var text))
                    return text;
            return key; // fallback: return key itself
        }

        public static string T(string key, params object[] args)
        {
            string fmt = T(key);
            try { return string.Format(fmt, args); }
            catch { return fmt; }
        }

        public static void Toggle()
        {
            Current = Current == "de" ? "en" : "de";
        }
    }
}
