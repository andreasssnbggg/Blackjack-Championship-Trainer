using System;
using System.Collections.Generic;
using Game.Common;

namespace BJCCheat
{
    /// <summary>
    /// Hi-Lo card counting system.
    /// 2-6 = +1, 7-9 = 0, 10-A = -1
    /// </summary>
    public class CardCounter
    {
        public int RunningCount { get; private set; }
        public int CardsSeen { get; private set; }

        // Shoe info from the server (0 = unknown, then we estimate from CardsSeen)
        public int ShoeCards { get; private set; }
        public int CardsDealt { get; private set; }
        public int CutCard { get; private set; }

        // Track exact card counts for advanced stats
        private int[] _rankCounts = new int[14]; // index 1-13

        // How often each card (e.g. "Kc") has been counted in the current round
        private readonly Dictionary<string, int> _countedThisRound = new Dictionary<string, int>();

        public void Reset()
        {
            RunningCount = 0;
            CardsSeen = 0;
            _rankCounts = new int[14];
            _countedThisRound.Clear();
        }

        public void NewRound()
        {
            _countedThisRound.Clear();
        }

        /// <summary>
        /// Counts the cards on the table that weren't counted yet this round. Works on how often
        /// each card occurs, so repeated updates, identical cards from different decks and cards
        /// moving to a new hand on a split are each counted exactly once.
        /// </summary>
        public void CountVisible(List<Card> cards)
        {
            var occurrences = new Dictionary<string, int>();
            foreach (var c in cards)
            {
                string key = c.ToString();
                occurrences.TryGetValue(key, out int n);
                occurrences[key] = ++n;

                _countedThisRound.TryGetValue(key, out int counted);
                if (n > counted)
                {
                    _countedThisRound[key] = n;
                    CountCard(c.Rank);
                }
            }
        }

        /// <summary>
        /// Updates the shoe info. Returns true when the shoe was reshuffled
        /// (dealt count went back down), which resets the count.
        /// </summary>
        public bool UpdateShoe(int countAll, int countDealt, int countCut)
        {
            bool shuffled = countDealt < CardsDealt;
            if (shuffled) Reset();
            ShoeCards = countAll;
            CardsDealt = countDealt;
            CutCard = countCut;
            return shuffled;
        }

        /// <summary>
        /// Count a card by its rank (1=Ace, 2-9, 10=T, 11=J, 12=Q, 13=K)
        /// </summary>
        public void CountCard(int rank)
        {
            if (rank < 1 || rank > 13) return;

            CardsSeen++;
            _rankCounts[rank]++;

            // Hi-Lo value
            int bjValue = rank >= 10 ? 10 : rank; // J/Q/K = 10
            if (rank == 1) bjValue = 11; // Ace

            if (bjValue >= 2 && bjValue <= 6)
                RunningCount++;
            else if (bjValue >= 10) // 10, J, Q, K, A
                RunningCount--;
            // 7, 8, 9 = 0
        }

        /// <summary>
        /// True count = running count / decks remaining, rounded to one decimal.
        ///
        /// The rounding is deliberate: every threshold in this plugin is written with one
        /// decimal, and the overlay shows one decimal. Deciding on the raw value meant a
        /// displayed "+2,0" could really be 1,97 and miss the "4x from +2" step - the
        /// number you read and the number that acts have to be the same one. The lost
        /// precision is meaningless anyway next to the uncertainty in the deck estimate.
        /// </summary>
        public float TrueCount(int totalDecks)
        {
            float remaining = DecksRemaining(totalDecks);
            if (remaining < 0.5f) remaining = 0.5f;
            return (float)Math.Round(RunningCount / remaining, 1);
        }

        public float DecksRemaining(int totalDecks)
        {
            int remaining = ShoeCards > 0
                ? ShoeCards - CardsDealt
                : totalDecks * 52 - CardsSeen;
            return remaining / 52f;
        }

        /// <summary>
        /// Rough player edge in percent: about -0.5% off the top, +0.5% per true count.
        /// </summary>
        public float PlayerEdgePercent(float trueCount)
        {
            return 0.5f * trueCount - 0.5f;
        }

        /// <summary>
        /// How far the shoe is played until the reshuffle (0..1), or -1 if unknown.
        /// </summary>
        public float ShoeProgress
        {
            get
            {
                if (ShoeCards <= 0 || CutCard <= 0) return -1f;
                // countCut is either the cut card's position or the cards behind it;
                // penetration is always more than half the shoe, so take the larger one
                int cutPos = Math.Max(CutCard, ShoeCards - CutCard);
                return Math.Min(1f, (float)CardsDealt / cutPos);
            }
        }

