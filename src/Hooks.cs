using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Game.Blackjack;
using Game.Common;
using Game.Common.UI;
using Game.Lobby;
using GXF.Base;
using GXF.UI;

namespace BJCCheat
{
    /// <summary>
    /// Live table state for the overlay, plus auto-play: store the table ref and poll turns
    /// from Update() every frame. This avoids all timing issues with calling Action during Decode.
    /// </summary>
    public static class ActiveTable
    {
        public static BJTable Table;
        private static int _lastAutoTurnSeq = -1;
        private static int _lastPlaySeq = -1;
        // Whether this table offers surrender at all (learned from the first decision)
        private static bool _surrenderOffered;

        /// <summary>Rounds sat out in a row (wonging). Reset on a new table or a reshuffle.</summary>
        public static int SkippedRounds;

        /// <summary>
        /// Back-counting state: sit-out intent and whether the seat is already gone.
        /// See WongState for why those two must not be collapsed into one flag.
        /// </summary>
        private static readonly WongState Wong = new WongState();

        /// <summary>Standing beside the table, waiting for the shoe to turn.</summary>
        public static bool WongedOut { get { return Wong.IsOutside; } }

        /// <summary>The shoe turned and we are after a seat - for the overlay.</summary>
        public static bool SeekingSeat { get { return Wong.SeekingSeat; } }

        /// <summary>A private table behaves differently in two ways that matter here.</summary>
        public static bool IsPrivateTable
        {
            get
            {
                try { return Table != null && Table.Stage != null && Table.Stage.IsPrivate; }
                catch { return false; }
            }
        }

        private static bool _privateWongWarned;
        private static bool _noTicketLogged;

        /// <summary>
        /// Back-counting at a private table is a different bet from back-counting in a
        /// public room, and the difference is not visible from the overlay. The game
        /// offers no join buttons at a private table at all, so the seat can only be
        /// taken back through the server call directly; and at a table with no other
        /// players a skipped round can end the table outright, which then costs a
        /// ticket to replace. Said once per table - the choice stays the player's.
        /// </summary>
        private static void WarnPrivateWonging()
        {
            if (_privateWongWarned || !IsPrivateTable) return;
            _privateWongWarned = true;
            Plugin.Log.LogWarning("[WONG] this is a private table: the game shows no join buttons here, " +
                "so getting the seat back depends on the server accepting a direct sit - and if nobody " +
                "else is betting, sitting out can end the table. Switch back-counting off (F5) if that " +
                "is not what you want.");
        }

        /// <summary>Seeking a seat and not finding one - for the overlay.</summary>
        public static bool TableFull { get; private set; }

        private static float _nextResitTime;
        /// <summary>Said once per round why we are still outside.</summary>
        private static bool _waitLogged;

        /// <summary>Seats the server refused this time - try the next one instead of retrying it.</summary>
        private static readonly HashSet<int> _failedSeats = new HashSet<int>();

        /// <summary>Seat we last held - preferred when taking one again.</summary>
        private static int _lastSeatIndex = -1;

        // Auto-bet state
        private static int _lastBetPlaySeq = -1;
        private static long _lastBetAmount;
        private static float _betDelayStart = -1f;
        private static float _betDelayTarget = 2.0f;

        // Auto-play delay (anti-detection) — random 1.0–2.5s
        private static int _pendingAutoTurnSeq = -1;
        private static float _autoPlayDelayStart = -1f;
        private static float _autoPlayDelayTarget = 1.0f;
        private static readonly System.Random _rng = new System.Random();

        // Insurance auto-handling
        private static int _lastInsuranceTurnSeq = -1;
        private static float _insuranceDelayStart = -1f;
        private static float _insuranceDelayTarget = 1.5f;

        private static float RandomDelay()
        {
            // 1.0 – 2.5 seconds
            return 1.0f + (float)(_rng.NextDouble() * 1.5);
        }

        private static float RandomBetDelay()
        {
            // 2.0 – 4.0 seconds
            return 2.0f + (float)(_rng.NextDouble() * 2.0);
        }

