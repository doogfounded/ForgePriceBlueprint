using System.Threading;
using System.Windows;
using Forge;
using Forge.Models;
using Xunit;

namespace Forge.Tests
{
    public class MainWindowTests
    {
        [Fact]
        public void MainWindow_InitializesWithEnterprisePreset_AndComputesPrice()
        {
            Exception? threadEx = null;
            var t = new Thread(() =>
            {
                try
                {
                    var window = new MainWindow();
                    Assert.True(window.CardCount > 0);
                    Assert.Contains("$", window.FinalPriceDisplay);
                    Assert.NotEqual("$0.00", window.FinalPriceDisplay);
                    Assert.NotEqual("—", window.FinalPriceDisplay);

                    // Verify JSON editor is in sync
                    Assert.Contains("Enterprise Custom Contract", window.JsonEditorText);
                    // Verify YAML editor is in sync
                    Assert.Contains("Enterprise Custom Contract", window.YamlEditorText);

                    // Test altering a simulator variable
                    window.SetValue("quantity", 10.0);
                    Assert.Contains("$", window.FinalPriceDisplay);
                }
                catch (Exception ex)
                {
                    threadEx = ex;
                }
            });

            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join(TimeSpan.FromSeconds(10));

            if (threadEx != null)
            {
                throw new AggregateException(threadEx);
            }
        }

        [Fact]
        public void MainWindow_CanAddRule_AndUpdatesCardCount()
        {
            Exception? threadEx = null;
            var t = new Thread(() =>
            {
                try
                {
                    var window = new MainWindow();
                    int initialCount = window.CardCount;
                    window.Document.AddRule("Rounding");
                    Assert.Equal(initialCount + 1, window.CardCount);
                    Assert.True(window.IsDirty);
                }
                catch (Exception ex)
                {
                    threadEx = ex;
                }
            });

            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join(TimeSpan.FromSeconds(10));

            if (threadEx != null)
            {
                throw new AggregateException(threadEx);
            }
        }
    }
}
