namespace BJCCheat
{
    /// <summary>
    /// What to do, and why. Reason is shown in the overlay so a deviation from
    /// basic strategy is never a silent surprise.
    /// </summary>
    public struct Advice
    {
        public string Action;
        public string Reason;
        public bool IsDeviation;

        public Advice(string action, string reason = null, bool deviation = false)
        {
            Action = action;
            Reason = reason;
            IsDeviation = deviation;
        }
    }

    /// <summary>
    /// Basic strategy for 4-8 decks, DAS, late surrender, with
    ///  - an H17 overlay (applied once the dealer is seen hitting soft 17), and
    ///  - the Hi-Lo count deviations (Illustrious 18 + Fab 4).
    ///
    /// The base tables are the S17 charts. H17 differences and count deviations are
    /// kept as explicit overrides on top, so every departure stays auditable.
    ///
    /// H=Hit, S=Stand, D=Double (else hit), Ds=Double (else stand), R=Surrender (else hit)
    /// Dealer up card uses RANK (1=A, 2-9, 10=T, 11=J, 12=Q, 13=K).
    /// Dealer index: 2=0, 3=1, ..., 9=7, 10/J/Q/K=8, A=9
    /// </summary>
    public class StrategyAdvisor
    {
        /// <summary>Splitting tens is correct at a high count but very conspicuous. Off by default.</summary>
        public bool AggressiveSplits = false;

        public const float InsuranceTrueCount = 3f;

        // Hard totals: [playerTotal - 5][dealerIndex]
        private static readonly string[,] HardStrategy = {
            //           2    3    4    5    6    7    8    9   T/J/Q/K  A
            /* 5  */  { "H", "H", "H", "H", "H", "H", "H", "H", "H", "H" },
            /* 6  */  { "H", "H", "H", "H", "H", "H", "H", "H", "H", "H" },
            /* 7  */  { "H", "H", "H", "H", "H", "H", "H", "H", "H", "H" },
            /* 8  */  { "H", "H", "H", "H", "H", "H", "H", "H", "H", "H" },
            /* 9  */  { "H", "D", "D", "D", "D", "H", "H", "H", "H", "H" },
            /* 10 */  { "D", "D", "D", "D", "D", "D", "D", "D", "H", "H" },
            /* 11 */  { "D", "D", "D", "D", "D", "D", "D", "D", "D", "H" },
            /* 12 */  { "H", "H", "S", "S", "S", "H", "H", "H", "H", "H" },
            /* 13 */  { "S", "S", "S", "S", "S", "H", "H", "H", "H", "H" },
            /* 14 */  { "S", "S", "S", "S", "S", "H", "H", "H", "H", "H" },
            /* 15 */  { "S", "S", "S", "S", "S", "H", "H", "H", "R", "H" },
            /* 16 */  { "S", "S", "S", "S", "S", "H", "H", "R", "R", "R" },
            /* 17 */  { "S", "S", "S", "S", "S", "S", "S", "S", "S", "S" },
            /* 18 */  { "S", "S", "S", "S", "S", "S", "S", "S", "S", "S" },
            /* 19 */  { "S", "S", "S", "S", "S", "S", "S", "S", "S", "S" },
            /* 20 */  { "S", "S", "S", "S", "S", "S", "S", "S", "S", "S" },
            /* 21 */  { "S", "S", "S", "S", "S", "S", "S", "S", "S", "S" },
        };

