using System;

namespace BJCCheat
{
    /// <summary>
    /// Session bookkeeping, built to answer one question: is the edge working, or
    /// was that just a good run? A chip count alone cannot tell those apart - only
    /// the result measured against the expectation AND the swing can.
    /// </summary>
    public static class SessionStats
    {
        /// <summary>Edge a counted shoe with a 1-16 spread is worth, as a fraction of the amount wagered.</summary>
        public const double AssumedEdge = 0.01;

        /// <summary>
        /// Standard deviation of one blackjack round, in units of the amount wagered.
        /// ~1.15 is the textbook figure once doubles, splits and blackjacks are mixed in.
        /// </summary>
        public const double RoundStdDev = 1.15;

        public static int RoundsPlayed { get; private set; }
        public static int RoundsSatOut { get; private set; }
        public static long TotalWagered { get; private set; }
        public static long NetResult { get; private set; }
        public static long BiggestWin { get; private set; }
        public static long BiggestLoss { get; private set; }
        public static DateTime Started { get; private set; } = DateTime.Now;

        // Sum of squared per-round wagers, for the standard deviation of the whole session
        private static double _sumSquaredWager;

        // Wager accumulating inside the current round (bet, plus double/split)
        private static long _roundWager;

        // Stack when the session started and at the end of the previous round
        private static long _startStack = -1;
        private static long _lastStack = -1;

        // Stack movement that is not winnings (buy-ins, top-ups), kept out of the result
        private static long _externalAdjust;

        /// <summary>Rounds whose stake we could not attribute - they count in the result, not in the edge.</summary>
        public static int RoundsUnattributed { get; private set; }

        public static void Reset()
        {
            RoundsPlayed = 0;
            RoundsSatOut = 0;
            TotalWagered = 0;
            NetResult = 0;
            BiggestWin = 0;
            BiggestLoss = 0;
            _sumSquaredWager = 0;
            _roundWager = 0;
            _startStack = -1;
            _lastStack = -1;
            _externalAdjust = 0;
            RoundsUnattributed = 0;
            Started = DateTime.Now;
        }

        /// <summary>A new table means a new stack - do not read the jump as a win.</summary>
        public static void DropStackBaseline()
        {
            _lastStack = -1; // only the per-round delta, never the session start
        }

        public static void NoteBet(long amount)
        {
            if (amount > 0) _roundWager += amount;
        }

        /// <summary>Double or split: the same amount goes at risk a second time.</summary>
        public static void NoteExtraWager(long amount)
        {
            if (amount > 0) _roundWager += amount;
        }

        public static void NoteSitOut()
        {
            RoundsSatOut++;
        }

        /// <summary>
        /// Called once per finished round with the current stack.
        ///
        /// The result is derived from the stack itself - start, minus buy-ins - and never
        /// from a running sum of per-round deltas. Summing deltas means every round that
        /// gets skipped (unknown stake, buy-in, table change) silently deletes its money
        /// from the total and the figure drifts away from the real balance for good.
        /// Taking the difference from the start cannot drift: whatever happens in between,
        /// the number always matches what the account actually did.
        /// </summary>
        public static void CloseRound(long stackNow)
        {
            long wager = _roundWager;
            _roundWager = 0;

            if (_startStack < 0) _startStack = stackNow;
            if (_lastStack < 0)
            {
                _lastStack = stackNow; // first observation is only a baseline
                return;
            }

            long delta = stackNow - _lastStack;
            _lastStack = stackNow;

            // A jump far beyond what this round could pay is money from outside the game
            if (wager > 0 && Math.Abs(delta) > wager * 20)
            {
                _externalAdjust += delta;
                Plugin.Log.LogInfo($"[STATS] Stack jumped by {delta:N0} on a {wager:N0} wager - "
                    + "looks like a buy-in, not counted as winnings");
            }
            else if (wager > 0)
            {
                RoundsPlayed++;
                TotalWagered += wager;
                _sumSquaredWager += (double)wager * wager;
                if (delta > BiggestWin) BiggestWin = delta;
                if (delta < BiggestLoss) BiggestLoss = delta;
            }
            else if (delta != 0)
            {
                // Money moved without a stake of ours - real, but it cannot go into the
                // edge calculation, so it is only counted
                RoundsUnattributed++;
            }

            NetResult = stackNow - _startStack - _externalAdjust;
        }

        /// <summary>Chips the edge should have produced for this much action.</summary>
        public static double Expected => TotalWagered * AssumedEdge;

        /// <summary>Standard deviation of the session result, in chips.</summary>
        public static double StdDev => RoundStdDev * Math.Sqrt(_sumSquaredWager);

        /// <summary>
        /// How many standard deviations the result sits above or below the expectation.
        /// Roughly: under 1 is noise, over 2 is a genuine outlier.
        /// </summary>
        public static double Sigma
        {
            get
            {
                double sd = StdDev;
                if (sd <= 0) return 0;
                return (NetResult - Expected) / sd;
            }
        }

        /// <summary>Realised return on everything wagered, in percent.</summary>
        public static double RealisedEdgePercent
        {
            get
            {
                if (TotalWagered <= 0) return 0;
                return 100.0 * NetResult / TotalWagered;
            }
        }

        /// <summary>
        /// Plain-language verdict. Deliberately blunt: with a 1% edge it takes tens of
        /// thousands of rounds before the result says anything at all.
        /// </summary>
        public static string Verdict
        {
            get
            {
                if (RoundsPlayed < 50) return Lang.T("verdict_too_few");

                double s = Sigma;
                if (s > 2) return Lang.T("verdict_way_above");
                if (s > 1) return Lang.T("verdict_above");
                if (s < -2) return Lang.T("verdict_way_below");
                if (s < -1) return Lang.T("verdict_below");
                return Lang.T("verdict_normal");
            }
        }

        public static TimeSpan Duration => DateTime.Now - Started;

        public static double RoundsPerHour
        {
            get
            {
                double h = Duration.TotalHours;
                if (h <= 0.0001) return 0;
                return RoundsPlayed / h;
            }
        }

        /// <summary>
        /// Rounds before the edge outgrows the noise. This is a property of blackjack,
        /// not of this session: a 1% edge against a 1.15 swing per round needs
        /// n*edge >= 2*sd*sqrt(n), so n >= (2*sd/edge)^2 - about 53,000 rounds either way.
        /// Rounded off, because printing it to the last round pretends a precision that
        /// the 1% assumption does not have.
        /// </summary>
        public static long RoundsForCertainty
        {
            get
            {
                double n = Math.Pow(2 * RoundStdDev / AssumedEdge, 2);
                return (long)(Math.Round(n / 1000.0) * 1000);
            }
        }

        /// <summary>Hours that many rounds take at the pace measured so far, 0 if unknown.</summary>
        public static double HoursForCertainty
        {
            get
            {
                double perHour = RoundsPerHour;
                if (perHour <= 0) return 0;
                return RoundsForCertainty / perHour;
            }
        }
    }
}
