using System;
using System.Collections.Generic;
using BJCCheat;
using Game.Common;

static class Program
{
    static int _failed, _passed;
    static readonly StrategyAdvisor A = new StrategyAdvisor();
    static readonly RuleSet S17 = new RuleSet { DealerHitsSoft17 = false };
    static readonly RuleSet H17 = new RuleSet { DealerHitsSoft17 = true };

    // total, dealer RANK (1=A, 11/12/13 = J/Q/K), flags
    static void Check(string label, string expected, int total, int dealer,
        bool soft = false, bool pair = false, bool canSplit = false,
        bool canDouble = true, bool canSurrender = true, float tc = 0f, RuleSet rules = null)
    {
        var got = A.Advise(total, dealer, soft, pair, canSplit, canDouble, canSurrender, tc, rules ?? S17);
        if (got.Action == expected) { _passed++; return; }
        _failed++;
        Console.WriteLine($"FAIL  {label,-46} erwartet {expected,-9} bekam {got.Action} ({got.Reason})");
    }

    static void Eq2(string label, object expected, object got)
    {
        if (Equals(expected, got)) return;   // nur Fehler melden, sonst flutet es
        _failed++;
        Console.WriteLine($"FAIL  {label,-46} erwartet {expected,-9} bekam {got}");
    }

    static void Eq(string label, object expected, object got)
    {
        if (Equals(expected, got)) { _passed++; return; }
        _failed++;
        Console.WriteLine($"FAIL  {label,-46} erwartet {expected,-9} bekam {got}");
    }

    // ───────────────────── Counting ─────────────────────

    static List<Card> Cards(params int[] ranks)
    {
        var l = new List<Card>();
        char suit = 's';
        foreach (int r in ranks) { l.Add(new Card(r, true, suit)); suit = suit == 's' ? 'h' : 's'; }
        return l;
    }

    static void CounterTests()
    {
        // Hi-Lo values: 2-6 = +1, 7-9 = 0, 10/J/Q/K/A = -1
        var c = new CardCounter();
        c.CountCard(2); c.CountCard(6);
        Eq("Hi-Lo kleine Karten", 2, c.RunningCount);
        c.CountCard(7); c.CountCard(9);
        Eq("Hi-Lo 7-9 neutral", 2, c.RunningCount);
        c.CountCard(10); c.CountCard(13); c.CountCard(1);
        Eq("Hi-Lo Zehner und Ass", -1, c.RunningCount);
        Eq("Karten gezaehlt", 7, c.CardsSeen);

        // The same card must not be counted twice within a round
        c = new CardCounter();
        var table = Cards(5, 5, 10);          // 5s, 5h, 10s -> +1 +1 -1
        c.CountVisible(table);
        c.CountVisible(table);                 // same update again
        Eq("CountVisible ist idempotent", 1, c.RunningCount);
        Eq("CountVisible zaehlt 3 Karten", 3, c.CardsSeen);

        // A new card arriving in the same round is counted
        var grown = Cards(5, 5, 10);
        grown.Add(new Card(4, true, 'c'));
        c.CountVisible(grown);
        Eq("neue Karte derselben Runde", 2, c.RunningCount);

        // True count = running count / decks remaining
        c = new CardCounter();
        c.UpdateShoe(312, 156, 78);            // 6 decks, half dealt -> 3 left
        for (int i = 0; i < 6; i++) c.CountCard(5);
        Eq("True Count bei 3 Decks Rest", 2f, c.TrueCount(6));

        // Angezeigter und verwendeter Wert muessen derselbe sein: 5 / 2,538 Decks = 1,9697,
        // angezeigt als +2,0 - also muss auch die Stufe "4x ab +2" greifen
        c = new CardCounter();
        c.UpdateShoe(312, 180, 78);            // 132 Karten = 2,538 Decks Rest
        for (int i = 0; i < 5; i++) c.CountCard(5);
        Eq("True Count auf eine Stelle gerundet", 2f, c.TrueCount(6));
        Eq("gerundeter Count loest die Stufe aus", 4f, CardCounter.SpreadUnits(c.TrueCount(6)));

        // Dasselbe an der Rueckkehrschwelle: knapp unter null darf nicht als +0,0 angezeigt
        // werden und dann doch draussen bleiben
        c = new CardCounter();
        c.UpdateShoe(312, 52, 78);             // 260 Karten = 5 Decks Rest
        c.CountCard(10);                        // RC -1 -> -0,2
        Eq("True Count negativ gerundet", -0.2f, c.TrueCount(6));
        c.CountCard(5);                         // RC 0 -> glatt null
        Eq("True Count genau null", 0f, c.TrueCount(6));
        Eq("bei null wieder hinsetzen", true, CardCounter.ShouldComeBack(c.TrueCount(6), true));

        // A reshuffle (dealt count drops) resets the count
        bool shuffled = c.UpdateShoe(312, 10, 78);
        Eq("Reshuffle erkannt", true, shuffled);
        Eq("Reshuffle setzt Count zurueck", 0, c.RunningCount);

        // ── Zuverlaessigkeit: genau das war der Bug ──

        // Von Anfang an dabei: alles gesehen, Zaehlung gilt
        c = new CardCounter();
        c.UpdateShoe(312, 20, 78);
        for (int i = 0; i < 20; i++) c.CountCard(7);
        Eq("frischer Schuh ist zuverlaessig", false, c.JoinedMidShoe);

        // Mitten im Schuh eingestiegen: 150 Karten weg, keine davon gesehen
        c = new CardCounter();
        c.UpdateShoe(312, 150, 78);
        for (int i = 0; i < 10; i++) c.CountCard(7);
        Eq("Einstieg mitten im Schuh ist unzuverlaessig", true, c.JoinedMidShoe);

        // ... und bleibt es bis zum Mischen, selbst wenn man ab da alles sieht
        for (int i = 0; i < 60; i++) c.CountCard(7);
        c.UpdateShoe(312, 220, 78);
        Eq("bleibt unzuverlaessig bis zum Mischen", true, c.JoinedMidShoe);

        // Erst der Reshuffle heilt es
        c.UpdateShoe(312, 6, 78);
        Eq("nach dem Mischen wieder zuverlaessig", false, c.JoinedMidShoe);
    }