        /// <summary>
        /// Joined mid-shoe: most dealt cards were never seen, so the count is unreliable until the next shuffle.
        /// </summary>
        public bool JoinedMidShoe => ShoeCards > 0 && CardsDealt > 52 && CardsSeen < CardsDealt / 2;

        public const float MaxBetTrueCount = 4f;

        /// <summary>
        /// Wonging: below this true count the shoe belongs to the house, so the best
        /// bet is no bet. Sitting out the bad shoes is worth more than any chart detail.
        /// </summary>
        public const float WongOutTrueCount = -1f;

        /// <summary>
        /// Come back to the table from this true count up. Deliberately higher than
        /// WongOutTrueCount: without that dead band we would leave at -1.1 and sit back
        /// down at -0.9, round after round.
        /// </summary>
        public const float WongInTrueCount = 0f;

        /// <summary>
        /// Fallback for tables that tolerate a missed bet. This game does not - it
        /// stands you up after a single one - so in practice the seat is gone first
        /// and we come back through ShouldComeBack instead.
        /// </summary>
        public const int MaxSkippedRoundsInARow = 4;

        /// <summary>Pure threshold test on a count we trust.</summary>
        public static bool ShouldSitOut(float trueCount)
        {
            return trueCount < WongOutTrueCount;
        }

        /// <summary>
        /// The full wonging rule. An untrustworthy count is itself a reason to sit out -
        /// joining mid-shoe is the classic "wait for the shuffle" situation, and playing
        /// on through it was exactly the bug that made this feature look dead.
        /// </summary>
        public static bool ShouldSitOut(float trueCount, bool countReliable)
        {
            if (!countReliable) return true;
            return ShouldSitOut(trueCount);
        }

        /// <summary>
        /// Worth taking a seat again? Needs a count we trust AND one that is actually
        /// favourable - not merely "no longer terrible".
        /// </summary>
        public static bool ShouldComeBack(float trueCount, bool countReliable)
        {
            return countReliable && trueCount >= WongInTrueCount;
        }

        /// <summary>
        /// Bet spread in table minimums: 1 / 2 / 4 / 8 / 16, table maximum from +4
        /// (see RecommendedBet). The old ramp stayed at 1.5x until true count +2 and then
        /// jumped straight to 8x, so the whole +1..+2 band - where the edge is already
        /// positive - was played at almost the minimum.
        /// </summary>
        public static float SpreadUnits(float trueCount)
        {
            if (trueCount >= 3f) return 16f;
            if (trueCount >= 2.5f) return 8f;
            if (trueCount >= 2f) return 4f;
            if (trueCount >= 1f) return 2f;
            return 1f;
        }

        /// <summary>Variance of one unit wagered at blackjack (standard deviation 1.15).</summary>
        public const double VariancePerUnit = 1.32;

        /// <summary>
        /// Share of the full Kelly stake actually bet. Full Kelly maximises long-run
        /// growth and is far too wild to sit through; half is the usual working choice
        /// and quarters the swings for three quarters of the growth.
        /// </summary>
        public const double KellyFraction = 0.5;

        /// <summary>
        /// The largest bet the bankroll justifies at this count: half of the Kelly
        /// stake, which is edge divided by variance, times the bankroll.
        ///
        /// This is the number the bet ladder was missing. A spread written in table
        /// minimums knows the table and nothing about the player - 16x the minimum is
        /// a sound bet on a bankroll of a thousand units and certain ruin on sixty,
        /// and the ladder cannot tell those apart. Everything else in this plugin can
        /// only improve the edge by fractions of a percent; betting more than the
        /// bankroll carries loses all of it, and no edge survives that.
        ///
        /// Zero when there is no edge to bet into - then only the table minimum is
        /// defensible, because it is the price of being dealt in at all.
        /// </summary>
        public static long MaxDefensibleBet(long bankroll, float trueCount)
        {
            if (bankroll <= 0) return 0;

            double edge = PlayerEdgeFraction(trueCount);
            if (edge <= 0) return 0;

            return (long)(bankroll * KellyFraction * edge / VariancePerUnit);
        }