        // Soft totals: [playerTotal - 13][dealerIndex]
        private static readonly string[,] SoftStrategy = {
            //           2    3    4    5    6    7    8    9   T/J/Q/K  A
            /* S13 */  { "H", "H", "H", "D", "D", "H", "H", "H", "H", "H" },
            /* S14 */  { "H", "H", "H", "D", "D", "H", "H", "H", "H", "H" },
            /* S15 */  { "H", "H", "D", "D", "D", "H", "H", "H", "H", "H" },
            /* S16 */  { "H", "H", "D", "D", "D", "H", "H", "H", "H", "H" },
            /* S17 */  { "H", "D", "D", "D", "D", "H", "H", "H", "H", "H" },
            /* S18 */  { "S", "Ds", "Ds", "Ds", "Ds", "S", "S", "H", "H", "H" },
            /* S19 */  { "S", "S", "S", "S", "S", "S", "S", "S", "S", "S" },
            /* S20 */  { "S", "S", "S", "S", "S", "S", "S", "S", "S", "S" },
            /* S21 */  { "S", "S", "S", "S", "S", "S", "S", "S", "S", "S" },
        };

        // Pair splits: AA=0, 22=1, 33=2, 44=3, 55=4, 66=5, 77=6, 88=7, 99=8, TT=9
        private static readonly string[,] PairStrategy = {
            //           2    3    4    5    6    7    8    9   T/J/Q/K  A
            /* AA */   { "Y", "Y", "Y", "Y", "Y", "Y", "Y", "Y", "Y", "Y" },
            /* 22 */   { "Y", "Y", "Y", "Y", "Y", "Y", "N", "N", "N", "N" },
            /* 33 */   { "Y", "Y", "Y", "Y", "Y", "Y", "N", "N", "N", "N" },
            /* 44 */   { "N", "N", "N", "Y", "Y", "N", "N", "N", "N", "N" },
            /* 55 */   { "N", "N", "N", "N", "N", "N", "N", "N", "N", "N" },
            /* 66 */   { "Y", "Y", "Y", "Y", "Y", "N", "N", "N", "N", "N" },
            /* 77 */   { "Y", "Y", "Y", "Y", "Y", "Y", "N", "N", "N", "N" },
            /* 88 */   { "Y", "Y", "Y", "Y", "Y", "Y", "Y", "Y", "Y", "Y" },
            /* 99 */   { "Y", "Y", "Y", "Y", "Y", "N", "Y", "Y", "N", "N" },
            /* TT */   { "N", "N", "N", "N", "N", "N", "N", "N", "N", "N" },
        };

        private static int DealerIndex(int rank)
        {
            if (rank == 1) return 9;   // Ace
            if (rank >= 10) return 8;  // 10, J, Q, K
            return rank - 2;
        }

        /// <summary>Dealer card as a blackjack value: 2..10, Ace = 11.</summary>
        private static int DealerValue(int rank)
        {
            if (rank == 1) return 11;
            if (rank >= 10) return 10;
            return rank;
        }

        public bool ShouldInsure(float trueCount)
        {
            return trueCount >= InsuranceTrueCount;
        }

        /// <summary>Old call sites: basic strategy, S17, no count.</summary>
        public string GetAdvice(int playerScore, int dealerUpCard, bool isSoft, bool isPair,
            bool canSplit, bool canDouble, bool canSurrender)
        {
            return Advise(playerScore, dealerUpCard, isSoft, isPair,
                canSplit, canDouble, canSurrender, 0f, null).Action;
        }

