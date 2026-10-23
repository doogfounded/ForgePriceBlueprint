using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Forge;
using Forge.Services;
using Xunit;

namespace Forge.Tests
{
    [Collection("DiagnosticsCollection")]
    public class ErrorDiagnosticsEndToEndTests
    {
        [Fact]
        public void EndToEnd_SimulationError_CapturesContextSnapshot_AndUpdatesUI()
        {
            Exception? threadEx = null;
            var t = new Thread(() =>
            {
                try
                {
                    ErrorDiagnosticsService.Instance.Clear();
                    var window = new MainWindow();

                    // Wait for initial load
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
                    Assert.Equal(Visibility.Collapsed, window.ErrorButtonVisibility);

                    // Trigger an engine error during recompute
                    window.RecomputeForTesting(@"{
                        ""BlueprintName"": ""Faulty Blueprint"",
                        ""Rules"": [
                            { ""Type"": ""NonExistentRule"", ""Name"": ""Invalid"" }
                        ]
                    }");
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);

                    // Verify error was reported
                    Assert.True(ErrorDiagnosticsService.Instance.HasErrors);
                    var latest = ErrorDiagnosticsService.Instance.LatestError;
                    Assert.NotNull(latest);
                    Assert.Equal("Pricing Engine", latest.Source);
                    Assert.NotNull(latest.ContextSnapshot);
                    Assert.Contains("Blueprint JSON:", latest.ContextSnapshot);
                    Assert.Contains("Context Variables:", latest.ContextSnapshot);
                    Assert.Contains("Faulty Blueprint", latest.ContextSnapshot);

                    // Verify MainWindow status bar button is now visible
                    Assert.Equal(Visibility.Visible, window.ErrorButtonVisibility);
                    Assert.Contains("⚠ 1 Error", window.ErrorButtonText);

                    // Open ErrorDetailsWindow and verify populated details
                    var detailsWindow = new ErrorDetailsWindow();
                    Assert.Equal(1, detailsWindow.ErrorCount);
                    Assert.Contains("NonExistentRule", detailsWindow.DetailsText);
                    Assert.Contains("--- CONTEXT SNAPSHOT ---", detailsWindow.DetailsText);

                    // Test Clear button in ErrorDetailsWindow
                    detailsWindow.ClearForTesting();
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);

                    // Verify everything resets
                    Assert.False(ErrorDiagnosticsService.Instance.HasErrors);
                    Assert.Equal(0, detailsWindow.ErrorCount);
                    Assert.Equal(string.Empty, detailsWindow.DetailsText);
                    Assert.Equal(Visibility.Collapsed, window.ErrorButtonVisibility);

                    detailsWindow.Close();
                    window.Close();
                }
                catch (Exception ex)
                {
                    threadEx = ex;
                }
            });

            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join(TimeSpan.FromSeconds(15));

            if (threadEx != null)
            {
                throw new AggregateException(threadEx);
            }
        }

        [Fact]
        public void EndToEnd_DetailedReport_FormatsCleanTextReport()
        {
            var entry = new DiagnosticErrorEntry(
                Timestamp: new DateTime(2026, 10, 23, 14, 0, 0),
                Source: "Pricing Engine",
                Message: "Rule execution failed on condition 'quantity > 0'",
                ExceptionType: "Forge.Services.PricingEngineException",
                StackTrace: "   at Forge.Services.PricingEngine.CalculateRaw()",
                ContextSnapshot: "Blueprint JSON:\n{\n  \"name\": \"Test\"\n}\n\nContext Variables:\n{\n  \"quantity\": 0\n}",
                IsFatal: false);

            string report = entry.ToDetailedReport();

            Assert.Contains("=== FORGE ERROR REPORT ===", report);
            Assert.Contains("Source:         Pricing Engine", report);
            Assert.Contains("Severity:       ERROR", report);
            Assert.Contains("Exception Type: Forge.Services.PricingEngineException", report);
            Assert.Contains("Message:        Rule execution failed on condition 'quantity > 0'", report);
            Assert.Contains("--- STACK TRACE ---", report);
            Assert.Contains("at Forge.Services.PricingEngine.CalculateRaw()", report);
            Assert.Contains("--- CONTEXT SNAPSHOT ---", report);
            Assert.Contains("\"quantity\": 0", report);
        }
    }
}
