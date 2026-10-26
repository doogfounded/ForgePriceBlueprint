using System.Threading;
using System.Windows;
using Forge;
using Forge.Models;
using Xunit;

namespace Forge.Tests
{
    [Collection("DiagnosticsCollection")]
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

        [Fact]
        public void MainWindow_ErrorStatusBarUpdates_OnDiagnosticEvents()
        {
            Exception? threadEx = null;
            var t = new Thread(() =>
            {
                try
                {
                    Forge.Services.ErrorDiagnosticsService.Instance.Clear();
                    var window = new MainWindow();

                    Assert.Equal(Visibility.Collapsed, window.ErrorButtonVisibility);

                    // Report an error
                    Forge.Services.ErrorDiagnosticsService.Instance.ReportError("TestModule", "Sample failure");
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);

                    Assert.Equal(Visibility.Visible, window.ErrorButtonVisibility);
                    Assert.Equal("⚠ 1 Error", window.ErrorButtonText);

                    // Report a second error
                    Forge.Services.ErrorDiagnosticsService.Instance.ReportError("TestModule", "Another failure");
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);

                    Assert.Equal(Visibility.Visible, window.ErrorButtonVisibility);
                    Assert.Equal("⚠ 2 Errors", window.ErrorButtonText);

                    // Clear
                    Forge.Services.ErrorDiagnosticsService.Instance.Clear();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);

                    Assert.Equal(Visibility.Collapsed, window.ErrorButtonVisibility);

                    window.Close();
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
        public void MainWindow_SaveFailure_ReportsToDiagnosticsService()
        {
            Exception? threadEx = null;
            var t = new Thread(() =>
            {
                try
                {
                    Forge.Services.ErrorDiagnosticsService.Instance.Clear();
                    var window = new MainWindow();

                    bool saved = window.SaveTo("Z:\\Invalid\\NonExistentFolder_12345\\blueprint.json");
                    Assert.False(saved);

                    var latest = Forge.Services.ErrorDiagnosticsService.Instance.LatestError;
                    Assert.NotNull(latest);
                    Assert.Equal("Blueprint Save", latest.Source);
                    Assert.Contains("Path: Z:\\Invalid\\NonExistentFolder_12345\\blueprint.json", latest.ContextSnapshot);
                    Assert.Contains("Could not save blueprint", window.StatusMessage);

                    window.Close();
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


