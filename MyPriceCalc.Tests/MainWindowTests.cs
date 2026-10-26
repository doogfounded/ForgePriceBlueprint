using System.Threading;
using System.Windows;
using MyPriceCalc.Models;
using Xunit;

namespace MyPriceCalc.Tests
{
    public class MainWindowTests
    {
        private static void RunOnStaThread(Action action)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { failure = ex; }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join(TimeSpan.FromSeconds(10));

            if (failure != null) throw new AggregateException(failure);
        }

        [Fact]
        public void StartsAtOneHundred() => RunOnStaThread(() =>
        {
            var window = new MainWindow();

            Assert.Equal(0, window.RowCount);
            Assert.Equal("$100.00", window.TotalDisplay);
            window.Close();
        });

        [Fact]
        public void AddingARowAppliesTheDefaultDiscount() => RunOnStaThread(() =>
        {
            var window = new MainWindow();
            window.ClickAddRow();

            Assert.Equal(1, window.RowCount);
            Assert.Equal("$90.00", window.TotalDisplay);
            window.Close();
        });

        [Fact]
        public void EditsRecalculateLive() => RunOnStaThread(() =>
        {
            var window = new MainWindow();
            window.AddRow(new Adjustment { Kind = AdjustmentKind.Flat, Amount = 25m });

            Assert.Equal("$125.00", window.TotalDisplay);

            window.SetBasePrice("200");
            Assert.Equal("$225.00", window.TotalDisplay);
            window.Close();
        });

        [Fact]
        public void InvalidBasePriceClearsTheTotal() => RunOnStaThread(() =>
        {
            var window = new MainWindow();
            window.SetBasePrice("abc");

            Assert.Equal("—", window.TotalDisplay);

            window.SetBasePrice("50");
            Assert.Equal("$50.00", window.TotalDisplay);
            window.Close();
        });

        [Fact]
        public void RoundingAppliesToTheTotal() => RunOnStaThread(() =>
        {
            var window = new MainWindow();
            window.AddRow(new Adjustment { Kind = AdjustmentKind.Percent, Amount = 12.5m });

            Assert.Equal("$112.50", window.TotalDisplay);

            window.SetRounding(RoundingMode.EndsIn99);
            Assert.Equal("$112.99", window.TotalDisplay);
            window.Close();
        });

        [Fact]
        public void ResetClearsRowsAndRestoresDefaults() => RunOnStaThread(() =>
        {
            var window = new MainWindow();
            window.ClickAddRow();
            window.SetBasePrice("500");

            window.ClickReset();

            Assert.Equal(0, window.RowCount);
            Assert.Equal("$100.00", window.TotalDisplay);
            window.Close();
        });
    }
}
