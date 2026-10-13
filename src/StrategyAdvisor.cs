namespace BJCCheat
{
    /// <summary>
    /// Perfect basic strategy advisor for Blackjack.
    /// Based on standard 4-8 deck, dealer stands on soft 17, double after split, late surrender.
    /// H=Hit, S=Stand, D=Double (else hit), Ds=Double (else stand), R=Surrender (else hit)
    ///
    /// Dealer up card uses RANK (1=A, 2-9, 10=T, 11=J, 12=Q, 13=K).
    /// For strategy table lookup, J/Q/K (11/12/13) are treated same as 10.
    /// Dealer index: 2=0, 3=1, ..., 9=7, 10/J/Q/K=8, A=9
    /// </summary>
    public class StrategyAdvisor
    {
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

        // Pair splits: [pairIndex][dealerIndex]
        // Index: AA=0, 22=1, 33=2, 44=3, 55=4, 66=5, 77=6, 88=7, 99=8, TT=9
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

        /// <summary>
        /// Convert dealer rank (1-13) to strategy table index (0-9).
        /// 2=0, 3=1, ..., 9=7, 10/11/12/13=8, 1(Ace)=9
        /// </summary>
        private int DealerIndex(int rank)
        {
            if (rank == 1) return 9;      // Ace
            if (rank >= 10) return 8;      // 10, J, Q, K all = index 8
            return rank - 2;               // 2=0, 3=1, ..., 9=7
        }

        public string GetAdvice(int playerScore, int dealerUpCard, bool isSoft, bool isPair,
            bool canSplit, bool canDouble, bool canSurrender)
        {
            if (dealerUpCard <= 0 || playerScore <= 0)
                return "WAIT";

            int di = DealerIndex(dealerUpCard);
            if (di < 0 || di > 9) return "WAIT";

            // Check pair split first
            if (isPair && canSplit)
            {
                int pairRank = GetPairRank(playerScore, isSoft);
                if (pairRank >= 1 && pairRank <= 13)
                {
                    int pi;
                    if (pairRank == 1) pi = 0;           // AA
                    else if (pairRank >= 10) pi = 9;      // TT/JJ/QQ/KK
                    else pi = pairRank - 1;               // 22=1, 33=2, ..., 99=8

                    if (pi >= 0 && pi <= 9 && PairStrategy[pi, di] == "Y")
                        return "SPLIT";
                }
            }

            // Soft hand
            if (isSoft && playerScore >= 13 && playerScore <= 21)
            {
                int si = playerScore - 13;
                if (si >= 0 && si < 9)
                {
                    string adv = SoftStrategy[si, di];
                    return ResolveAction(adv, canDouble, canSurrender);
                }
            }

            // Hard hand
            if (playerScore >= 5 && playerScore <= 21)
            {
                int hi = playerScore - 5;
                if (hi >= 0 && hi < 17)
                {
                    string adv = HardStrategy[hi, di];
                    return ResolveAction(adv, canDouble, canSurrender);
                }
            }

            // Fallback
            if (playerScore < 5) return "HIT";
            return playerScore >= 17 ? "STAND" : "HIT";
        }

        /// <summary>
        /// Insurance (and even money) only pays off when the rest of the shoe is rich in tens:
        /// the Hi-Lo index for taking it is true count +3, otherwise never.
        /// </summary>
        public const float InsuranceTrueCount = 3f;

        public bool ShouldInsure(float trueCount)
        {
            return trueCount >= InsuranceTrueCount;
        }

        private string ResolveAction(string adv, bool canDouble, bool canSurrender)
        {
            if (adv == "D" && !canDouble) return "HIT";
            if (adv == "Ds" && !canDouble) return "STAND";
            if (adv == "R" && !canSurrender) return "HIT";
            switch (adv)
            {
                case "H": return "HIT";
                case "S": return "STAND";
                case "D":
                case "Ds": return "DOUBLE";
                case "R": return "SURRENDER";
                default: return "HIT";
            }
        }

        private int GetPairRank(int score, bool soft)
        {
            // AA = score 12 soft (A+A = 2 hard, 12 soft)
            if (soft && score == 12) return 1;
            if (score % 2 != 0) return 0; // not a pair
            int rank = score / 2;
            return rank;
        }
    }
}