    // ───────────────────── Betting / wonging ─────────────────────

    static void BettingTests()
    {
        Eq("Spread TC 0", 1f, CardCounter.SpreadUnits(0f));
        Eq("Spread TC +0.9", 1f, CardCounter.SpreadUnits(0.9f));
        Eq("Spread TC +1", 2f, CardCounter.SpreadUnits(1f));
        Eq("Spread TC +2", 4f, CardCounter.SpreadUnits(2f));
        Eq("Spread TC +2.5", 8f, CardCounter.SpreadUnits(2.5f));
        Eq("Spread TC +3", 16f, CardCounter.SpreadUnits(3f));
        Eq("Spread steigt monoton", true,
            CardCounter.SpreadUnits(1f) <= CardCounter.SpreadUnits(2f) &&
            CardCounter.SpreadUnits(2f) <= CardCounter.SpreadUnits(2.5f) &&
            CardCounter.SpreadUnits(2.5f) <= CardCounter.SpreadUnits(3f));

        // min 100, max 10000, step 100, stack 1000000
        Eq("Einsatz TC 0 = Minimum", 100L, CardCounter.RecommendedBet(0f, 1000000, 100, 10000, 100));
        Eq("Einsatz TC +2 = 4x", 400L, CardCounter.RecommendedBet(2f, 1000000, 100, 10000, 100));
        // Frueher stand hier "TC +4 = Tischmaximum", und genau das hat ein Konto
        // geleert: das Tischmaximum hat mit dem Guthaben nichts zu tun. Bei 1.000.000
        // und 1,5 % Vorteil traegt halbes Kelly 5.681, abgerundet auf die
        // Schrittweite 5.600 - nicht die 10.000, die der Tisch erlauben wuerde.
        Eq("Einsatz TC +4 vom Guthaben gedeckelt", 5600L, CardCounter.RecommendedBet(4f, 1000000, 100, 10000, 100));
        Eq("Einsatz nie unter Minimum", 100L, CardCounter.RecommendedBet(-5f, 1000000, 100, 10000, 100));
        // Winziges Guthaben: der Deckel liegt unter dem Tischminimum, also bleibt es
        // beim Minimum - und das ist weiterhin hoechstens der Stack.
        Eq("Einsatz nie ueber Stack", 100L, CardCounter.RecommendedBet(4f, 250, 100, 10000, 0));
        // 8 units x 100 = 800, abgerundet auf die Schrittweite 250 -> 750
        Eq("Einsatz haelt Schrittweite", 750L, CardCounter.RecommendedBet(2.5f, 1000000, 100, 10000, 250));

        // Wonging threshold on a count we trust
        Eq("Aussetzen bei TC -1.1", true, CardCounter.ShouldSitOut(-1.1f));
        Eq("kein Aussetzen bei TC -1", false, CardCounter.ShouldSitOut(-1f));
        Eq("kein Aussetzen bei TC 0", false, CardCounter.ShouldSitOut(0f));

        // Die Regel, die vorher gefehlt hat: unzuverlaessige Zaehlung = aussetzen,
        // egal welche Zahl gerade herauskommt
        Eq("Aussetzen bei unzuverlaessiger Zaehlung", true, CardCounter.ShouldSitOut(0f, false));
        Eq("Aussetzen trotz gutem Count, wenn unzuverlaessig", true, CardCounter.ShouldSitOut(5f, false));
        Eq("kein Aussetzen bei TC 0 und zuverlaessig", false, CardCounter.ShouldSitOut(0f, true));
        Eq("Aussetzen bei TC -2 und zuverlaessig", true, CardCounter.ShouldSitOut(-2f, true));

        // Rueckkehr an den Tisch: Totband, sonst steht man jede Runde auf und setzt sich wieder
        Eq("Rueckkehr bei TC 0", true, CardCounter.ShouldComeBack(0f, true));
        Eq("Rueckkehr bei TC +2", true, CardCounter.ShouldComeBack(2f, true));
        Eq("keine Rueckkehr bei TC -0.5", false, CardCounter.ShouldComeBack(-0.5f, true));
        Eq("keine Rueckkehr bei unzuverlaessiger Zaehlung", false, CardCounter.ShouldComeBack(3f, false));
        // Totband: zwischen Rausgehen und Reinkommen darf es keine Ueberschneidung geben
        Eq("Totband vorhanden", true, CardCounter.WongInTrueCount > CardCounter.WongOutTrueCount);
        for (float t = -3f; t <= 3f; t += 0.1f)
        {
            if (CardCounter.ShouldSitOut(t, true) && CardCounter.ShouldComeBack(t, true))
            {
                _failed++;
                Console.WriteLine($"FAIL  Aussetzen und Rueckkehr gleichzeitig bei TC {t:0.0}");
                break;
            }
        }
        _passed++;
    }


    // ───────────────────── Back-counting state ─────────────────────