        public Advice Advise(int playerScore, int dealerUpCard, bool isSoft, bool isPair,
            bool canSplit, bool canDouble, bool canSurrender, float trueCount, RuleSet rules)
        {
            if (dealerUpCard <= 0 || playerScore <= 0)
                return new Advice("WAIT");

            // Over 21 the hand is finished and there is nothing to advise. Without this
            // the lookup falls past the end of the chart into "17 or more stands", and
            // the overlay recommends standing on 23 with a straight face.
            if (playerScore > 21)
                return new Advice("BUST");

            int di = DealerIndex(dealerUpCard);
            if (di < 0 || di > 9) return new Advice("WAIT");

            int dv = DealerValue(dealerUpCard);
            bool h17 = rules != null && rules.H17;

            // ── Pairs ──
            if (isPair && canSplit)
            {
                int pi = PairIndex(GetPairRank(playerScore, isSoft));
                if (pi >= 0)
                {
                    // H17: 8,8 vs A is a surrender when the table allows it
                    if (h17 && pi == 7 && dv == 11 && canSurrender)
                        return new Advice("SURRENDER", Lang.T("h17_surrender", "8,8", Lang.T("card_ace")), true);

                    if (AggressiveSplits && pi == 9)
                    {
                        if (dv == 5 && trueCount >= 5f)
                            return new Advice("SPLIT", Lang.T("dev_split", 10, 5, 5f), true);
                        if (dv == 6 && trueCount >= 4f)
                            return new Advice("SPLIT", Lang.T("dev_split", 10, 6, 4f), true);
                    }

                    if (PairStrategy[pi, di] == "Y")
                        return new Advice("SPLIT");
                }
            }

            // ── Soft hands ──
            if (isSoft && playerScore >= 13 && playerScore <= 21)
            {
                int si = playerScore - 13;
                if (si >= 0 && si < 9)
                {
                    string adv = SoftStrategy[si, di];
                    string reason = null;

                    if (h17)
                    {
                        if (playerScore == 17 && dv == 2) { adv = "D"; reason = Lang.T("h17_double", "A,6", 2); }
                        else if (playerScore == 18 && dv == 2) { adv = "Ds"; reason = Lang.T("h17_double", "A,7", 2); }
                        else if (playerScore == 19 && dv == 6) { adv = "Ds"; reason = Lang.T("h17_double", "A,8", 6); }
                    }

                    return Resolve(adv, canDouble, canSurrender, reason, reason != null);
                }
            }

            // ── Hard hands ──
            if (playerScore >= 5 && playerScore <= 21)
            {
                int hi = playerScore - 5;
                if (hi >= 0 && hi < 17)
                {
                    Advice? dev = HardDeviation(playerScore, dv, trueCount, canDouble, canSurrender);
                    if (dev != null) return dev.Value;

                    string adv = HardStrategy[hi, di];
                    string reason = null;
                    bool deviation = false;

                    if (h17)
                    {
                        if (playerScore == 11 && dv == 11) { adv = "D"; reason = Lang.T("h17_double", 11, Lang.T("card_ace")); deviation = true; }
                        else if (playerScore == 15 && dv == 11) { adv = "R"; reason = Lang.T("h17_surrender", 15, Lang.T("card_ace")); deviation = true; }
                        else if (playerScore == 17 && dv == 11) { adv = "R"; reason = Lang.T("h17_surrender", 17, Lang.T("card_ace")); deviation = true; }
                    }

                    return Resolve(adv, canDouble, canSurrender, reason, deviation);
                }
            }

            if (playerScore < 5) return new Advice("HIT");
            return new Advice(playerScore >= 17 ? "STAND" : "HIT");
        }

