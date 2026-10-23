using System;
using System.Threading;
using System.Windows.Threading;
using Forge;
using Forge.Services;
using Xunit;

namespace Forge.Tests
{
    public class ErrorDetailsWindowTests
    {
        [Fact]
        public void ErrorDetailsWindow_LoadsExistingHistory_AndUpdatesOnNewError()
        {
            Exception? threadEx = null;
            var t = new Thread(() =>
            {
                try
                {
                    var service = new ErrorDiagnosticsService();
                    service.ReportError("Engine", "First engine issue");

                    var window = new ErrorDetailsWindow(service);
                    Assert.Equal(1, window.ErrorCount);
                    Assert.Contains("First engine issue", window.DetailsText);
                    Assert.Contains("=== FORGE ERROR REPORT ===", window.DetailsText);

                    // Add second error
                    service.ReportError("Parser", "Second parser issue", new FormatException("Bad format"));

                    // Allow dispatcher queue to process
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);

                    Assert.Equal(2, window.ErrorCount);

                    // Switch selection to first error
                    window.SelectIndex(0);
                    Assert.Contains("First engine issue", window.DetailsText);

                    // Switch selection to second error
                    window.SelectIndex(1);
                    Assert.Contains("Second parser issue", window.DetailsText);
                    Assert.Contains("System.FormatException", window.DetailsText);

                    // Clear service history
                    service.Clear();
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);

                    Assert.Equal(0, window.ErrorCount);
                    Assert.Equal(string.Empty, window.DetailsText);

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