    static void WongStateTests()
    {
        // Der Fehler, der dreimal durchgerutscht ist: zwischen "Einsatz ausgelassen"
        // und "Server stellt hoch" vergehen Sekunden, in denen man noch sitzt. Jede
        // Server-Nachricht in der Zeit darf die Absicht NICHT loeschen.
        var w = new WongState();
        w.SkippedBet();
        Eq("Absicht nach ausgelassenem Einsatz", true, w.SitOutIntent);
        for (int i = 0; i < 10; i++)
        {
            Eq("Sitzplatz-Meldung meldet keine Rueckkehr", false, w.ObserveSeated());
        }
        Eq("Absicht ueberlebt die Wartezeit", true, w.SitOutIntent);
        Eq("noch nicht draussen", false, w.IsOutside);

        // Jetzt stellt der Server hoch
        Eq("Platzverlust wird einmal gemeldet", true, w.ObserveNoSeat());
        Eq("Platzverlust meldet sich nicht nochmal", false, w.ObserveNoSeat());
        Eq("jetzt draussen", true, w.IsOutside);
        Eq("Absicht steht weiter", true, w.SitOutIntent);

        // Rueckkehr an den Tisch
        Eq("Rueckkehr wird einmal gemeldet", true, w.ObserveSeated());
        Eq("Rueckkehr meldet sich nicht nochmal", false, w.ObserveSeated());
        Eq("nicht mehr draussen", false, w.IsOutside);
        Eq("Absicht beendet", false, w.SitOutIntent);

        // Ohne Absicht ist ein fehlender Platz nicht unsere Sache
        w = new WongState();
        Eq("fremder Platzverlust ignoriert", false, w.ObserveNoSeat());
        Eq("nicht draussen ohne Absicht", false, w.IsOutside);

        // Wer wieder setzt, sitzt wieder
        w = new WongState();
        w.SkippedBet();
        w.ObserveNoSeat();
        w.PlacedBet();
        Eq("Einsatz beendet die Absicht", false, w.SitOutIntent);
        Eq("Einsatz beendet das Draussenstehen", false, w.IsOutside);

        // Mehrfach ausgesetzt bleibt ausgesetzt
        w = new WongState();
        w.SkippedBet();
        w.SkippedBet();
        w.ObserveNoSeat();
        w.SkippedBet();
        Eq("mehrfaches Aussetzen bleibt draussen", true, w.IsOutside);

        // Tischwechsel setzt alles zurueck
        w.Reset();
        Eq("Reset loescht die Absicht", false, w.SitOutIntent);
        Eq("Reset loescht den Zustand", false, w.IsOutside);

        // ── Platzsuche: einmal entschieden, bleibt entschieden ──
        // Der gemeldete Fehler: Tisch voll, Schuh dreht, waehrend des Wartens faellt
        // der Count kurz unter die Linie - genau dann wird ein Platz frei und der Bot
        // laesst ihn liegen. Die Absicht muss den Durchhaenger ueberleben.
        w = new WongState();
        w.SkippedBet();
        w.ObserveNoSeat();
        Eq("sucht noch keinen Platz", false, w.SeekingSeat);
        Eq("Platzsuche wird einmal gemeldet", true, w.WantSeat());
        Eq("Platzsuche meldet sich nicht nochmal", false, w.WantSeat());
        Eq("sucht jetzt einen Platz", true, w.SeekingSeat);
        Eq("steht dabei weiter draussen", true, w.IsOutside);

        // Rundenlang kein freier Platz - die Suche laeuft weiter
        for (int i = 0; i < 50; i++)
            Eq("Suche ueberlebt das Warten", true, w.SeekingSeat);

        // Erst ein wirklich schlechter Schuh beendet sie
        Eq("Abbruch wird einmal gemeldet", true, w.StopSeeking());
        Eq("Abbruch meldet sich nicht nochmal", false, w.StopSeeking());
        Eq("sucht nicht mehr", false, w.SeekingSeat);
        Eq("steht aber weiter draussen", true, w.IsOutside);

        // ... und danach geht die Suche wieder los
        Eq("neue Suche moeglich", true, w.WantSeat());
        Eq("sucht wieder", true, w.SeekingSeat);

        // Platz bekommen beendet alles
        w.ObserveSeated();
        Eq("Platz beendet die Suche", false, w.SeekingSeat);
        Eq("Platz beendet die Absicht", false, w.SitOutIntent);

        // Ohne Aussetz-Absicht wird nie nach einem Platz gesucht: ein Platz, den der
        // Spieler selbst verlassen hat, geht uns nichts an.
        w = new WongState();
        Eq("keine Suche ohne Absicht", false, w.WantSeat());
        Eq("sucht nicht", false, w.SeekingSeat);

        // Ein gesetzter Einsatz beendet die Suche ebenfalls
        w = new WongState();
        w.SkippedBet();
        w.ObserveNoSeat();
        w.WantSeat();
        w.PlacedBet();
        Eq("Einsatz beendet die Suche", false, w.SeekingSeat);

        // Tischwechsel ebenso
        w = new WongState();
        w.SkippedBet();
        w.WantSeat();
        w.Reset();
        Eq("Reset beendet die Suche", false, w.SeekingSeat);
    }

    // ───────────────────── Einsatz gegen das Guthaben ─────────────────────