        // BJTurn.ValidActions is protected
        private static readonly PropertyInfo ValidActionsProp = typeof(BJTurn).GetProperty("ValidActions",
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

        public static void Set(BJTable t)
        {
            // Different table = different shoe, the old count is worthless
            if (t != Table)
            {
                Plugin.Counter.Reset();
                _surrenderOffered = false;
                SkippedRounds = 0;
                Wong.Reset();
                _nextResitTime = 0f;
                _privateWongWarned = false;
                SessionStats.DropStackBaseline(); // different table, different stack
            }
            Table = t;
        }

        /// <summary>
        /// Pulls everything the overlay needs from the table. Called after every play update and action.
        /// </summary>
        public static void Refresh(BJTable t)
        {
            var play = t.Play;

            if (play.PlaySeq != _lastPlaySeq)
            {
                _lastPlaySeq = play.PlaySeq;
                _lastAutoTurnSeq = -1;
                _lastInsuranceTurnSeq = -1;
                _insuranceDelayStart = -1f;
                // NOTE: do NOT reset _pendingAutoTurnSeq/_autoPlayDelayStart here —
                // Refresh fires on every DecodeOnAction (including HIT responses),
                // resetting the delay timer mid-round would cause instant actions.
                Plugin.Counter.NewRound();
                // Seats free up between rounds - give every seat a fresh chance
                _failedSeats.Clear();
                _waitLogged = false;
                Plugin.PlayerScore = 0;
                Plugin.PlayerSoft = false;
                Plugin.PlayerPair = false;
                Plugin.DealerUpCard = 0;
            }

            // Shoe first: a reshuffle resets the count before this round's cards are counted
            try
            {
                // Keep the stage object itself, not only its code. A private table is
                // created on the spot and never appears in the lobby listing, so looking
                // its code up there afterwards finds nothing - but the server sends the
                // stage along with the table on connect, so we already hold it.
                if (!string.IsNullOrEmpty(t.StageCode)) Plugin.LastStageCode = t.StageCode;
                if (t.Stage != null) Plugin.LastStage = t.Stage;
            }
            catch { }

            if (t.Deck != null)
            {
                Plugin.NumDecks = t.Deck.NumDeck;
                Plugin.Rules.NumDecks = t.Deck.NumDeck;
                if (Plugin.Counter.UpdateShoe(t.Deck.CountAll, t.Deck.CountDealt, t.Deck.CountCut))
                {
                    SkippedRounds = 0;
                    Plugin.Log.LogInfo($"Shoe reshuffled ({t.Deck.NumDeck} decks), count reset");
                }
            }

            Plugin.PlayState = play.PlayState.ToString();

            // Stack and bet limits for the bet tip
            var seat = t.LocalSeat;
            if (seat != null)
            {
                Plugin.Stack = seat.Stack;
                // TotalChip is everything the player owns; the seat stack is only the
                // buy-in sitting on this table. Kelly is about the former.
                long owned = 0;
                try { owned = seat.TotalChip; } catch { }
                Plugin.Bankroll = owned > 0 ? owned : seat.Stack;
                _lastSeatIndex = seat.SeatIndex;
                // Fires only on the transition back onto a seat, not on every update
                if (Wong.ObserveSeated())
                {
                    Plugin.Log.LogInfo("[WONG] back at the table");
                    _waitLogged = false;
                    _failedSeats.Clear();
                    SkippedRounds = 0;
                }
            }
            else if (Wong.ObserveNoSeat())
            {
                // Expected: the server stands you up for the missed bet. We keep
                // watching the shoe from the side and take a seat again when it turns.
                SkippedRounds = 0;
                Plugin.Log.LogInfo($"[WONG] seat taken by the server - still counting, " +
                    $"back from True Count {CardCounter.WongInTrueCount:+0.#;-0.#;0}");
            }
            Plugin.BetMin = t.ValidBetMin;
            Plugin.BetMax = t.ValidBetMax;
            Plugin.BetStep = t.ValidBetStep;

            // First face-up dealer card = up card (the hole card arrives as an invalid "0u")
            var dealerCards = play.Dealer?.Hand?.Cards;
            if (dealerCards != null)
                foreach (var c in dealerCards)
                    if (c != null && c.IsValid) { Plugin.DealerUpCard = c.Rank; break; }

            UpdateLocalHand(play);
            Plugin.Counter.CountVisible(VisibleCards(play));

            if (play.PlayState == BJPlayState.RESULT && play.PlaySeq != _lastRulePlaySeq)
            {
                _lastRulePlaySeq = play.PlaySeq;
                ObserveRules(play);
                if (seat != null) SessionStats.CloseRound(seat.Stack);
            }
        }

        private static int _lastRulePlaySeq = -1;

        /// <summary>
        /// Learns the dealer's soft-17 rule from the finished hand. Only counts as
        /// evidence when at least one player hand was still live, because with every
        /// player busted the dealer never draws and would "stand" on anything.
        /// </summary>
        static void ObserveRules(BJPlay play)
        {
            var dealerCards = play.Dealer?.Hand?.Cards;
            if (dealerCards == null || dealerCards.Length < 2) return;

            bool dealerHadToPlay = false;
            if (play.Seats != null)
            {
                foreach (var seat in play.Seats)
                {
                    if (seat?.Hands == null) continue;
                    foreach (var hand in seat.Hands)
                    {
                        if (hand?.Cards == null || hand.Cards.Length == 0) continue;
                        if (!hand.IsBust && !hand.IsAbandoned && !hand.IsBlackJack)
                        {
                            dealerHadToPlay = true;
                            break;
                        }
                    }
                    if (dealerHadToPlay) break;
                }
            }

            RuleDetector.ObserveDealerHand(dealerCards, dealerHadToPlay, Plugin.Rules);
        }

        /// <summary>
        /// Feeds the advisor with the hand being played (after a split: the one in turn)
        /// and the options the server allows for it.
        /// </summary>
        static void UpdateLocalHand(BJPlay play)
        {
            BJTurn turn = LocalTurn(play);
            BJHand hand = HandOf(turn) ?? FirstLocalHand(play);
            if (hand?.Cards == null || hand.Cards.Length == 0) return;

            Plugin.PlayerScore = hand.Score;
            Plugin.PlayerSoft = hand.IsAvailableSoft;
            Plugin.PlayerPair = hand.IsPair;
            Plugin.HandSeq = turn != null ? turn.HandSeq : 0;
            LocalHands = LocalHandCount(play);

            string[] valid = turn != null ? GetValidActions(turn) : null;
            if (valid != null && (Has(valid, "HIT") || Has(valid, "STAND")))
            {
                Plugin.CanDouble = Has(valid, "DOUBLE");
                Plugin.CanSplit = Has(valid, "SPLIT");
                Plugin.CanSurrender = Has(valid, "SURRENDER");
                _surrenderOffered |= Plugin.CanSurrender;
                Plugin.Rules.SurrenderOffered = _surrenderOffered;

                // Double offered on a hand that came out of a split => DAS
                if (Plugin.CanDouble && !Plugin.Rules.DoubleAfterSplit && LocalHandCount(play) > 1)
                {
                    Plugin.Rules.DoubleAfterSplit = true;
                    Plugin.Log.LogInfo("[RULES] Double after split is allowed");
                }
            }
            else
            {
                // Not our decision yet: assume the usual first-two-cards options so the advice is ready early
                bool firstDecision = hand.Cards.Length == 2;
                Plugin.CanDouble = firstDecision;
                Plugin.CanSplit = hand.IsPair;
                Plugin.CanSurrender = firstDecision && _surrenderOffered;
            }
        }

        /// <summary>Hands the local seat is currently playing - more than one after a split.</summary>
        public static int LocalHands { get; private set; }

        static int LocalHandCount(BJPlay play)
        {
            if (play.Seats == null) return 0;
            foreach (var seat in play.Seats)
            {
                if (seat == null || !seat.IsLocalSeat || seat.Hands == null) continue;
                return seat.Hands.Length;
            }
            return 0;
        }

        /// <summary>
        /// The local turn that is actually to act. After a split there is one turn per
        /// hand and the array order is not the playing order, so prefer a turn we can
        /// act on, and among several take the lowest hand sequence - that is the order
        /// the game deals them out in. Taking play.Turns[0] blindly made the advisor
        /// rate one hand while the bot played the other.
        /// </summary>
        static BJTurn LocalTurn(BJPlay play)
        {
            if (play.Turns == null) return null;

            BJTurn actionable = null;
            BJTurn fallback = null;

            foreach (var turn in play.Turns)
            {
                if (turn?.Seat == null || !turn.Seat.IsLocalSeat) continue;

                string[] valid = GetValidActions(turn);
                bool canAct = valid != null && (Has(valid, "HIT") || Has(valid, "STAND"));

                if (canAct)
                {
                    if (actionable == null || turn.HandSeq < actionable.HandSeq)
                        actionable = turn;
                }
                else if (fallback == null || turn.HandSeq < fallback.HandSeq)
                {
                    fallback = turn;
                }
            }

            return actionable ?? fallback;
        }

        /// <summary>Hand belonging to a turn, falling back to the seat's own hand list.</summary>
        static BJHand HandOf(BJTurn turn)
        {
            if (turn == null) return null;
            try
            {
                BJHand hand = turn.Hand;
                if (hand?.Cards != null && hand.Cards.Length > 0) return hand;

                // Turn.Hand resolves through Table.Seats; if that lags behind, look the
                // hand up by its sequence on the seat we have
                BJSeat seat = turn.Seat;
                if (seat?.Hands == null) return null;
                foreach (var h in seat.Hands)
                    if (h != null && h.HandSeq == turn.HandSeq) return h;
            }
            catch { }
            return null;
        }

        static BJHand FirstLocalHand(BJPlay play)
        {
            if (play.Seats == null) return null;
            foreach (var seat in play.Seats)
            {
                if (seat?.Hands == null || !seat.IsLocalSeat) continue;
                foreach (var hand in seat.Hands)
                    if (hand?.Cards != null && hand.Cards.Length > 0) return hand;
            }
            return null;
        }

        /// <summary>
        /// All face-up cards on the table: dealer and every hand of every seat.
        /// </summary>
        static List<Card> VisibleCards(BJPlay play)
        {
            var cards = new List<Card>();
            if (play.Dealer?.Hand?.Cards != null)
                foreach (var c in play.Dealer.Hand.Cards)
                    if (c != null && c.IsValid) cards.Add(c);

            if (play.Seats != null)
                foreach (var seat in play.Seats)
                {
                    if (seat?.Hands == null) continue;
                    foreach (var hand in seat.Hands)
                    {
                        if (hand?.Cards == null) continue;
                        foreach (var c in hand.Cards)
                            if (c != null && c.IsValid) cards.Add(c);
                    }
                }
            return cards;
        }

        public static string[] GetValidActions(BJTurn turn)
        {
            try { return ValidActionsProp?.GetValue(turn) as string[]; }
            catch { return null; }
        }

        static bool Has(string[] actions, string action)
        {
            return Array.IndexOf(actions, action) >= 0;
        }

        /// <summary>
        /// Called from CheatOverlay.Update() every frame.
        /// Checks if there are pending turns for the local player and executes auto-play.
        /// </summary>
        public static void PollAutoPlay()
        {
            if (!Plugin.AutoPlayEnabled) return;
            if (Table == null || Table.Play == null || Table.Play.Turns == null) return;

            // Handle insurance phase
            if (Table.Play.PlayState == BJPlayState.INSURANCE)
            {
                PollAutoInsurance();
                return;
            }

            foreach (var turn in Table.Play.Turns)
            {
                if (turn == null) continue;
                // Skip if not local player
                if (turn.Seat == null || !turn.Seat.IsLocalSeat) continue;
                // Skip if we already sent an action for this turn
                if (turn.TurnSeq == _lastAutoTurnSeq) continue;

                string[] valid = GetValidActions(turn);
                if (valid == null || valid.Length == 0) continue;

                // Don't auto-bet here, only play actions
                bool hasBET = Has(valid, "BET");
                bool hasHIT = Has(valid, "HIT");
                bool hasSTAND = Has(valid, "STAND");
                if (!hasHIT && !hasSTAND && hasBET) continue; // BET phase, skip

                // Start random delay for this turn (anti-detection)
                if (turn.TurnSeq != _pendingAutoTurnSeq)
                {
                    _pendingAutoTurnSeq = turn.TurnSeq;
                    _autoPlayDelayStart = UnityEngine.Time.unscaledTime;
                    _autoPlayDelayTarget = RandomDelay();
                    return;
                }

                // Wait for delay
                float elapsed = UnityEngine.Time.unscaledTime - _autoPlayDelayStart;
                if (elapsed < _autoPlayDelayTarget) return;

                Plugin.CanDouble = Has(valid, "DOUBLE");
                Plugin.CanSplit = Has(valid, "SPLIT");
                Plugin.CanSurrender = Has(valid, "SURRENDER");

                // Read the hand off THIS turn. The cached values belong to whichever
                // hand the last server update was about - after a split that is routinely
                // the other one, and we would play it by the wrong total.
                BJHand turnHand = HandOf(turn);
                int score = Plugin.PlayerScore;
                bool soft = Plugin.PlayerSoft;
                bool pair = Plugin.PlayerPair;
                if (turnHand != null)
                {
                    score = turnHand.Score;
                    soft = turnHand.IsAvailableSoft;
                    pair = turnHand.IsPair;
                    Plugin.PlayerScore = score;
                    Plugin.PlayerSoft = soft;
                    Plugin.PlayerPair = pair;
                    Plugin.HandSeq = turn.HandSeq;
                }

                float playTc = Plugin.EffectiveTrueCount();
                Advice decision = Plugin.Advisor.Advise(
                    score, Plugin.DealerUpCard, soft, pair,
                    Plugin.CanSplit, Plugin.CanDouble, Plugin.CanSurrender,
                    playTc, Plugin.Rules);

                if (!string.IsNullOrEmpty(decision.Reason))
                    Plugin.Log.LogInfo($"[AUTO] {decision.Reason} (TC={playTc:+0.0;-0.0;0.0})");

                string actionStr = AdviceToActionStr(decision.Action);
                if (actionStr == null) continue;

                if (!Has(valid, actionStr))
                {
                    Plugin.Log.LogInfo($"[AUTO] {actionStr} not in [{string.Join(",", valid)}], fallback");
                    // Fallback: if advised DOUBLE but can't, HIT instead
                    if (actionStr == "DOUBLE" && hasHIT) actionStr = "HIT";
                    else if (actionStr == "SURRENDER" && hasHIT) actionStr = "HIT";
                    else continue;
                }

                _lastAutoTurnSeq = turn.TurnSeq;
                _pendingAutoTurnSeq = -1;
                _autoPlayDelayStart = -1f;
                string handTag = LocalHandCount(Table.Play) > 1 ? $"Hand {turn.HandSeq}: " : "";
                Plugin.Log.LogInfo($"[AUTO] >> {actionStr} ({handTag}you={score}{(soft ? " soft" : "")} " +
                    $"vs D={Plugin.DealerUpCard}, delay={_autoPlayDelayTarget:0.0}s)");

                try
                {
                    string sent = actionStr;
                    Table.Action(turn, actionStr, 0L, delegate(Result result)
                    {
                        if (result.IsSuccess)
                        {
                            // Double and split put the same stake at risk again
                            if (sent == "DOUBLE" || sent == "SPLIT")
                                SessionStats.NoteExtraWager(_lastBetAmount);
                            Plugin.Log.LogInfo($"[AUTO] {sent} OK");
                        }
                        else
                            Plugin.Log.LogWarning($"[AUTO] {sent} FAIL: {result}");
                    });
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"[AUTO] Error: {ex.Message}");
                    _lastAutoTurnSeq = -1; // Allow retry
                }
                break; // One action per frame
            }
        }

