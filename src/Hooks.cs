using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Game.Blackjack;
using Game.Common;
using GXF.Base;

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

        // Auto-bet state
        private static int _lastBetPlaySeq = -1;
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
                Plugin.PlayerScore = 0;
                Plugin.PlayerSoft = false;
                Plugin.PlayerPair = false;
                Plugin.DealerUpCard = 0;
            }

            // Shoe first: a reshuffle resets the count before this round's cards are counted
            if (t.Deck != null)
            {
                Plugin.NumDecks = t.Deck.NumDeck;
                if (Plugin.Counter.UpdateShoe(t.Deck.CountAll, t.Deck.CountDealt, t.Deck.CountCut))
                    Plugin.Log.LogInfo($"Shoe reshuffled ({t.Deck.NumDeck} decks), count reset");
            }

            Plugin.PlayState = play.PlayState.ToString();

            // Stack and bet limits for the bet tip
            var seat = t.LocalSeat;
            if (seat != null) Plugin.Stack = seat.Stack;
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
        }

        /// <summary>
        /// Feeds the advisor with the hand being played (after a split: the one in turn)
        /// and the options the server allows for it.
        /// </summary>
        static void UpdateLocalHand(BJPlay play)
        {
            BJTurn turn = LocalTurn(play);
            BJHand hand = turn?.Hand ?? FirstLocalHand(play);
            if (hand?.Cards == null || hand.Cards.Length == 0) return;

            Plugin.PlayerScore = hand.Score;
            Plugin.PlayerSoft = hand.IsAvailableSoft;
            Plugin.PlayerPair = hand.IsPair;

            string[] valid = turn != null ? GetValidActions(turn) : null;
            if (valid != null && (Has(valid, "HIT") || Has(valid, "STAND")))
            {
                Plugin.CanDouble = Has(valid, "DOUBLE");
                Plugin.CanSplit = Has(valid, "SPLIT");
                Plugin.CanSurrender = Has(valid, "SURRENDER");
                _surrenderOffered |= Plugin.CanSurrender;
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

        static BJTurn LocalTurn(BJPlay play)
        {
            if (play.Turns == null) return null;
            foreach (var turn in play.Turns)
                if (turn?.Seat != null && turn.Seat.IsLocalSeat) return turn;
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

                string advice = Plugin.Advisor.GetAdvice(
                    Plugin.PlayerScore, Plugin.DealerUpCard,
                    Plugin.PlayerSoft, Plugin.PlayerPair,
                    Plugin.CanSplit, Plugin.CanDouble, Plugin.CanSurrender);

                string actionStr = AdviceToActionStr(advice);
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
                Plugin.Log.LogInfo($"[AUTO] >> {actionStr} (you={Plugin.PlayerScore} vs D={Plugin.DealerUpCard}, delay={_autoPlayDelayTarget:0.0}s)");

                try
                {
                    Table.Action(turn, actionStr, 0L, delegate(Result result)
                    {
                        if (result.IsSuccess)
                            Plugin.Log.LogInfo($"[AUTO] {actionStr} OK");
                        else
                            Plugin.Log.LogWarning($"[AUTO] {actionStr} FAIL: {result}");
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

                float tc = Plugin.Counter.TrueCount(Plugin.NumDecks);
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

            // Find our local BET turn
            BJTurn betTurn = null;
            foreach (var turn in Table.Play.Turns)
            {
                if (turn == null || turn.Seat == null || !turn.Seat.IsLocalSeat) continue;
                string[] valid = GetValidActions(turn);
                if (valid != null && Has(valid, "BET"))
                {
                    betTurn = turn;
                    break;
                }
            }

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
            float tc = Plugin.Counter.TrueCount(Plugin.NumDecks);
            long bet = CardCounter.RecommendedBet(tc, Plugin.Stack, Plugin.BetMin, Plugin.BetMax, Plugin.BetStep);
            if (bet <= 0) bet = Plugin.BetMin;

            _lastBetPlaySeq = Table.Play.PlaySeq;
            _betDelayStart = -1f;

            Plugin.Log.LogInfo($"[AUTOBET] >> BET {bet:N0} (TC={tc:+0.0;-0.0;0.0}, units={CardCounter.SpreadUnits(tc)})");

            try
            {
                Table.Action(betTurn, "BET", bet, delegate(Result result)
                {
                    if (result.IsSuccess)
                        Plugin.Log.LogInfo($"[AUTOBET] BET {bet:N0} OK");
                    else
                        Plugin.Log.LogWarning($"[AUTOBET] BET {bet:N0} FAIL: {result}");
                });
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[AUTOBET] Error: {ex.Message}");
                _lastBetPlaySeq = -1; // Allow retry
            }
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
