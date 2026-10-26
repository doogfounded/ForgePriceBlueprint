using MyPriceCalc.Models;

namespace MyPriceCalc.Tests
{
    public class CalculatorTests
    {
        private static Adjustment Percent(decimal amount, bool enabled = true) =>
            new() { Kind = AdjustmentKind.Percent, Amount = amount, Enabled = enabled };

        private static Adjustment Flat(decimal amount, bool enabled = true) =>
            new() { Kind = AdjustmentKind.Flat, Amount = amount, Enabled = enabled };

        [Fact]
        public void AppliesRowsInOrder()
        {
            // 100 - 10% = 90, + 15% = 103.50, - 3.50 = 100
            var result = Calculator.Evaluate(100m, new[] { Percent(-10m), Percent(15m), Flat(-3.50m) }, RoundingMode.None);

            Assert.Equal(100m, result.Total);
            Assert.Equal(new[] { 90m, 103.50m, 100m }, result.Rows.Select(r => r.After));
            Assert.Equal(3, result.Rows.Count);
        }

        [Fact]
        public void OrderMatters()
        {
            var discountFirst = Calculator.Evaluate(100m, new[] { Percent(-50m), Flat(-10m) }, RoundingMode.None);
            var flatFirst = Calculator.Evaluate(100m, new[] { Flat(-10m), Percent(-50m) }, RoundingMode.None);

            Assert.Equal(40m, discountFirst.Total);
            Assert.Equal(45m, flatFirst.Total);
        }

        [Fact]
        public void DisabledRowsAreSkippedButStillReported()
        {
            var result = Calculator.Evaluate(100m, new[] { Percent(-10m, enabled: false), Flat(-5m) }, RoundingMode.None);

            Assert.Equal(95m, result.Total);
            Assert.Equal(2, result.Rows.Count);
            Assert.Equal(100m, result.Rows[0].After);
            Assert.Equal(95m, result.Rows[1].After);
        }

        [Fact]
        public void NoRowsLeavesTheBasePriceAlone()
        {
            var result = Calculator.Evaluate(42.5m, Array.Empty<Adjustment>(), RoundingMode.None);

            Assert.Equal(42.5m, result.BasePrice);
            Assert.Equal(42.5m, result.Total);
            Assert.Empty(result.Rows);
        }

        [Theory]
        [InlineData(10.34, RoundingMode.NearestDollar, 10.00)]
        [InlineData(10.50, RoundingMode.NearestDollar, 11.00)]
        [InlineData(10.34, RoundingMode.EndsIn99, 9.99)]
        [InlineData(10.34, RoundingMode.EndsIn95, 9.95)]
        [InlineData(10.37, RoundingMode.NearestNickel, 10.35)]
        [InlineData(10.38, RoundingMode.NearestNickel, 10.40)]
        [InlineData(10.34, RoundingMode.NearestDime, 10.30)]
        [InlineData(10.36, RoundingMode.NearestDime, 10.40)]
        [InlineData(10.3456, RoundingMode.None, 10.3456)]
        public void RoundingModes(decimal value, RoundingMode mode, decimal expected)
        {
            Assert.Equal(expected, Calculator.Round(value, mode));
        }

        [Fact]
        public void RoundingAppliesOnceAfterTheRows()
        {
            var rows = new[] { Percent(-10m), Percent(-10m) };
            var rounded = Calculator.Evaluate(100m, rows, RoundingMode.NearestDollar);

            Assert.Equal(81m, rounded.BeforeRounding);
            Assert.Equal(81m, rounded.Total);
            Assert.Equal(new[] { 90m, 81m }, rounded.Rows.Select(r => r.After));
        }

        [Fact]
        public void DescribeShowsTheSignedAmountForEachKind()
        {
            Assert.Equal("-15%", Percent(-15m).Describe());
            Assert.Equal("+8%", Percent(8m).Describe());
            Assert.Equal("-$4.99", Flat(-4.99m).Describe());
            Assert.Equal("+$12.00", Flat(12m).Describe());
        }
    }
}