    static void BankrollTests()
    {
        // Der Fall, der ein Konto geleert hat: 628.000 Guthaben, Tischminimum 10.000,
        // also 63 Einheiten. Die Leiter wollte bei True Count +3,1 das 16-fache,
        // 160.000 - ein Viertel des gesamten Guthabens auf eine Hand.
        long br = 628000, min = 10000, max = 500000;
        Eq("ruinoeser Einsatz wird gedeckelt", 10000L,
            CardCounter.RecommendedBet(3.1f, br, min, max, 0, br));
        Eq("auch bei True Count +4 kein Tischmaximum", 10000L,
            CardCounter.RecommendedBet(4.0f, br, min, max, 0, br));
        Eq("Tisch wird als zu teuer erkannt", true, CardCounter.TableTooExpensive(br, min));

        // Mit genug Guthaben darf die Leiter wieder arbeiten
        long rich = 100000000L;
        Eq("reiches Konto darf spreaden", 160000L,
            CardCounter.RecommendedBet(3.1f, rich, min, max, 0, rich));
        Eq("reicher Tisch nicht zu teuer", false, CardCounter.TableTooExpensive(rich, min));

        // Der Deckel darf nie unter das Tischminimum druecken: weniger als das
        // Minimum ist kein gueltiger Einsatz, man kann nur den Tisch wechseln.
        for (float tc = -3f; tc <= 5f; tc += 0.5f)
        {
            long bet = CardCounter.RecommendedBet(tc, br, min, max, 0, br);
            Eq("nie unter Tischminimum (TC " + tc + ")", true, bet >= min);
            Eq("nie ueber Tischmaximum (TC " + tc + ")", true, bet <= max);
        }

        // Ohne Vorteil rechtfertigt kein Guthaben einen Aufschlag
        Eq("kein Vorteil, kein Deckel ueber Null", 0L, CardCounter.MaxDefensibleBet(br, 1f));
        Eq("negativer Count erst recht nicht", 0L, CardCounter.MaxDefensibleBet(br, -2f));
        Eq("ohne Guthaben nichts", 0L, CardCounter.MaxDefensibleBet(0, 5f));
        Eq("negatives Guthaben nichts", 0L, CardCounter.MaxDefensibleBet(-100, 5f));

        // Der Deckel waechst mit Guthaben und mit dem Vorteil, nie andersherum
        Eq("mehr Guthaben, groesserer Deckel", true,
            CardCounter.MaxDefensibleBet(2000000, 3f) > CardCounter.MaxDefensibleBet(1000000, 3f));
        Eq("hoeherer Count, groesserer Deckel", true,
            CardCounter.MaxDefensibleBet(1000000, 4f) > CardCounter.MaxDefensibleBet(1000000, 3f));
        // Linear im Guthaben, bis auf das Abschneiden auf ganze Einheiten
        Eq("doppeltes Guthaben, doppelter Deckel", true,
            Math.Abs(CardCounter.MaxDefensibleBet(2000000, 3f)
                     - 2 * CardCounter.MaxDefensibleBet(1000000, 3f)) <= 1);

        // Hin und zurueck: das noetige Guthaben fuer einen Einsatz muss genau diesen
        // Einsatz wieder erlauben. Faellt das auseinander, luegt eine der beiden Zahlen.
        for (float tc = 1.5f; tc <= 5f; tc += 0.5f)
        {
            long want = 50000;
            long need = CardCounter.BankrollNeededFor(want, tc);
            Eq("Umkehrung traegt den Einsatz (TC " + tc + ")", true,
                CardCounter.MaxDefensibleBet(need, tc) >= want - 1);
        }
        Eq("ohne Vorteil kein noetiges Guthaben", 0L, CardCounter.BankrollNeededFor(50000, 1f));

        // Die alte Signatur ohne Guthaben nimmt den Stack dafuer - sonst waere der
        // Schutz je nach Aufrufstelle an oder aus.
        Eq("alte Signatur deckelt ebenfalls",
            CardCounter.RecommendedBet(3.1f, br, min, max, 0, br),
            CardCounter.RecommendedBet(3.1f, br, min, max, 0));

        // Schrittweite bleibt gewahrt
        long stepped = CardCounter.RecommendedBet(3.0f, 50000000, 10000, 500000, 25000, 50000000);
        Eq("Schrittweite eingehalten", 0L, stepped % 25000);
    }

    // ───────────────────── Mehrere Hände (privater Tisch) ─────────────────────

    static void PerHandBetTests()
    {
        // Eine Hand bleibt unangetastet - die ganze Rechnung darf den Normalfall
        // nicht anfassen.
        Eq("eine Hand unveraendert", 10000L, CardCounter.PerHandBet(10000, 1, 1000, 100));
        Eq("null Haende wie eine", 10000L, CardCounter.PerHandBet(10000, 0, 1000, 100));
        Eq("negative Haende wie eine", 10000L, CardCounter.PerHandBet(10000, -3, 1000, 100));

        // Zwei und drei Hände: 73 % bzw. 60 % der Einzelhand
        Eq("zwei Haende 73%", 7300L, CardCounter.PerHandBet(10000, 2, 0, 0));
        Eq("drei Haende 60%", 6000L, CardCounter.PerHandBet(10000, 3, 0, 0));

        // Mehr als erlaubt wird gedeckelt, nicht hochgerechnet
        Eq("vier Haende wie drei", CardCounter.PerHandBet(10000, 3, 0, 0),
                                   CardCounter.PerHandBet(10000, 4, 0, 0));

        // Pro Hand weniger, insgesamt mehr - das ist der Sinn der Sache, und wenn
        // sich das Vorzeichen dreht, stimmt die Rechnung nicht mehr.
        long one = CardCounter.PerHandBet(10000, 1, 0, 0);
        long two = CardCounter.PerHandBet(10000, 2, 0, 0);
        long three = CardCounter.PerHandBet(10000, 3, 0, 0);
        Eq("zwei Haende: pro Hand weniger", true, two < one);
        Eq("drei Haende: pro Hand noch weniger", true, three < two);
        Eq("zwei Haende: insgesamt mehr", true, 2 * two > one);
        Eq("drei Haende: insgesamt mehr als zwei", true, 3 * three > 2 * two);

        // Der Gesamteinsatz darf nicht davonlaufen: hoechstens das Doppelte einer
        // Einzelhand, sonst waere die Risikoangleichung sinnlos.
        Eq("zwei Haende unter 2x gesamt", true, 2 * two <= 2 * one);
        Eq("drei Haende unter 2x gesamt", true, 3 * three <= 2 * one);

        // Tischminimum schlaegt die Verkleinerung - unter das Minimum geht kein Einsatz
        Eq("Minimum wird eingehalten", 10000L, CardCounter.PerHandBet(10000, 3, 10000, 0));
        Eq("Minimum schlaegt zwei Haende", 10000L, CardCounter.PerHandBet(12000, 2, 10000, 0));

        // Schrittweite: das Ergebnis muss ein gueltiger Einsatz sein
        for (int hands = 1; hands <= CardCounter.MaxHands; hands++)
        {
            long bet = CardCounter.PerHandBet(13337, hands, 1000, 1000);
            Eq("Schrittweite eingehalten (" + hands + " Haende)", 0L, bet % 1000);
            Eq("nicht unter Minimum (" + hands + " Haende)", true, bet >= 1000);
        }
    }

    // ───────────────────── Log-Rauschfilter ─────────────────────

