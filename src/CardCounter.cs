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

        /// <summary>
        /// Spread bet, kept within the table limits and the stack.
        /// </summary>
        public static long RecommendedBet(float trueCount, long stack, long min, long max, long step)
        {
            long bet = trueCount >= MaxBetTrueCount && max > 0 ? max : (long)(min * SpreadUnits(trueCount));
            if (max > 0) bet = Math.Min(bet, max);
            if (stack > 0) bet = Math.Min(bet, stack);
            if (step > 0) bet -= bet % step;
            return Math.Max(bet, min);
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