        /// <summary>Player edge as a fraction (0.01 = 1 %), the percent figure over 100.</summary>
        public static double PlayerEdgeFraction(float trueCount)
        {
            return (0.5 * trueCount - 0.5) / 100.0;
        }

        /// <summary>
        /// Bankroll needed before a given bet is defensible at this count - the inverse
        /// of MaxDefensibleBet, so the overlay can say what the table actually asks for.
        /// Zero when there is no edge, since no bankroll makes a negative bet right.
        /// </summary>
        public static long BankrollNeededFor(long bet, float trueCount)
        {
            double edge = PlayerEdgeFraction(trueCount);
            if (edge <= 0 || bet <= 0) return 0;
            return (long)(bet * VariancePerUnit / (KellyFraction * edge));
        }

        /// <summary>
        /// Spread bet, kept within the table limits, the bankroll and the stack.
        /// </summary>
        public static long RecommendedBet(float trueCount, long stack, long min, long max, long step)
        {
            return RecommendedBet(trueCount, stack, min, max, step, stack);
        }

        /// <summary>
        /// As above, with the bankroll given separately from the chips on the table.
        /// The bankroll is what the bet has to be measured against; the stack is only
        /// what happens to be in front of us this minute.
        /// </summary>
        public static long RecommendedBet(float trueCount, long stack, long min, long max, long step, long bankroll)
        {
            long bet = trueCount >= MaxBetTrueCount && max > 0 ? max : (long)(min * SpreadUnits(trueCount));
            if (max > 0) bet = Math.Min(bet, max);

            // Bankroll before anything else. The table minimum survives the cap: it is
            // not a choice about how much to risk, it is the cost of playing this table
            // at all - and when it sits above the cap, the answer is a cheaper table,
            // which TableTooExpensive says out loud instead of quietly over-betting.
            long cap = MaxDefensibleBet(bankroll, trueCount);
            bet = Math.Min(bet, Math.Max(cap, min));

            if (stack > 0) bet = Math.Min(bet, stack);
            if (step > 0) bet -= bet % step;
            return Math.Max(bet, min);
        }

        /// <summary>
        /// This table costs more per round than the bankroll can carry even at a good
        /// count: the minimum alone is over the Kelly stake. Nothing can be done about
        /// it at the table - it is the wrong table for the money.
        /// </summary>
        public static bool TableTooExpensive(long bankroll, long min)
        {
            if (bankroll <= 0 || min <= 0) return false;
            // Measured at a clearly favourable count, so this says "even at its best"
            return MaxDefensibleBet(bankroll, 3f) < min;
        }

        /// <summary>
        /// How many hands may be played at once on a private table.
        /// </summary>
        public const int MaxHands = 3;

        /// <summary>
        /// Risk-equivalent bet per hand when several hands are played in the same round.
        ///
        /// Hands dealt from one shoe against one dealer up card are correlated, so N hands
        /// of the full bet are not N independent bets - they are closer to one bet of
        /// N times the size, and the bankroll swings accordingly. The published
        /// equivalence factors (Schlesinger, Blackjack Attack) put two hands at about
        /// 73 % and three at about 60 % of the one-hand bet for the same risk of ruin.
        /// Slightly more money goes out per round that way (1.46x and 1.80x of a single
        /// bet), which is the point: more hands per shoe at the same risk.
        /// </summary>
        public static long PerHandBet(long oneHandBet, int hands, long min, long step)
        {
            if (hands > MaxHands) hands = MaxHands;

            long bet = oneHandBet;
            if (hands > 1)
            {
                double factor = hands == 2 ? 0.73 : 0.60;
                bet = (long)(oneHandBet * factor);
            }

            // Applied for one hand too, not only for the scaled-down ones: a function
            // handed a minimum and a step has to return a bet that respects them
            // whatever the count of hands, or the single-hand case quietly skips the
            // only two rules the table actually enforces.
            if (step > 0) bet -= bet % step;
            if (min > 0 && bet < min) bet = min;
            return bet;
        }

        public int GetRankCount(int rank)
        {
            if (rank < 1 || rank > 13) return 0;
            return _rankCounts[rank];
        }

        /// <summary>
        /// Remaining cards of a given rank
        /// </summary>
        public int RemainingOfRank(int rank, int totalDecks)
        {
            if (rank < 1 || rank > 13) return 0;
            int perDeck = (rank >= 10) ? 4 : 4; // each rank has 4 per deck
            return (totalDecks * perDeck) - _rankCounts[rank];
        }
    }
}