    static void NoiseFilterTests()
    {
        // Die gemeldete Zeile verschwindet
        Eq("Unity-Rauschen wird erkannt", true,
            NoiseFilter.Match("Unity Log",
                "Sub-emitters may not use stop actions. The Stop action will not be executed.") != null);

        // ... auch mit Zusatztext davor oder dahinter
        Eq("Rauschen mit Vorspann", true,
            NoiseFilter.Match("Unity Log",
                "Particle System: Sub-emitters may not use stop actions.") != null);

        // Ein Filter, der zu breit trifft, schluckt echte Fehler. Das darf nie passieren:
        Eq("fremde Unity-Meldung bleibt", null,
            NoiseFilter.Match("Unity Log", "NullReferenceException: Object reference not set"));
        Eq("Unity-Fehler bleibt", null,
            NoiseFilter.Match("Unity Log", "Sub-emitters are fine, this is a real error"));

        // Nur Unity wird gefiltert - ein Plugin wird nie stummgeschaltet, egal wie sein
        // Text aussieht. Sonst koennte ein Mod sich hinter einer Engine-Meldung verstecken.
        Eq("eigenes Plugin bleibt", null,
            NoiseFilter.Match("BJC Cheat Suite",
                "Sub-emitters may not use stop actions. The Stop action will not be executed."));
        Eq("fremdes Plugin bleibt", null,
            NoiseFilter.Match("SomeOtherMod",
                "Sub-emitters may not use stop actions."));
        Eq("BepInEx selbst bleibt", null,
            NoiseFilter.Match("BepInEx",
                "Sub-emitters may not use stop actions."));

        // Gross-/Kleinschreibung zaehlt: Unity schreibt die Meldung so, wie sie dasteht
        Eq("andere Schreibweise bleibt", null,
            NoiseFilter.Match("Unity Log", "SUB-EMITTERS MAY NOT USE STOP ACTIONS"));

        // Leeres und Fehlendes faellt nicht durch
        Eq("leerer Text bleibt", null, NoiseFilter.Match("Unity Log", ""));
        Eq("null-Text bleibt", null, NoiseFilter.Match("Unity Log", null));
        Eq("null-Quelle bleibt", null,
            NoiseFilter.Match(null, "Sub-emitters may not use stop actions."));

        // Jedes Muster muss sich selbst treffen - ein Tippfehler im Muster faellt sonst
        // nie auf, weil ein Filter, der nichts trifft, genauso aussieht wie keiner.
        foreach (string p in NoiseFilter.Patterns)
        {
            Eq("Muster trifft sich selbst: " + p, p, NoiseFilter.Match("Unity Log", p));
            Eq("Muster ist nicht leer", true, p.Length > 10);
        }
    }

    // ───────────────────── Session statistics ─────────────────────