        /// <summary>
        /// Handles the INSURANCE phase: either take insurance or pass, based on True Count.
        /// </summary>
        private static void PollAutoInsurance()
        {
            if (Table.Play.Turns == null) return;

            foreach (var turn in Table.Play.Turns)
            {
                if (turn == null || turn.Seat == null || !turn.Seat.IsLocalSeat) continue;
                if (turn.TurnSeq == _lastInsuranceTurnSeq) continue;

                string[] valid = GetValidActions(turn);
                if (valid == null || valid.Length == 0) continue;
                if (!Has(valid, "INSURANCE") && !Has(valid, "PASS") && !Has(valid, "EVENMONEY")) continue;

                // Start delay
                if (_insuranceDelayStart < 0f)
                {
                    _insuranceDelayStart = UnityEngine.Time.unscaledTime;
                    _insuranceDelayTarget = RandomDelay();
                    return;
                }
                float elapsed = UnityEngine.Time.unscaledTime - _insuranceDelayStart;
                if (elapsed < _insuranceDelayTarget) return;

                _lastInsuranceTurnSeq = turn.TurnSeq;
                _insuranceDelayStart = -1f;

                float tc = Plugin.EffectiveTrueCount();
                bool insure = Plugin.Advisor.ShouldInsure(tc);

                // Even money is offered instead of insurance when player has blackjack
                if (Has(valid, "EVENMONEY"))
                {
                    string action = insure ? "EVENMONEY" : "PASS";
                    long amount = insure ? turn.Seat.AmountBet : 0L;
                    Plugin.Log.LogInfo($"[AUTO] >> {action} (TC={tc:+0.0;-0.0;0.0})");
                    try
                    {
                        Table.Action(turn, action, amount, delegate(Result result)
                        {
                            if (result.IsSuccess)
                                Plugin.Log.LogInfo($"[AUTO] {action} OK");
                            else
                                Plugin.Log.LogWarning($"[AUTO] {action} FAIL: {result}");
                        });
                    }
                    catch (Exception ex) { Plugin.Log.LogError($"[AUTO] Insurance error: {ex.Message}"); }
                }
                else
                {
                    string action = insure ? "INSURANCE" : "PASS";
                    long amount = insure ? turn.Seat.AmountBet / 2 : 0L;
                    Plugin.Log.LogInfo($"[AUTO] >> {action} (TC={tc:+0.0;-0.0;0.0}, insure={insure})");
                    try
                    {
                        Table.Action(turn, action, amount, delegate(Result result)
                        {
                            if (result.IsSuccess)
                                Plugin.Log.LogInfo($"[AUTO] {action} OK");
                            else
                                Plugin.Log.LogWarning($"[AUTO] {action} FAIL: {result}");
                        });
                    }
                    catch (Exception ex) { Plugin.Log.LogError($"[AUTO] Insurance error: {ex.Message}"); }
                }
                break;
            }
        }

