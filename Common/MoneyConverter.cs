namespace core_banking_lite.Common
{
    /// <summary>
    /// Converts between decimal currency amounts and their INTEGER minor-unit
    /// representation (e.g. cents, pence) used for exact storage in SQLite.
    ///
    /// SQLite REAL is IEEE 754 64-bit float and cannot represent most decimal
    /// fractions exactly. Storing money as INTEGER minor units guarantees that
    /// all balance arithmetic is performed with exact integer operations.
    ///
    /// Contract:
    ///   12.50  → 1250  (ToMinorUnits)
    ///   1250   → 12.50 (FromMinorUnits)
    /// </summary>
    public static class MoneyConverter
    {
        private const decimal ScaleFactor = 100m;

        /// <summary>
        /// Converts a decimal currency amount to its integer minor-unit representation.
        /// Uses MidpointRounding.AwayFromZero — the standard rounding convention in banking.
        /// </summary>
        public static long ToMinorUnits(decimal amount)
            => (long)Math.Round(amount * ScaleFactor, MidpointRounding.AwayFromZero);

        /// <summary>
        /// Converts a stored integer minor-unit value back to a decimal currency amount.
        /// Division by a decimal literal avoids any intermediate float conversion.
        /// </summary>
        public static decimal FromMinorUnits(long minor)
            => minor / ScaleFactor;
    }
}