    static void StatsTests()
    {
        // One won round: bet 100, stack goes up by 100
        SessionStats.Reset();
        long stack = 10000;
        SessionStats.CloseRound(stack);                 // baseline only
        Eq("erste Runde ist nur Basislinie", 0, SessionStats.RoundsPlayed);

        SessionStats.NoteBet(100);
        stack += 100;
        SessionStats.CloseRound(stack);
        Eq("gewonnene Runde gezaehlt", 1, SessionStats.RoundsPlayed);
        Eq("Ergebnis der gewonnenen Runde", 100L, SessionStats.NetResult);
        Eq("Einsatz der gewonnenen Runde", 100L, SessionStats.TotalWagered);

        // A lost round brings the result back to zero but adds to the action
        SessionStats.NoteBet(100);
        stack -= 100;
        SessionStats.CloseRound(stack);
        Eq("verlorene Runde gezaehlt", 2, SessionStats.RoundsPlayed);
        Eq("Ergebnis wieder bei null", 0L, SessionStats.NetResult);
        Eq("Einsatz summiert sich", 200L, SessionStats.TotalWagered);
        Eq("groesster Gewinn", 100L, SessionStats.BiggestWin);
        Eq("groesster Verlust", -100L, SessionStats.BiggestLoss);

        // Double: the same stake goes at risk twice
        SessionStats.Reset();
        stack = 10000;
        SessionStats.CloseRound(stack);
        SessionStats.NoteBet(100);
        SessionStats.NoteExtraWager(100);
        stack += 200;
        SessionStats.CloseRound(stack);
        Eq("Verdopplung zaehlt doppelten Einsatz", 200L, SessionStats.TotalWagered);
        Eq("Ergebnis der Verdopplung", 200L, SessionStats.NetResult);

        // A round we sat out is tracked separately and never counted as played
        SessionStats.Reset();
        stack = 10000;
        SessionStats.CloseRound(stack);
        SessionStats.NoteSitOut();
        SessionStats.CloseRound(stack);                 // no wager this round
        Eq("ausgesetzte Runde nicht als gespielt gezaehlt", 0, SessionStats.RoundsPlayed);
        Eq("ausgesetzte Runde gezaehlt", 1, SessionStats.RoundsSatOut);
        Eq("ausgesetzte Runde ohne Einsatz", 0L, SessionStats.TotalWagered);

        // A buy-in must not be read as a win
        SessionStats.Reset();
        stack = 10000;
        SessionStats.CloseRound(stack);
        SessionStats.NoteBet(100);
        stack += 500000;                                 // nachgekauft
        SessionStats.CloseRound(stack);
        Eq("Nachkauf nicht als Gewinn gewertet", 0L, SessionStats.NetResult);
        Eq("Nachkauf nicht als Runde gewertet", 0, SessionStats.RoundsPlayed);

        // Nach dem Nachkauf muss normal weitergezaehlt werden
        SessionStats.NoteBet(100);
        stack += 100;
        SessionStats.CloseRound(stack);
        Eq("Gewinn nach Nachkauf zaehlt", 100L, SessionStats.NetResult);
        Eq("Runde nach Nachkauf zaehlt", 1, SessionStats.RoundsPlayed);

        // ── Genau der Fehler aus dem Spiel: eine Runde ohne erkannten Einsatz
        // durfte das Geld nicht verschlucken. Konto rauf, Statistik runter - unmoeglich.
        SessionStats.Reset();
        stack = 1000000;
        SessionStats.CloseRound(stack);
        SessionStats.NoteBet(10000);
        stack -= 10000;                                  // verloren
        SessionStats.CloseRound(stack);
        stack += 351000;                                 // Gewinn ohne zugeordneten Einsatz
        SessionStats.CloseRound(stack);
        Eq("nicht zugeordneter Gewinn bleibt im Ergebnis", 341000L, SessionStats.NetResult);
        Eq("nicht zugeordnete Runde wird gezaehlt", 1, SessionStats.RoundsUnattributed);
        Eq("nicht zugeordnete Runde zaehlt nicht beim Einsatz", 10000L, SessionStats.TotalWagered);

        // Invariante ueber eine lange, gemischte Sitzung: das Ergebnis ist immer
        // die Kontobewegung abzueglich erkannter Nachkaeufe - egal was dazwischen passiert
        SessionStats.Reset();
        stack = 500000;
        long startStack = stack;
        long buyIns = 0;
        SessionStats.CloseRound(stack);
        var rnd = new Random(1234);
        for (int i = 0; i < 400; i++)
        {
            int kind = rnd.Next(10);
            if (kind == 0)
            {
                // Runde ohne erkannten Einsatz
                stack += rnd.Next(-5000, 5000);
            }
            else if (kind == 1)
            {
                // Nachkauf
                long topUp = 250000;
                SessionStats.NoteBet(1000);
                stack += topUp;
                buyIns += topUp;
            }
            else
            {
                long bet = 1000 * (1 + rnd.Next(16));
                SessionStats.NoteBet(bet);
                if (rnd.Next(2) == 0) SessionStats.NoteExtraWager(bet);
                stack += rnd.Next(2) == 0 ? bet : -bet;
            }
            SessionStats.CloseRound(stack);
            Eq2("Ergebnis folgt immer dem Konto", stack - startStack - buyIns, SessionStats.NetResult);
        }
        Eq("Invariante ueber 400 Runden", stack - startStack - buyIns, SessionStats.NetResult);
        Eq("Tischwechsel bricht die Buchhaltung nicht", true, SessionStats.RoundsPlayed > 250);

        // Ein Tischwechsel setzt nur die Rundendifferenz zurueck, nicht den Startpunkt
        SessionStats.Reset();
        stack = 100000;
        SessionStats.CloseRound(stack);
        SessionStats.NoteBet(1000);
        stack += 1000;
        SessionStats.CloseRound(stack);
        SessionStats.DropStackBaseline();
        SessionStats.NoteBet(1000);
        stack += 1000;
        SessionStats.CloseRound(stack);                  // nur Basislinie
        SessionStats.NoteBet(1000);
        stack += 1000;
        SessionStats.CloseRound(stack);
        Eq("Ergebnis ueberlebt den Tischwechsel", 3000L, SessionStats.NetResult);

        // Sigma: 100 rounds a 100 chips, result exactly on expectation -> 0 sigma
        SessionStats.Reset();
        stack = 100000;
        SessionStats.CloseRound(stack);
        for (int i = 0; i < 100; i++)
        {
            SessionStats.NoteBet(100);
            long delta = (i == 0) ? 100 : 0;             // +100 insgesamt = 1 % von 10.000
            stack += delta;
            SessionStats.CloseRound(stack);
        }
        Eq("100 Runden gezaehlt", 100, SessionStats.RoundsPlayed);
        Eq("Gesamteinsatz", 10000L, SessionStats.TotalWagered);
        Eq("Erwartung = 1 % vom Einsatz", 100.0, Math.Round(SessionStats.Expected, 6));
        Eq("Standardabweichung", 1150.0, Math.Round(SessionStats.StdDev, 6));
        Eq("auf Erwartung = 0 Sigma", 0.0, Math.Round(SessionStats.Sigma, 6));
        Eq("realisierter Schnitt 1 %", 1.0, Math.Round(SessionStats.RealisedEdgePercent, 6));

        // A run far above expectation must be called out as such
        SessionStats.Reset();
        stack = 100000;
        SessionStats.CloseRound(stack);
        for (int i = 0; i < 100; i++)
        {
            SessionStats.NoteBet(100);
            stack += 150;                                // weit ueber jeder Erwartung
            SessionStats.CloseRound(stack);
        }
        Eq("Ausreisser erkannt", true, SessionStats.Sigma > 2);
        Lang.Current = "de";
        Eq("Urteil nennt die Straehne (DE)", true, SessionStats.Verdict.Contains("Glückssträhne"));
        Lang.Current = "en";
        Eq("Urteil nennt die Straehne (EN)", true, SessionStats.Verdict.Contains("lucky run"));

        // Too few rounds -> no verdict at all
        SessionStats.Reset();
        stack = 1000;
        SessionStats.CloseRound(stack);
        SessionStats.NoteBet(100);
        stack += 100;
        SessionStats.CloseRound(stack);
        Lang.Current = "de";
        Eq("zu wenige Runden fuer ein Urteil (DE)", true, SessionStats.Verdict.Contains("zu wenige"));
        Lang.Current = "en";
        Eq("zu wenige Runden fuer ein Urteil (EN)", true, SessionStats.Verdict.Contains("too few"));

        SessionStats.Reset();
    }

    // ───────────────────── Rule detection ─────────────────────

    static List<Card> Hand(params int[] ranks)
    {
        var l = new List<Card>();
        foreach (int r in ranks) l.Add(new Card(r));
        return l;
    }

    static void Expect(string label, bool? expected, RuleSet rules)
    {
        if (rules.DealerHitsSoft17 == expected) { _passed++; return; }
        _failed++;
        Console.WriteLine($"FAIL  {label,-46} erwartet {Show(expected),-9} bekam {Show(rules.DealerHitsSoft17)}");
    }

    static string Show(bool? v) => v == null ? "unbekannt" : (v.Value ? "H17" : "S17");

    static RuleSet Fresh()
    {
        RuleDetector.Reset();
        return new RuleSet();
    }