        private static bool _tooExpensiveWarned;

        /// <summary>
        /// Said once per table when the minimum bet is already more than the bankroll
        /// can carry. Nothing at the table can fix that, so it has to be said plainly
        /// rather than quietly worked around.
        /// </summary>
        static void WarnTooExpensive(long bankroll)
        {
            if (!CardCounter.TableTooExpensive(bankroll, Plugin.BetMin))
            {
                _tooExpensiveWarned = false;
                return;
            }
            if (_tooExpensiveWarned) return;
            _tooExpensiveWarned = true;

            long units = bankroll / Math.Max(1L, Plugin.BetMin);
            long needed = CardCounter.BankrollNeededFor(Plugin.BetMin * 16, 3f);
            string consequence = CardCounter.BankrollCapEnabled
                ? "Bets are held at the minimum, because anything more is over the Kelly stake."
                : "The spread runs anyway - the bankroll limit is switched off in the menu.";
            Plugin.Log.LogWarning(
                $"[BANKROLL] this table is expensive for the bankroll: {bankroll:N0} is {units} table " +
                $"minimums. {consequence} Spreading to 16x here would want about {needed:N0}.");
        }

        static string AdviceToActionStr(string advice)
        {
            switch (advice)
            {
                case "HIT": return "HIT";
                case "STAND": return "STAND";
                case "DOUBLE": return "DOUBLE";
                case "SPLIT": return "SPLIT";
                case "SURRENDER": return "SURRENDER";
                default: return null;
            }
        }

