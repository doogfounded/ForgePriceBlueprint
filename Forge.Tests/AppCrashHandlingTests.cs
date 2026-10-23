using System;
using System.Threading;
using Forge;
using Forge.Services;
using Xunit;

namespace Forge.Tests
{
    public class AppCrashHandlingTests
    {
        [Fact]
        public void HandleUnhandledCrash_RecordsFatalErrorInDiagnosticsService()
        {
            Exception? threadEx = null;
            var t = new Thread(() =>
            {
                try
                {
                    ErrorDiagnosticsService.Instance.Clear();

                    var testEx = new InvalidOperationException("Simulation unhandled crash test");
                    App.HandleUnhandledCrash(testEx);

                    var latest = ErrorDiagnosticsService.Instance.LatestError;
                    Assert.NotNull(latest);
                    Assert.True(latest.IsFatal);
                    Assert.Equal("Unhandled Crash", latest.Source);
                    Assert.Equal("Simulation unhandled crash test", latest.Message);
                    Assert.Equal(typeof(InvalidOperationException).FullName, latest.ExceptionType);
                    Assert.NotNull(latest.StackTrace);

                    // Clean up
                    ErrorDiagnosticsService.Instance.Clear();
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