        /// <summary>
        /// Hi-Lo index plays for hard totals (Illustrious 18 + Fab 4 surrenders).
        /// Returns null when the count does not change the basic-strategy call.
        /// </summary>
        private Advice? HardDeviation(int total, int dv, float tc, bool canDouble, bool canSurrender)
        {
            // Fab 4 - these refine surrender decisions, so they come first
            if (canSurrender)
            {
                if (total == 14 && dv == 10 && tc >= 3f)
                    return new Advice("SURRENDER", Lang.T("dev_surrender", 14, 10, 3f), true);
                if (total == 15 && dv == 9 && tc >= 2f)
                    return new Advice("SURRENDER", Lang.T("dev_surrender", 15, 9, 2f), true);
                if (total == 15 && dv == 11 && tc >= 1f)
                    return new Advice("SURRENDER", Lang.T("dev_surrender", 15, Lang.T("card_ace"), 1f), true);
                if (total == 15 && dv == 10)
                    return tc >= 0f
                        ? new Advice("SURRENDER", Lang.T("dev_surrender", 15, 10, 0f), true)
                        : new Advice("HIT", Lang.T("dev_hit_below", 15, 10, 0f), true);
            }
            else
            {
                // Illustrious 18 stands, only relevant when we cannot surrender the hand
                if (total == 16 && dv == 10 && tc >= 0f)
                    return new Advice("STAND", Lang.T("dev_stand", 16, 10, 0f), true);
                if (total == 16 && dv == 9 && tc >= 5f)
                    return new Advice("STAND", Lang.T("dev_stand", 16, 9, 5f), true);
                if (total == 15 && dv == 10 && tc >= 4f)
                    return new Advice("STAND", Lang.T("dev_stand", 15, 10, 4f), true);
            }

            if (total == 12 && dv == 2 && tc >= 3f)
                return new Advice("STAND", Lang.T("dev_stand", 12, 2, 3f), true);
            if (total == 12 && dv == 3 && tc >= 2f)
                return new Advice("STAND", Lang.T("dev_stand", 12, 3, 2f), true);
            if (total == 12 && dv == 4 && tc < 0f)
                return new Advice("HIT", Lang.T("dev_hit_below", 12, 4, 0f), true);
            if (total == 12 && dv == 5 && tc <= -2f)
                return new Advice("HIT", Lang.T("dev_hit", 12, 5, -2f), true);
            if (total == 12 && dv == 6 && tc <= -1f)
                return new Advice("HIT", Lang.T("dev_hit", 12, 6, -1f), true);
            if (total == 13 && dv == 2 && tc <= -1f)
                return new Advice("HIT", Lang.T("dev_hit", 13, 2, -1f), true);
            if (total == 13 && dv == 3 && tc <= -2f)
                return new Advice("HIT", Lang.T("dev_hit", 13, 3, -2f), true);

            // Doubles that only pay off in a ten-rich shoe
            if (canDouble)
            {
                if (total == 9 && dv == 2 && tc >= 1f)
                    return new Advice("DOUBLE", Lang.T("dev_double", 9, 2, 1f), true);
                if (total == 9 && dv == 7 && tc >= 3f)
                    return new Advice("DOUBLE", Lang.T("dev_double", 9, 7, 3f), true);
                if (total == 10 && dv == 10 && tc >= 4f)
                    return new Advice("DOUBLE", Lang.T("dev_double", 10, 10, 4f), true);
                if (total == 10 && dv == 11 && tc >= 4f)
                    return new Advice("DOUBLE", Lang.T("dev_double", 10, Lang.T("card_ace"), 4f), true);
                if (total == 11 && dv == 11 && tc >= 1f)
                    return new Advice("DOUBLE", Lang.T("dev_double", 11, Lang.T("card_ace"), 1f), true);
            }

            return null;
        }

        private Advice Resolve(string adv, bool canDouble, bool canSurrender, string reason, bool deviation)
        {
            if (adv == "D" && !canDouble) return new Advice("HIT", reason, deviation);
            if (adv == "Ds" && !canDouble) return new Advice("STAND", reason, deviation);
            if (adv == "R" && !canSurrender) return new Advice("HIT", reason, deviation);
            switch (adv)
            {
                case "H": return new Advice("HIT", reason, deviation);
                case "S": return new Advice("STAND", reason, deviation);
                case "D":
                case "Ds": return new Advice("DOUBLE", reason, deviation);
                case "R": return new Advice("SURRENDER", reason, deviation);
                default: return new Advice("HIT", reason, deviation);
            }
        }

        private static int PairIndex(int pairRank)
        {
            if (pairRank == 1) return 0;    // AA
            if (pairRank >= 10) return 9;   // TT/JJ/QQ/KK
            if (pairRank >= 2 && pairRank <= 9) return pairRank - 1;
            return -1;
        }

        private int GetPairRank(int score, bool soft)
        {
            if (soft && score == 12) return 1; // A,A
            if (score % 2 != 0) return 0;
            return score / 2;
        }
    }
}
