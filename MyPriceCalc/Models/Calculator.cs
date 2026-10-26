namespace MyPriceCalc.Models
{
    /// <summary>How a row changes the running price.</summary>
    public enum AdjustmentKind
    {
        /// <summary>Signed percentage of the running price: -15 = 15% off, 10 = 10% markup.</summary>
        Percent,

        /// <summary>Signed amount added to the running price: -15 = $15 off, 4.99 = $4.99 shipping.</summary>
        Flat
    }

    public enum RoundingMode
    {
        None,
        NearestDollar,
        EndsIn99,
        EndsIn95,
        NearestNickel,
        NearestDime
    }

    /// <summary>One row of the adjustment list. Amount is signed and its meaning depends on <see cref="Kind"/>.</summary>
    public sealed class Adjustment
    {
        public bool Enabled { get; set; } = true;
        public AdjustmentKind Kind { get; set; } = AdjustmentKind.Percent;
        public decimal Amount { get; set; } = -10m;

        public string Describe() => Kind == AdjustmentKind.Percent
            ? $"{Amount:+0.##;-0.##;0}%"
            : $"{Amount:+$#,##0.00;-$#,##0.00;$0.00}";
    }

    /// <summary>The running price after a row was applied (or skipped, when <see cref="Adjustment.Enabled"/> is false).</summary>
    public sealed record RowOutcome(Adjustment Row, decimal After);

    public sealed record Calculation(
        decimal BasePrice,
        decimal BeforeRounding,
        decimal Total,
        IReadOnlyList<RowOutcome> Rows);

    /// <summary>Applies the adjustment rows top to bottom, then the optional rounding step.</summary>
    public static class Calculator
    {
        public static Calculation Evaluate(
            decimal basePrice,
            IReadOnlyList<Adjustment> rows,
            RoundingMode rounding)
        {
            decimal price = basePrice;
            var outcomes = new List<RowOutcome>(rows.Count);

            foreach (var row in rows)
            {
                if (row.Enabled)
                {
                    price = row.Kind == AdjustmentKind.Percent
                        ? price * (1m + row.Amount / 100m)
                        : price + row.Amount;
                }
                outcomes.Add(new RowOutcome(row, price));
            }

            decimal beforeRounding = price;
            return new Calculation(basePrice, beforeRounding, Round(price, rounding), outcomes);
        }

        public static decimal Round(decimal value, RoundingMode mode) => mode switch
        {
            RoundingMode.NearestDollar => Math.Round(value, 0, MidpointRounding.AwayFromZero),
            RoundingMode.EndsIn99 => Math.Round(value, 0, MidpointRounding.AwayFromZero) - 0.01m,
            RoundingMode.EndsIn95 => Math.Round(value, 0, MidpointRounding.AwayFromZero) - 0.05m,
            RoundingMode.NearestNickel => Math.Round(value * 20m, 0, MidpointRounding.AwayFromZero) / 20m,
            RoundingMode.NearestDime => Math.Round(value * 10m, 0, MidpointRounding.AwayFromZero) / 10m,
            _ => value
        };
    }
}