    static void RuleDetectorTests()
    {
        // Dealer draws on soft 17 -> H17 after a single observation (A,6 then a ten)
        var r = Fresh();
        RuleDetector.ObserveDealerHand(Hand(1, 6, 10), true, r);
        Expect("A,6,10 -> H17", true, r);

        // Same signal even when nobody was left to beat
        r = Fresh();
        RuleDetector.ObserveDealerHand(Hand(1, 6, 5), false, r);
        Expect("A,6,5 ohne lebende Hand -> H17", true, r);

        // Dealer stops on soft 17: needs repeated evidence
        r = Fresh();
        RuleDetector.ObserveDealerHand(Hand(1, 6), true, r);
        Expect("A,6 einmal -> noch unbekannt", null, r);
        RuleDetector.ObserveDealerHand(Hand(6, 1), true, r);
        Expect("A,6 zweimal -> noch unbekannt", null, r);
        RuleDetector.ObserveDealerHand(Hand(1, 2, 4), true, r);
        Expect("A,6 dreimal -> S17", false, r);

        // Rounds where the dealer never had to play must not count as evidence
        r = Fresh();
        for (int i = 0; i < 5; i++)
            RuleDetector.ObserveDealerHand(Hand(1, 6), false, r);
        Expect("5x soft 17 ohne lebende Hand -> unbekannt", null, r);

        // Hard 17 and other finals say nothing at all
        r = Fresh();
        for (int i = 0; i < 5; i++)
            RuleDetector.ObserveDealerHand(Hand(10, 7), true, r);
        Expect("5x hart 17 -> unbekannt", null, r);

        // Multi-ace soft 17: A,A,5 = 11+1+5
        r = Fresh();
        for (int i = 0; i < 3; i++)
            RuleDetector.ObserveDealerHand(Hand(1, 1, 5), true, r);
        Expect("3x A,A,5 -> S17", false, r);

        // Blackjack (A,10) is 21, not soft 17 - no evidence
        r = Fresh();
        for (int i = 0; i < 5; i++)
            RuleDetector.ObserveDealerHand(Hand(1, 13), true, r);
        Expect("5x A,K -> unbekannt", null, r);

        // A hidden hole card means the round is not over
        r = Fresh();
        var hidden = new List<Card> { new Card(1), new Card(0, false) };
        for (int i = 0; i < 5; i++)
            RuleDetector.ObserveDealerHand(hidden, true, r);
        Expect("verdeckte Karte -> unbekannt", null, r);

        // Once H17 is established, a later soft-17 final must not flip it back
        r = Fresh();
        RuleDetector.ObserveDealerHand(Hand(1, 6, 4), true, r);
        for (int i = 0; i < 5; i++)
            RuleDetector.ObserveDealerHand(Hand(1, 6), true, r);
        Expect("H17 bleibt H17", true, r);

        // A busted dealer hand that passed through soft 17 is still H17 evidence
        r = Fresh();
        RuleDetector.ObserveDealerHand(Hand(1, 6, 9, 7), true, r);
        Expect("A,6,9,7 (ueberkauft) -> H17", true, r);
    }

