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
                {"de", "[BJC] F1 = Menü · F2 = Auto-Play · F3 = Auto-Bet · F4 = Speed · F5 = Aussetzen · F6 = Neuer Tisch · F7 = Sprache"},
                {"en", "[BJC] F1 = Menu · F2 = Auto-Play · F3 = Auto-Bet · F4 = Speed · F5 = Sit out · F6 = New table · F7 = Language"}
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
            {"bet_4x", new Dictionary<string, string> {
                {"de", "4× Minimum · 8× ab True Count +2,5"},
                {"en", "4× minimum · 8× from True Count +2.5"}
            }},
            {"bet_2x", new Dictionary<string, string> {
                {"de", "2× Minimum · 4× ab True Count +2"},
                {"en", "2× minimum · 4× from True Count +2"}
            }},
            {"bet_min_level", new Dictionary<string, string> {
                {"de", "Tischminimum · 2× ab True Count +1"},
                {"en", "Table minimum · 2× from True Count +1"}
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
                {"de", "Mitten im Schuh eingestiegen – zu viele Karten sind ungesehen vorbei. Bis zum nächsten Mischen wird nur Minimum gesetzt und nach Grundstrategie gespielt; mit [F5] wird stattdessen ausgesetzt."},
                {"en", "Joined mid-shoe – too many cards went by unseen. Until the next shuffle it bets the minimum and plays plain basic strategy; with [F5] it sits out instead."}
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
                {"de", "• Einsatz-Tipp: True Count unter +1 → Minimum, +1 → 2×, +2 → 4×, +2,5 → 8×, +3 → 16× Minimum, ab +4 → Tischmaximum."},
                {"en", "• Bet tip: True Count below +1 → minimum, +1 → 2×, +2 → 4×, +2.5 → 8×, +3 → 16× minimum, +4+ → table max."}
            }},
            {"help8", new Dictionary<string, string> {
                {"de", "• {0}. Die Strategie stellt sich automatisch um, sobald es feststeht."},
                {"en", "• {0}. The strategy switches over automatically once it is established."}
            }},
            {"help9", new Dictionary<string, string> {
                {"de", "• Aussetzen [F5]: unter True Count {0:+0.#;-0.#;0} wird nicht gesetzt – und ebenso, solange die Zählung nach einem Einstieg mitten im Schuh unzuverlässig ist. Du wirst dabei vom Platz gestellt, zählst aber weiter mit und setzt dich ab True Count {1:+0.#;-0.#;0} wieder hin."},
                {"en", "• Sit out [F5]: below True Count {0:+0.#;-0.#;0} no bet is placed – same while the count is unreliable after joining mid-shoe. The server takes your seat, but counting continues and it sits back down from True Count {1:+0.#;-0.#;0}."}
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
            // -- Wonging / sitting out --
            {"wong_toggle", new Dictionary<string, string> {
                {"de", "  Schlechte Schuhe aussetzen [F5]  (ab TC {0:+0.#;-0.#;0})"},
                {"en", "  Sit out bad shoes [F5]  (from TC {0:+0.#;-0.#;0})"}
            }},
            {"rejoin_toggle", new Dictionary<string, string> {
                {"de", "  Nach Tischende neuen Tisch suchen [F6]"},
                {"en", "  Find a new table when this one ends [F6]"}
            }},
            {"sit_out", new Dictionary<string, string> {
                {"de", "AUSSETZEN"},
                {"en", "SIT OUT"}
            }},
            {"sit_out_unreliable", new Dictionary<string, string> {
                {"de", "Zählung unzuverlässig · zurück nach dem Mischen"},
                {"en", "Count unreliable · back after the shuffle"}
            }},
            {"sit_out_bad_shoe", new Dictionary<string, string> {
                {"de", "Schuh gehört der Bank · zurück ab True Count {0:+0.#;-0.#;0}"},
                {"en", "Shoe belongs to the house · back from True Count {0:+0.#;-0.#;0}"}
            }},
            {"bet_tip_sit_out", new Dictionary<string, string> {
                {"de", "Einsatz-Tipp: aussetzen"},
                {"en", "Bet tip: sit out"}
            }},
            {"forced_min", new Dictionary<string, string> {
                {"de", "Pflichtrunde: Minimum, damit der Platz nicht verfällt"},
                {"en", "Forced round: minimum, so the seat is not lost"}
            }},
            {"waiting_outside", new Dictionary<string, string> {
                {"de", "WARTE NEBEN DEM TISCH"},
                {"en", "WAITING BESIDE THE TABLE"}
            }},
            {"waiting_outside_unreliable", new Dictionary<string, string> {
                {"de", "Zählung noch unzuverlässig - zurück nach dem Mischen"},
                {"en", "Count still unreliable - back after the shuffle"}
            }},
            {"waiting_outside_count", new Dictionary<string, string> {
                {"de", "True Count {0:+0.0;-0.0;0.0} · zurück ab {1:+0.#;-0.#;0}"},
                {"en", "True Count {0:+0.0;-0.0;0.0} · back from {1:+0.#;-0.#;0}"}
            }},
            {"table_too_expensive", new Dictionary<string, string> {
                {"de", "TISCH ZU TEUER FÜR DEIN GUTHABEN"},
                {"en", "TABLE TOO EXPENSIVE FOR YOUR BANKROLL"}
            }},
            {"table_too_expensive_why", new Dictionary<string, string> {
                {"de", "Guthaben = {0} Tischminimum · Einsätze bleiben am Minimum (für den vollen Spread bräuchte es ~{1:N0})"},
                {"en", "Bankroll = {0} table minimums · bets stay at the minimum (the full spread would want ~{1:N0})"}
            }},
            {"hands_per_round", new Dictionary<string, string> {
                {"de", "Hände pro Runde:"},
                {"en", "Hands per round:"}
            }},
            {"private_tickets", new Dictionary<string, string> {
                {"de", "Privater Tisch · {0} Tickets übrig"},
                {"en", "Private table · {0} tickets left"}
            }},
            {"rejoin_private_toggle", new Dictionary<string, string> {
                {"de", "auch private Tische neu eröffnen (kostet ein Ticket)"},
                {"en", "open a new private table too (costs a ticket)"}
            }},
            {"rejoin_private_toggle_n", new Dictionary<string, string> {
                {"de", "auch private Tische neu eröffnen (kostet 1 von {0} Tickets)"},
                {"en", "open a new private table too (costs 1 of {0} tickets)"}
            }},
            {"waiting_seat_full", new Dictionary<string, string> {
                {"de", "Schuh ist gut - Tisch ist voll, warte auf einen Platz"},
                {"en", "Shoe is good - table is full, waiting for a seat"}
            }},
            {"waiting_seat", new Dictionary<string, string> {
                {"de", "Schuh ist gut - nehme den nächsten freien Platz"},
                {"en", "Shoe is good - taking the next free seat"}
            }},
            // -- Count reliability --
            {"count_unreliable", new Dictionary<string, string> {
                {"de", "ZÄHLUNG UNZUVERLÄSSIG"},
                {"en", "COUNT UNRELIABLE"}
            }},
            {"count_unreliable_raw", new Dictionary<string, string> {
                {"de", "roher True Count {0:+0.0;-0.0;0.0} - wird nicht verwendet"},
                {"en", "raw True Count {0:+0.0;-0.0;0.0} - not used"}
            }},
            // -- Dealer rule detection --
            {"rule_unknown", new Dictionary<string, string> {
                {"de", "Dealer soft 17: wird noch beobachtet"},
                {"en", "Dealer soft 17: still watching"}
            }},
            {"rule_h17", new Dictionary<string, string> {
                {"de", "Dealer zieht bei soft 17 (H17)"},
                {"en", "Dealer hits soft 17 (H17)"}
            }},
            {"rule_s17", new Dictionary<string, string> {
                {"de", "Dealer bleibt bei soft 17 (S17)"},
                {"en", "Dealer stands on soft 17 (S17)"}
            }},
            // -- Strategy deviations (Illustrious 18 / Fab 4 / H17) --
            {"dev_stand", new Dictionary<string, string> {
                {"de", "Abweichung: {0} gegen {1} ab TC {2:+0.#;-0.#;0} stehen"},
                {"en", "Deviation: stand {0} vs {1} from TC {2:+0.#;-0.#;0}"}
            }},
            {"dev_hit", new Dictionary<string, string> {
                {"de", "Abweichung: {0} gegen {1} ab TC {2:+0.#;-0.#;0} ziehen"},
                {"en", "Deviation: hit {0} vs {1} from TC {2:+0.#;-0.#;0}"}
            }},
            {"dev_hit_below", new Dictionary<string, string> {
                {"de", "Abweichung: {0} gegen {1} unter TC {2:+0.#;-0.#;0} ziehen"},
                {"en", "Deviation: hit {0} vs {1} below TC {2:+0.#;-0.#;0}"}
            }},
            {"dev_double", new Dictionary<string, string> {
                {"de", "Abweichung: {0} gegen {1} ab TC {2:+0.#;-0.#;0} verdoppeln"},
                {"en", "Deviation: double {0} vs {1} from TC {2:+0.#;-0.#;0}"}
            }},
            {"dev_surrender", new Dictionary<string, string> {
                {"de", "Abweichung: {0} gegen {1} ab TC {2:+0.#;-0.#;0} aufgeben"},
                {"en", "Deviation: surrender {0} vs {1} from TC {2:+0.#;-0.#;0}"}
            }},
            {"dev_split", new Dictionary<string, string> {
                {"de", "Abweichung: {0},{0} gegen {1} ab TC {2:+0.#;-0.#;0} teilen"},
                {"en", "Deviation: split {0},{0} vs {1} from TC {2:+0.#;-0.#;0}"}
            }},
            {"h17_double", new Dictionary<string, string> {
                {"de", "H17: {0} gegen {1} verdoppeln"},
                {"en", "H17: double {0} vs {1}"}
            }},
            {"h17_surrender", new Dictionary<string, string> {
                {"de", "H17: {0} gegen {1} aufgeben"},
                {"en", "H17: surrender {0} vs {1}"}
            }},
            {"card_ace", new Dictionary<string, string> {
                {"de", "Ass"},
                {"en", "Ace"}
            }},
            // -- Session statistics --
            {"session_title", new Dictionary<string, string> {
                {"de", "── SITZUNG ──"},
                {"en", "── SESSION ──"}
            }},
            {"session_empty", new Dictionary<string, string> {
                {"de", "Noch keine gewertete Runde."},
                {"en", "No round counted yet."}
            }},
            {"session_rounds", new Dictionary<string, string> {
                {"de", "{0} Runden gespielt · {1} ausgesetzt"},
                {"en", "{0} rounds played · {1} sat out"}
            }},
            {"session_wagered", new Dictionary<string, string> {
                {"de", "Gesamteinsatz {0:N0}  ·  {1:0} Runden/h"},
                {"en", "Total wagered {0:N0}  ·  {1:0} rounds/h"}
            }},
            {"session_edge", new Dictionary<string, string> {
                {"de", "Dein Schnitt: {0:+0.0;-0.0;0.0} % vom Einsatz"},
                {"en", "Your return: {0:+0.0;-0.0;0.0} % of amount wagered"}
            }},
            {"session_expected", new Dictionary<string, string> {
                {"de", "Erwartet wären {0:N0} (±{1:N0} Schwankung)"},
                {"en", "Expected would be {0:N0} (±{1:N0} swing)"}
            }},
            {"session_sigma", new Dictionary<string, string> {
                {"de", "{0:+0.0;-0.0;0.0} Standardabweichungen"},
                {"en", "{0:+0.0;-0.0;0.0} standard deviations"}
            }},
            {"session_reset", new Dictionary<string, string> {
                {"de", "Statistik zurücksetzen"},
                {"en", "Reset statistics"}
            }},
            {"session_horizon", new Dictionary<string, string> {
                {"de", "Ein Vorteil von 1 % hebt sich erst nach etwa {0:N0} Runden sicher vom Zufall ab{1}. Bis dahin sagt das Ergebnis wenig."},
                {"en", "A 1 % edge only rises clear of chance after about {0:N0} rounds{1}. Until then the result says little."}
            }},
            {"session_horizon_pace", new Dictionary<string, string> {
                {"de", " - bei deinem Tempo rund {0:N0} Stunden"},
                {"en", " - at your pace about {0:N0} hours"}
            }},
            {"verdict_too_few", new Dictionary<string, string> {
                {"de", "Noch viel zu wenige Runden für eine Aussage."},
                {"en", "Far too few rounds to tell anything yet."}
            }},
            {"verdict_way_above", new Dictionary<string, string> {
                {"de", "Weit über der Erwartung - das ist eine Glückssträhne, kein Dauerzustand."},
                {"en", "Far above expectation - that is a lucky run, not a steady state."}
            }},
            {"verdict_above", new Dictionary<string, string> {
                {"de", "Über der Erwartung, aber noch im Bereich des Zufalls."},
                {"en", "Above expectation, but still within the range of chance."}
            }},
            {"verdict_way_below", new Dictionary<string, string> {
                {"de", "Weit unter der Erwartung - Pechsträhne, nicht unbedingt ein Fehler im System."},
                {"en", "Far below expectation - a bad run, not necessarily a flaw in the system."}
            }},
            {"verdict_below", new Dictionary<string, string> {
                {"de", "Unter der Erwartung, aber noch im Bereich des Zufalls."},
                {"en", "Below expectation, but still within the range of chance."}
            }},
            {"verdict_normal", new Dictionary<string, string> {
                {"de", "Genau im Rahmen des Zufalls - so sieht es normal aus."},
                {"en", "Right within the range of chance - this is what normal looks like."}
            }},
            // -- Split hands --
            {"hand_label", new Dictionary<string, string> {
                {"de", "Hand {0}: "},
                {"en", "Hand {0}: "}
            }},
            {"you_label", new Dictionary<string, string> {
                {"de", "Du: "},
                {"en", "You: "}
            }},
            {"hand_vs_dealer", new Dictionary<string, string> {
                {"de", "{0}{1}{2}  ·  Dealer: {3}"},
                {"en", "{0}{1}{2}  ·  Dealer: {3}"}
            }},
            {"minimum_word", new Dictionary<string, string> {
                {"de", "Minimum"},
                {"en", "minimum"}
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