        /// <summary>
        /// Back-counting re-entry: we are standing beside the table without a seat,
        /// still counting. Once the shoe turns favourable, take any free seat again.
        /// Only ever fires after WE gave the seat up - a seat the player left by hand
        /// is none of our business.
        /// </summary>
        public static void PollWongReentry()
        {
            if (!Plugin.AutoBetEnabled || !Plugin.AutoPlayEnabled) return;
            if (!Wong.SitOutIntent || Table == null) return;

            try
            {
                if (Table.LocalSeat != null) return; // already seated again
            }
            catch { return; }

            float tc = Plugin.EffectiveTrueCount();
            bool reliable = Plugin.CountIsReliable();

            // Whether we want a seat is latched, not re-decided every frame. At a full
            // table the wait lasts rounds and the count drifts during it; re-deciding
            // means the seat that frees up at TC -0.4 is skipped, and by the time the
            // count is back over the line someone else is sitting in it.
            if (!Plugin.WongingEnabled)
            {
                // Wonging switched off while we stand outside - get back in, period
                if (Wong.WantSeat())
                    Plugin.Log.LogInfo("[WONG] back-counting switched off - looking for a seat");
            }
            else if (CardCounter.ShouldComeBack(tc, reliable))
            {
                if (Wong.WantSeat())
                    Plugin.Log.LogInfo($"[WONG] shoe turned (TC={tc:+0.0;-0.0;0.0}) - looking for a seat");
            }
            else if (CardCounter.ShouldSitOut(tc, reliable))
            {
                if (Wong.StopSeeking())
                    Plugin.Log.LogInfo($"[WONG] shoe went bad again (TC={tc:+0.0;-0.0;0.0}) - staying out");
            }
            // In between (the dead band) the latch keeps whatever it had.

            if (!Wong.SeekingSeat)
            {
                ReportWaiting($"waiting out the shoe (TC={tc:+0.0;-0.0;0.0}" +
                              (reliable ? ")" : ", count unreliable)"));
                return;
            }

            // Scanning the seat map is a dictionary lookup on data we already hold, so
            // it runs every frame: a seat that frees up has to be ours before the next
            // player clicks Join. Only the sit request itself is rate limited.
            int target = PickFreeSeat();
            TableFull = target < 0;
            if (target < 0)
            {
                ReportWaiting($"TC={tc:+0.0;-0.0;0.0}, no seat free - {SeatMap()}");
                return;
            }

            // A sit request may still be in flight - do not send a second one
            if (UnityEngine.Time.unscaledTime < _nextResitTime) return;
            _nextResitTime = UnityEngine.Time.unscaledTime + 6f;

            Plugin.Log.LogInfo($"[WONG] taking seat {target} (TC={tc:+0.0;-0.0;0.0})");
            try
            {
                Table.Sit(target, delegate(Result result)
                {
                    if (result != null && result.IsSuccess)
                    {
                        Plugin.Log.LogInfo($"[WONG] seat {target} taken");
                        _failedSeats.Clear();
                    }
                    else
                    {
                        // Do not hammer the same seat - take the next one right away
                        _failedSeats.Add(target);
                        _nextResitTime = UnityEngine.Time.unscaledTime + 1f;
                        Plugin.Log.LogWarning($"[WONG] seat {target} refused: {result}");
                    }
                });
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[WONG] sit failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Says once per round why we are still standing outside. A bot that waits
        /// correctly and a bot that is stuck look exactly the same on screen - the
        /// only difference is whether it says so.
        /// </summary>
        private static void ReportWaiting(string why)
        {
            if (_waitLogged) return;
            _waitLogged = true;
            Plugin.Log.LogInfo($"[WONG] outside: {why}");
        }

        /// <summary>
        /// A seat we are allowed to take: unoccupied or abandoned, and not locked.
        /// Our old seat first, since it is the least conspicuous.
        /// </summary>
        private static int PickFreeSeat()
        {
            try
            {
                int max = Table.MaxSeats;
                if (max <= 0) return -1;

                if (IsSeatFree(_lastSeatIndex, max)) return _lastSeatIndex;

                for (int i = 0; i < max; i++)
                    if (IsSeatFree(i, max)) return i;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[WONG] seat search: {ex.Message}");
            }
            return -1;
        }

        /// <summary>
        /// Same rule the game uses for its own join buttons (ShowJoinButtons): a seat is
        /// free when nobody holds it or the holder abandoned it, and it is not locked.
        /// Nothing else - the seat prohibit reasons from the server are not part of it.
        /// A seat that frees up mid-round counts too; the game offers those as well.
        /// </summary>
        private static bool IsSeatFree(int index, int max)
        {
            if (index < 0 || index >= max) return false;
            if (_failedSeats.Contains(index)) return false;
            try
            {
                if (Table.Seats != null && Table.Seats.ContainsKey(index))
                {
                    BJSeat held = Table.Seats[index];
                    if (held != null && held.SeatState != BJSeatState.ABANDONED) return false;
                }
                if (Table.IsSeatLocked(index)) return false;
                return true;
            }
            catch { return false; }
        }

        /// <summary>Seat by seat, so a silent wait is never a mystery again.</summary>
        private static string SeatMap()
        {
            var sb = new System.Text.StringBuilder();
            int max;
            try { max = Table.MaxSeats; } catch { return "seats unreadable"; }
            if (max <= 0) return "table reports no seats";

            for (int i = 0; i < max; i++)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(i).Append("=");
                try
                {
                    if (_failedSeats.Contains(i)) { sb.Append("refused"); continue; }
                    if (Table.IsSeatLocked(i)) { sb.Append("locked"); continue; }
                    if (Table.Seats != null && Table.Seats.ContainsKey(i))
                        sb.Append(Table.Seats[i] != null ? Table.Seats[i].SeatState.ToString() : "taken");
                    else
                        sb.Append("free");
                }
                catch { sb.Append("?"); }
            }
            return sb.ToString();
        }

        private static float _lobbySince;
        private static float _nextRejoinTime;
        private static bool _wasAtTable;
        private static bool _noStageLogged;

        /// <summary>
        /// A table that runs out of players closes, and the game drops you in the lobby.
        /// Take the same stage again through the game's own "new table" path, so the
        /// buy-in checks and the loading screen behave exactly as if you clicked it.
        /// Only ever after we have actually been at a table, so this cannot hijack a
        /// lobby the player went to on purpose before any table was played.
        /// </summary>
        public static void PollAutoRejoin()
        {
            if (!Plugin.AutoRejoinEnabled) return;
            if (string.IsNullOrEmpty(Plugin.LastStageCode)) return;

            UISceneBehaviour scene = null;
            try { scene = UISceneBehaviour.CurrentScene; } catch { }
            if (scene == null) return;

            bool lobby;
            try { lobby = scene.IsLobbyScene; } catch { return; }

            if (!lobby)
            {
                _wasAtTable = true;   // at a table or loading one
                _lobbySince = 0f;
                return;
            }
            if (!_wasAtTable) return;

            try { if (((SceneBehaviour)scene).IsWaiting) { _lobbySince = 0f; return; } }
            catch { return; }

            if (UnityEngine.Time.unscaledTime < _nextRejoinTime) return;

            // Let the lobby settle first - result popups and animations run on arrival
            if (_lobbySince <= 0f)
            {
                _lobbySince = UnityEngine.Time.unscaledTime;
                return;
            }
            if (UnityEngine.Time.unscaledTime - _lobbySince < 5f) return;

            // The table's own stage first; the lobby listing only as a fallback
            BJStage stage = Plugin.LastStage;
            if (stage == null)
            {
                try { stage = BJStage.GetCachedStage(Plugin.LastStageCode); } catch { }
            }
            if (stage == null)
            {
                if (!_noStageLogged)
                {
                    _noStageLogged = true;
                    Plugin.Log.LogWarning($"[REJOIN] stage {Plugin.LastStageCode} is unknown - cannot look for a new table");
                }
                _nextRejoinTime = UnityEngine.Time.unscaledTime + 30f;
                return;
            }

            bool isPrivate;
            try { isPrivate = stage.IsPrivate; } catch { isPrivate = false; }

            if (isPrivate && !CanOpenPrivateTable(stage)) return;

            _noStageLogged = false;
            _lobbySince = 0f;
            _nextRejoinTime = UnityEngine.Time.unscaledTime + 30f; // retry window if it fails

            try
            {
                if (isPrivate)
                {
                    // A private table is not matched into, it is opened. Prefer the lobby's
                    // own template for this stage number, the way the game's own button
                    // does it, and fall back to the stage the finished table carried.
                    BJStage template = null;
                    try { template = BJStage.GetStage(BJStageType.PRIVATE, stage.StageNo); } catch { }

                    Plugin.Log.LogInfo($"[REJOIN] private table ended - opening a new one " +
                        $"(stage {stage.StageNo}, this spends a ticket)");
                    scene.CreateAndMoveToTable(template ?? stage);
                }
                else
                {
                    Plugin.Log.LogInfo($"[REJOIN] table ended - looking for a new one ({Plugin.LastStageCode})");
                    scene.MatchAndMoveToTable(stage);
                }
            }
            catch (Exception ex) { Plugin.Log.LogError($"[REJOIN] failed: {ex.Message}"); }
        }

        /// <summary>
        /// Whether we may open a new private table. Opening one costs a ticket, and a
        /// ticket is bought, won or earned by watching an ad - so it is never spent
        /// without the player having said so, and never silently. Says each reason once;
        /// a bot sitting in the lobby all night must not fill the log either.
        /// </summary>
        private static bool CanOpenPrivateTable(BJStage stage)
        {
            if (!Plugin.AutoRejoinPrivateEnabled)
            {
                RejoinBlocked("private table ended - opening a new one costs a ticket, " +
                              "switch it on in the menu if that is what you want");
                return false;
            }

            try
            {
                if (Singleton<LocalPlayer>.Instance.IsPrivateTableRestricted)
                {
                    RejoinBlocked("private tables are locked on this account");
                    return false;
                }
            }
            catch { }

            long tickets = PrivateTableTickets();
            if (tickets <= 0)
            {
                RejoinBlocked("private table ended but there is no ticket left to open another");
                return false;
            }

            _rejoinBlockedMsg = null;
            return true;
        }

        /// <summary>Tickets in hand, or -1 when the game will not say.</summary>
        public static long PrivateTableTickets()
        {
            try { return Singleton<LocalPlayer>.Instance.GetTotalTickets(); }
            catch { return -1; }
        }

        private static string _rejoinBlockedMsg;

        /// <summary>Each distinct reason once, then silence until it changes.</summary>
        private static void RejoinBlocked(string why)
        {
            _nextRejoinTime = UnityEngine.Time.unscaledTime + 30f;
            if (_rejoinBlockedMsg == why) return;
            _rejoinBlockedMsg = why;
            Plugin.Log.LogInfo($"[REJOIN] {why}");
        }

        /// <summary>
        /// Called from CheatOverlay.Update() every frame when AutoBet + AutoPlay are enabled.
        /// Waits 2 seconds after BET phase starts, then places the recommended bet.
        /// </summary>
        public static void PollAutoBet()
        {
            if (!Plugin.AutoBetEnabled || !Plugin.AutoPlayEnabled) return;
            if (Table == null || Table.Play == null || Table.Play.Turns == null) return;

            // Only act during BET phase
            if (Table.Play.PlayState != BJPlayState.BET)
            {
                _betDelayStart = -1f; // Reset timer when not in BET phase
                return;
            }

            bool single = IsPrivateTable;

            BJTurn betTurn = single ? FindSingleBetTurn() : FindSeatBetTurn();
            if (betTurn == null) return;

            // Already bet this round
            if (Table.Play.PlaySeq == _lastBetPlaySeq) return;

            // Start the delay timer (random 2-4s)
            if (_betDelayStart < 0f)
            {
                _betDelayStart = UnityEngine.Time.unscaledTime;
                _betDelayTarget = RandomBetDelay();
                Plugin.Log.LogInfo($"[AUTOBET] BET phase detected (PlaySeq={Table.Play.PlaySeq}), waiting {_betDelayTarget:0.0}s...");
                return;
            }

            // Wait for delay
            float elapsed = UnityEngine.Time.unscaledTime - _betDelayStart;
            if (elapsed < _betDelayTarget) return;

            // Calculate recommended bet
            float tc = Plugin.EffectiveTrueCount();
            long bankroll = Plugin.Bankroll > 0 ? Plugin.Bankroll : Plugin.Stack;
            long bet = CardCounter.RecommendedBet(tc, Plugin.Stack, Plugin.BetMin, Plugin.BetMax,
                                                  Plugin.BetStep, bankroll);
            if (bet <= 0) bet = Plugin.BetMin;

            WarnTooExpensive(bankroll);

            // ── Wonging: skip the bad shoes instead of feeding them the minimum ──
            Plugin.SitOut sitOut = Plugin.ShouldSitOutNow();
            if (sitOut != Plugin.SitOut.No)
            {
                string why = $"poor shoe (TC={tc:+0.0;-0.0;0.0})";

                WarnPrivateWonging();

                if (SkippedRounds < CardCounter.MaxSkippedRoundsInARow)
                {
                    SkippedRounds++;
                    SessionStats.NoteSitOut();
                    Wong.SkippedBet();                    // expect to lose the seat for this
                    _lastBetPlaySeq = Table.Play.PlaySeq; // this round is handled: by doing nothing
                    _betDelayStart = -1f;
                    Plugin.Log.LogInfo($"[WONG] sitting this round out - {why} " +
                        $"({SkippedRounds}/{CardCounter.MaxSkippedRoundsInARow}) · {Plugin.CountDebug()}");
                    return;
                }

                // Hold the seat: one table minimum, then we may sit out again
                if (Plugin.BetMin > 0) bet = Plugin.BetMin;
                Plugin.Log.LogInfo($"[WONG] forced round of {bet:N0} to hold the seat - {why}");
                SkippedRounds = 0;
            }
            else
            {
                SkippedRounds = 0;
                Wong.PlacedBet();
            }

            _lastBetPlaySeq = Table.Play.PlaySeq;
            _betDelayStart = -1f;

            Plugin.Log.LogInfo($"[AUTOBET] >> BET {bet:N0} (TC={tc:+0.0;-0.0;0.0}, " +
                $"units={CardCounter.SpreadUnits(tc)}) · {Plugin.CountDebug()}");

            try
            {
                if (single) SendSingleBet(betTurn, bet);
                else SendSeatBet(betTurn, bet);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[AUTOBET] Error: {ex.Message}");
                _lastBetPlaySeq = -1; // Allow retry
            }
        }

        static void SendSeatBet(BJTurn betTurn, long bet)
        {
            long placed = bet;
            Table.Action(betTurn, "BET", bet, delegate(Result result)
            {
                if (result.IsSuccess)
                {
                    _lastBetAmount = placed;
                    SessionStats.NoteBet(placed);
                    Plugin.Log.LogInfo($"[AUTOBET] BET {placed:N0} OK");
                }
                else
                    Plugin.Log.LogWarning($"[AUTOBET] BET {placed:N0} FAIL: {result}");
            });
        }

        /// <summary>
        /// Our normal table: one turn per seat, and only our own seat is ours to bet on.
        /// </summary>
        static BJTurn FindSeatBetTurn()
        {
            foreach (var turn in Table.Play.Turns)
            {
                if (turn == null || turn.Seat == null || !turn.Seat.IsLocalSeat) continue;
                string[] valid = GetValidActions(turn);
                if (valid != null && Has(valid, "BET")) return turn;
            }
            return null;
        }

        // ─────────────────── private ("single") tables ───────────────────
        //
        // A private table is the whole table: every seat belongs to the player, and all
        // of them are bet in ONE action carrying an array of amounts indexed by seat.
        // Two consequences, and both of them made the mod do nothing at all there:
        //
        //  - the turns are not attached to a local seat before the first bet, so looking
        //    for a turn whose Seat.IsLocalSeat is true found none, and
        //  - the single-amount BET the game uses everywhere else is not what this table
        //    takes; it wants amountBets, one entry per seat.
        //
        // Both rules below are the game's own, from UISingleBlackjackTable.

        /// <summary>Seat order the game itself offers: middle first, then right, then left.</summary>
        private static readonly int[] SingleSeatOrder = { 1, 2, 0 };

        /// <summary>Seats picked for the current bet, in the order they will be played.</summary>
        private static readonly List<int> _singleSeats = new List<int>(CardCounter.MaxHands);

        /// <summary>
        /// A turn that may be bet on, and the seats to bet on with it. Unlike the normal
        /// table this does not ask whose seat it is - at a private table they are all ours.
        /// </summary>
        static BJTurn FindSingleBetTurn()
        {
            _singleSeats.Clear();
            BJTurn betTurn = null;

            int max;
            try { max = Table.MaxSeats; } catch { return null; }

            int wanted = Plugin.PrivateHands;
            if (wanted < 1) wanted = 1;
            if (wanted > CardCounter.MaxHands) wanted = CardCounter.MaxHands;

            foreach (int seat in SingleSeatOrder)
            {
                if (seat >= max || _singleSeats.Count >= wanted) continue;

                BJTurn[] turns;
                try { turns = Table.Play.GetTurns(seat); }
                catch { continue; }
                if (turns == null) continue;

                foreach (var turn in turns)
                {
                    if (turn == null) continue;
                    bool enabled;
                    try { enabled = turn.GetActionState("BET") == BJActionState.ENABLED; }
                    catch { continue; }
                    if (!enabled) continue;

                    if (betTurn == null) betTurn = turn;
                    _singleSeats.Add(seat);
                    break;
                }
            }

            return _singleSeats.Count > 0 ? betTurn : null;
        }

        /// <summary>
        /// One action, an amount per seat, zero for the seats we are not playing.
        /// </summary>
        static void SendSingleBet(BJTurn betTurn, long oneHandBet)
        {
            // The game checks this before it deals a private round (DealSingleBet), so
            // we check it too rather than letting the server refuse and look like a bug
            long tickets = PrivateTableTickets();
            if (tickets == 0)
            {
                if (!_noTicketLogged)
                {
                    _noTicketLogged = true;
                    Plugin.Log.LogWarning("[AUTOBET] no private table ticket left - the game will not deal");
                }
                _lastBetPlaySeq = -1; // not handled, try again next frame
                return;
            }
            _noTicketLogged = false;

            int hands = _singleSeats.Count;
            long perHand = CardCounter.PerHandBet(oneHandBet, hands, Plugin.BetMin, Plugin.BetStep);

            int max = Table.MaxSeats;
            long[] amounts = new long[max];
            foreach (int seat in _singleSeats)
                if (seat >= 0 && seat < max) amounts[seat] = perHand;

            long total = perHand * hands;
            string seatList = string.Join(",", _singleSeats.ConvertAll(i => i.ToString()).ToArray());

            Table.Action(betTurn, "BET", amounts, delegate(Result result)
            {
                if (result != null && result.IsSuccess)
                {
                    _lastBetAmount = total;
                    SessionStats.NoteBet(total);
                    Plugin.Log.LogInfo($"[AUTOBET] BET {perHand:N0} x{hands} (seats {seatList}) = {total:N0} OK");
                }
                else
                    Plugin.Log.LogWarning($"[AUTOBET] BET {perHand:N0} x{hands} FAIL: {result}");
            });
        }
    }

    // ═══════════════════════════════════════
    // HOOK: every play state change (deal, insurance, result) and
    // every action (hit/double/split cards, dealer draws)
    // ═══════════════════════════════════════
    [HarmonyPatch]
    public static class TableUpdateHook
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(BJTable), "DecodeOnPlayUpdate");
            yield return AccessTools.Method(typeof(BJTable), "DecodeOnAction");
        }

        [HarmonyPostfix]
        static void Postfix(BJTable table)
        {
            try
            {
                // Replays are BJTables too, but their cards aren't from the live shoe
                if (table?.Play == null || table is BJReplayTable) return;
                ActiveTable.Set(table);
                ActiveTable.Refresh(table);
            }
            catch (Exception ex) { Plugin.Log.LogError($"TableUpdateHook: {ex}"); }
        }
    }
}