    static void Main()
    {
        // ─────────── S17 basic strategy, hard totals ───────────
        Check("hart 8 vs 6", "HIT", 8, 6);
        Check("hart 9 vs 2", "HIT", 9, 2);
        Check("hart 9 vs 3", "DOUBLE", 9, 3);
        Check("hart 9 vs 7", "HIT", 9, 7);
        Check("hart 10 vs 9", "DOUBLE", 10, 9);
        Check("hart 10 vs 10", "HIT", 10, 10);
        Check("hart 10 vs K", "HIT", 10, 13);
        Check("hart 11 vs 10", "DOUBLE", 11, 10);
        Check("hart 11 vs A (S17)", "HIT", 11, 1);
        Check("hart 12 vs 2", "HIT", 12, 2);
        Check("hart 12 vs 4", "STAND", 12, 4);
        Check("hart 13 vs 6", "STAND", 13, 6);
        Check("hart 14 vs 7", "HIT", 14, 7);
        Check("hart 16 vs 7", "HIT", 16, 7);
        Check("hart 17 vs A (S17)", "STAND", 17, 1);
        Check("hart 20 vs 10", "STAND", 20, 10);

        // Surrenders from the chart (late surrender available)
        Check("hart 16 vs 9", "SURRENDER", 16, 9);
        Check("hart 16 vs Q", "SURRENDER", 16, 12);
        Check("hart 16 vs A", "SURRENDER", 16, 1);
        Check("hart 15 vs 10 (TC 0)", "SURRENDER", 15, 10);
        Check("hart 16 vs 9 ohne Surrender", "HIT", 16, 9, canSurrender: false);
        Check("hart 16 vs 10 ohne Surrender, TC -1", "HIT", 16, 10, canSurrender: false, tc: -1f);

        // ─────────── S17 soft totals ───────────
        Check("A,2 vs 5", "DOUBLE", 13, 5, soft: true);
        Check("A,2 vs 4", "HIT", 13, 4, soft: true);
        Check("A,4 vs 4", "DOUBLE", 15, 4, soft: true);
        Check("A,6 vs 2 (S17)", "HIT", 17, 2, soft: true);
        Check("A,6 vs 3", "DOUBLE", 17, 3, soft: true);
        Check("A,7 vs 2 (S17)", "STAND", 18, 2, soft: true);
        Check("A,7 vs 6", "DOUBLE", 18, 6, soft: true);
        Check("A,7 vs 6 ohne Double", "STAND", 18, 6, soft: true, canDouble: false);
        Check("A,7 vs 9", "HIT", 18, 9, soft: true);
        Check("A,8 vs 6 (S17)", "STAND", 19, 6, soft: true);
        Check("A,9 vs 6", "STAND", 20, 6, soft: true);

        // ─────────── Pairs ───────────
        Check("A,A vs 10", "SPLIT", 12, 10, soft: true, pair: true, canSplit: true);
        Check("2,2 vs 7", "SPLIT", 4, 7, pair: true, canSplit: true);
        Check("2,2 vs 8", "HIT", 4, 8, pair: true, canSplit: true);
        Check("4,4 vs 5", "SPLIT", 8, 5, pair: true, canSplit: true);
        Check("4,4 vs 7", "HIT", 8, 7, pair: true, canSplit: true);
        Check("5,5 vs 6 = hart 10", "DOUBLE", 10, 6, pair: true, canSplit: true);
        Check("8,8 vs 10", "SPLIT", 16, 10, pair: true, canSplit: true);
        Check("9,9 vs 7", "STAND", 18, 7, pair: true, canSplit: true);
        Check("9,9 vs 9", "SPLIT", 18, 9, pair: true, canSplit: true);
        Check("10,10 vs 6 (Standard)", "STAND", 20, 6, pair: true, canSplit: true);
        Check("7,7 vs 8 ohne Split", "HIT", 14, 8, pair: true, canSplit: false);

        // ─────────── H17 differences ───────────
        Check("hart 11 vs A (H17)", "DOUBLE", 11, 1, rules: H17);
        Check("hart 15 vs A (H17)", "SURRENDER", 15, 1, rules: H17);
        Check("hart 17 vs A (H17)", "SURRENDER", 17, 1, rules: H17);
        Check("hart 17 vs A (H17, kein Surrender)", "HIT", 17, 1, canSurrender: false, rules: H17);
        Check("A,6 vs 2 (H17)", "DOUBLE", 17, 2, soft: true, rules: H17);
        Check("A,7 vs 2 (H17)", "DOUBLE", 18, 2, soft: true, rules: H17);
        Check("A,8 vs 6 (H17)", "DOUBLE", 19, 6, soft: true, rules: H17);
        Check("A,8 vs 6 (H17, kein Double)", "STAND", 19, 6, soft: true, canDouble: false, rules: H17);
        Check("8,8 vs A (H17)", "SURRENDER", 16, 1, pair: true, canSplit: true, rules: H17);
        Check("8,8 vs A (S17)", "SPLIT", 16, 1, pair: true, canSplit: true);

        // ─────────── Count deviations: must NOT fire at a neutral count ───────────
        Check("12 vs 2, TC 0", "HIT", 12, 2);
        Check("12 vs 3, TC 0", "HIT", 12, 3);
        Check("12 vs 5, TC 0", "STAND", 12, 5);
        Check("13 vs 2, TC 0", "STAND", 13, 2);
        Check("10 vs 10, TC 0", "HIT", 10, 10);
        Check("9 vs 2, TC 0", "HIT", 9, 2);
        Check("14 vs 10, TC 0", "HIT", 14, 10);

        // ─────────── Count deviations: must fire at the index ───────────
        Check("12 vs 2, TC +3", "STAND", 12, 2, tc: 3f);
        Check("12 vs 2, TC +2.9", "HIT", 12, 2, tc: 2.9f);
        Check("12 vs 3, TC +2", "STAND", 12, 3, tc: 2f);
        Check("12 vs 4, TC -0.5", "HIT", 12, 4, tc: -0.5f);
        Check("12 vs 5, TC -2", "HIT", 12, 5, tc: -2f);
        Check("12 vs 6, TC -1", "HIT", 12, 6, tc: -1f);
        Check("13 vs 2, TC -1", "HIT", 13, 2, tc: -1f);
        Check("13 vs 3, TC -2", "HIT", 13, 3, tc: -2f);
        Check("9 vs 2, TC +1", "DOUBLE", 9, 2, tc: 1f);
        Check("9 vs 7, TC +3", "DOUBLE", 9, 7, tc: 3f);
        Check("10 vs 10, TC +4", "DOUBLE", 10, 10, tc: 4f);
        Check("10 vs A, TC +4", "DOUBLE", 10, 1, tc: 4f);
        Check("11 vs A, TC +1 (S17)", "DOUBLE", 11, 1, tc: 1f);
        Check("16 vs 10 ohne Surrender, TC 0", "STAND", 16, 10, canSurrender: false, tc: 0f);
        Check("16 vs 9 ohne Surrender, TC +5", "STAND", 16, 9, canSurrender: false, tc: 5f);
        Check("15 vs 10 ohne Surrender, TC +4", "STAND", 15, 10, canSurrender: false, tc: 4f);

        // Fab 4
        Check("14 vs 10, TC +3", "SURRENDER", 14, 10, tc: 3f);
        Check("15 vs 9, TC +2", "SURRENDER", 15, 9, tc: 2f);
        Check("15 vs A, TC +1", "SURRENDER", 15, 1, tc: 1f);
        Check("15 vs 10, TC -1", "HIT", 15, 10, tc: -1f);

        // Deviations must not double when doubling is not allowed
        Check("10 vs 10, TC +4, kein Double", "HIT", 10, 10, canDouble: false, tc: 4f);
        Check("9 vs 2, TC +1, kein Double", "HIT", 9, 2, canDouble: false, tc: 1f);

        // Splitting tens stays off unless explicitly enabled
        Check("10,10 vs 6, TC +5, konservativ", "STAND", 20, 6, pair: true, canSplit: true, tc: 5f);
        A.AggressiveSplits = true;
        Check("10,10 vs 6, TC +5, aggressiv", "SPLIT", 20, 6, pair: true, canSplit: true, tc: 5f);
        Check("10,10 vs 6, TC +3, aggressiv", "STAND", 20, 6, pair: true, canSplit: true, tc: 3f);
        Check("10,10 vs 5, TC +5, aggressiv", "SPLIT", 20, 5, pair: true, canSplit: true, tc: 5f);
        A.AggressiveSplits = false;

        // Face cards must map onto 10 everywhere
        Check("hart 16 vs J", "SURRENDER", 16, 11);
        Check("hart 16 vs K ohne Surrender, TC 0", "STAND", 16, 13, canSurrender: false);
        Check("hart 12 vs Q", "HIT", 12, 12);

        // Legacy entry point stays basic strategy
        string legacy = A.GetAdvice(16, 10, false, false, false, true, false);
        if (legacy != "STAND") { _failed++; Console.WriteLine($"FAIL  GetAdvice(16 vs 10) = {legacy}"); }
        else _passed++;

        RuleDetectorTests();
        CounterTests();
        BettingTests();
        StatsTests();
        WongStateTests();
        NoiseFilterTests();
        PerHandBetTests();
        BankrollTests();

        Console.WriteLine();
        Console.WriteLine($"{_passed} bestanden, {_failed} fehlgeschlagen");
        Environment.Exit(_failed == 0 ? 0 : 1);
    }
}
