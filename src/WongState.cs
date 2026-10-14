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
        private bool _seeking;

        /// <summary>We are deliberately not betting.</summary>
        public bool SitOutIntent { get { return _intent; } }

        /// <summary>Standing beside the table: meant to sit out and the seat is gone.</summary>
        public bool IsOutside { get { return _intent && _seatLost; } }

        /// <summary>
        /// Looking for a seat: the shoe turned and any free one will do.
        /// </summary>
        public bool SeekingSeat { get { return _intent && _seeking; } }

        public void Reset()
        {
            _intent = false;
            _seatLost = false;
            _seeking = false;
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
            _seeking = false;
        }

        /// <summary>
        /// The shoe turned: from here on take any seat that frees up. Latched on
        /// purpose. At a full table the wait for a seat lasts rounds, and the count
        /// drifts while it does - deciding again on every frame means a seat that
        /// opens during a dip to -0.4 is passed over, and by the time the count is
        /// back above the line the seat is gone. The latch is only dropped when the
        /// shoe turns genuinely bad again (StopSeeking), which is what the dead band
        /// between WongOutTrueCount and WongInTrueCount is for.
        ///
        /// Returns true the first time, so the decision gets logged once.
        /// </summary>
        public bool WantSeat()
        {
            if (!_intent || _seeking) return false;
            _seeking = true;
            return true;
        }

        /// <summary>
        /// The shoe went bad again before a seat came free - stop looking.
        /// True exactly once, for the log.
        /// </summary>
        public bool StopSeeking()
        {
            if (!_seeking) return false;
            _seeking = false;
            return true;
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
            _seeking = false;
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
