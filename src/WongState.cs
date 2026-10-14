namespace BJCCheat
{
    /// <summary>
    /// Back-counting state, kept free of any game types so it can be tested.
    ///
    /// Two things have to be told apart, and conflating them is what broke the feature
    /// three times: the INTENT to sit out (set the moment a bet is skipped) and the
    /// STATE of having no seat (which follows seconds later, when the server reacts).
    /// Between those two moments the player is still seated - clearing the intent there
    /// means that by the time the seat is actually gone, nobody remembers wanting it back.
    /// </summary>
    public class WongState
    {
        private bool _intent;
        private bool _seatLost;

        /// <summary>We are deliberately not betting.</summary>
        public bool SitOutIntent { get { return _intent; } }

        /// <summary>Standing beside the table: meant to sit out and the seat is gone.</summary>
        public bool IsOutside { get { return _intent && _seatLost; } }

        public void Reset()
        {
            _intent = false;
            _seatLost = false;
        }

        /// <summary>A bet was skipped on purpose.</summary>
        public void SkippedBet()
        {
            _intent = true;
        }

        /// <summary>A bet went out, so we are playing again.</summary>
        public void PlacedBet()
        {
            _intent = false;
            _seatLost = false;
        }

        /// <summary>
        /// Observed that we hold a seat. True exactly once - when we have it back
        /// after losing it, which is the moment to start betting again.
        /// </summary>
        public bool ObserveSeated()
        {
            if (!_seatLost) return false;
            _seatLost = false;
            _intent = false;
            return true;
        }

        /// <summary>
        /// Observed that we have no seat. True exactly once - when the loss is first
        /// noticed, so it gets reported but not repeated.
        /// </summary>
        public bool ObserveNoSeat()
        {
            if (!_intent || _seatLost) return false;
            _seatLost = true;
            return true;
        }
    }
}
