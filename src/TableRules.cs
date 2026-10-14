using System;
using System.Collections.Generic;
using Game.Common;

namespace BJCCheat
{
    /// <summary>
    /// The table rules we need for correct strategy. The server never tells us these
    /// directly, so everything except the deck count is learned by watching rounds.
    /// </summary>
    public class RuleSet
    {
        public int NumDecks = 6;

        /// <summary>Surrender appeared in ValidActions at least once.</summary>
        public bool SurrenderOffered;

        /// <summary>Double after split seen at least once.</summary>
        public bool DoubleAfterSplit;

        /// <summary>null = not observed yet. Changes ~8 cells of basic strategy.</summary>
        public bool? DealerHitsSoft17;

        public bool H17 => DealerHitsSoft17 == true;

        public string DealerRuleText
        {
            get
            {
                if (DealerHitsSoft17 == null) return Lang.T("rule_unknown");
                return Lang.T(DealerHitsSoft17.Value ? "rule_h17" : "rule_s17");
            }
        }
    }

    /// <summary>
    /// Learns the dealer's soft-17 rule from finished rounds.
    ///
    /// Walk the dealer's final hand card by card. If the hand passes through exactly
    /// soft 17 and another card follows, the dealer hit a soft 17 - that is only legal
    /// under H17, so a single observation settles it. The opposite case (hand ENDS on
    /// soft 17) is weaker evidence: in a round where every player busted the dealer
    /// never draws at all, so we require several sightings before calling it S17.
    /// </summary>
    public static class RuleDetector
    {
        private const int S17EvidenceNeeded = 3;
        private static int _s17Evidence;

        public static void Reset()
        {
            _s17Evidence = 0;
        }

        private static int Value(int rank)
        {
            if (rank >= 10) return 10;
            return rank;
        }

        /// <param name="dealerHadToPlay">
        /// False when no player hand survived - the dealer may then stop early and
        /// any "stood on soft 17" reading would be meaningless.
        /// </param>
        public static void ObserveDealerHand(IList<Card> cards, bool dealerHadToPlay, RuleSet rules)
        {
            if (rules == null || cards == null || cards.Count < 2) return;
            if (rules.DealerHitsSoft17 != null) return; // already known

            int total = 0;
            int aces = 0;
            int softSeventeenAt = -1;

            for (int i = 0; i < cards.Count; i++)
            {
                Card c = cards[i];
                if (c == null || !c.IsValid) return; // hole card still hidden - round not over

                int v = Value(c.Rank);
                if (c.Rank == 1) { aces++; v = 11; }
                total += v;
                while (total > 21 && aces > 0) { total -= 10; aces--; }

                if (aces > 0 && total == 17 && softSeventeenAt < 0)
                    softSeventeenAt = i;
            }

            if (softSeventeenAt < 0)
            {
                // A finished dealer hand below 17 means the dealer never drew -
                // that round tells us nothing either way.
                return;
            }

            if (softSeventeenAt < cards.Count - 1)
            {
                rules.DealerHitsSoft17 = true;
                Plugin.Log.LogWarning("[RULES] Dealer hits soft 17 (H17) - strategy switched");
                return;
            }

            if (!dealerHadToPlay) return;

            _s17Evidence++;
            if (_s17Evidence >= S17EvidenceNeeded)
            {
                rules.DealerHitsSoft17 = false;
                Plugin.Log.LogWarning("[RULES] Dealer stands on soft 17 (S17) - strategy confirmed");
            }
        }
    }
}
