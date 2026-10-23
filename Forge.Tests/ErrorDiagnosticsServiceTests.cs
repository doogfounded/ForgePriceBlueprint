using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Forge.Services;
using Xunit;

namespace Forge.Tests
{
    public class ErrorDiagnosticsServiceTests
    {
        [Fact]
        public void ReportError_RecordsEntryAndFiresEvent()
        {
            var service = new ErrorDiagnosticsService();
            DiagnosticErrorEntry? received = null;
            service.ErrorOccurred += entry => received = entry;

            var entry = service.ReportError("TestModule", "Something went wrong");

            Assert.NotNull(received);
            Assert.Equal("TestModule", received.Source);
            Assert.Equal("Something went wrong", received.Message);
            Assert.False(received.IsFatal);
            Assert.Single(service.History);
            Assert.Equal(1, service.ErrorCount);
            Assert.True(service.HasErrors);
            Assert.Same(entry, service.LatestError);
        }

        [Fact]
        public void ReportException_CapturesExceptionTypeAndStackTrace()
        {
            var service = new ErrorDiagnosticsService();
            Exception ex;
            try
            {
                throw new InvalidOperationException("Operation failed test");
            }
            catch (Exception caught)
            {
                ex = caught;
            }

            var entry = service.ReportException("PricingEngine", ex, "{\"quantity\": 5}");

            Assert.Equal("PricingEngine", entry.Source);
            Assert.Equal("Operation failed test", entry.Message);
            Assert.Equal(typeof(InvalidOperationException).FullName, entry.ExceptionType);
            Assert.NotNull(entry.StackTrace);
            Assert.Contains("ReportException_CapturesExceptionTypeAndStackTrace", entry.StackTrace);
            Assert.Equal("{\"quantity\": 5}", entry.ContextSnapshot);
            Assert.False(entry.IsFatal);
        }

        [Fact]
        public void ReportUnhandledException_SetsFatalFlagAndSource()
        {
            var service = new ErrorDiagnosticsService();
            var ex = new ApplicationException("Fatal crash test");

            var entry = service.ReportUnhandledException(ex);

            Assert.Equal("Unhandled Crash", entry.Source);
            Assert.Equal("Fatal crash test", entry.Message);
            Assert.True(entry.IsFatal);
            Assert.Equal(typeof(ApplicationException).FullName, entry.ExceptionType);
            Assert.NotNull(entry.StackTrace);
        }

        [Fact]
        public void ToDetailedReport_FormatsCompleteDiagnostics()
        {
            var entry = new DiagnosticErrorEntry(
                Timestamp: new DateTime(2026, 10, 23, 13, 30, 0),
                Source: "EngineInterop",
                Message: "Cannot resolve symbol",
                ExceptionType: "System.DllNotFoundException",
                StackTrace: "   at Forge.Services.PricingEngine.Calculate()",
                ContextSnapshot: "{\"blueprint\": \"tier_1\"}",
                IsFatal: true);

            string report = entry.ToDetailedReport();

            Assert.Contains("=== FORGE ERROR REPORT ===", report);
            Assert.Contains("Timestamp:      2026-10-23 13:30:00.000", report);
            Assert.Contains("Source:         EngineInterop", report);
            Assert.Contains("Severity:       FATAL CRASH", report);
            Assert.Contains("Exception Type: System.DllNotFoundException", report);
            Assert.Contains("Message:        Cannot resolve symbol", report);
            Assert.Contains("--- STACK TRACE ---", report);
            Assert.Contains("at Forge.Services.PricingEngine.Calculate()", report);
            Assert.Contains("--- CONTEXT SNAPSHOT ---", report);
            Assert.Contains("{\"blueprint\": \"tier_1\"}", report);
        }

        [Fact]
        public void Clear_EmptiesHistoryAndFiresEvent()
        {
            var service = new ErrorDiagnosticsService();
            service.ReportError("Mod1", "Err1");
            service.ReportError("Mod2", "Err2");
            Assert.Equal(2, service.ErrorCount);

            bool clearedFired = false;
            service.HistoryCleared += () => clearedFired = true;

            service.Clear();

            Assert.True(clearedFired);
            Assert.Equal(0, service.ErrorCount);
            Assert.False(service.HasErrors);
            Assert.Null(service.LatestError);
            Assert.Empty(service.History);
        }

        [Fact]
        public void ThreadSafety_ConcurrentErrorReporting()
        {
            var service = new ErrorDiagnosticsService();
            const int totalReports = 500;
            int eventCounter = 0;
            service.ErrorOccurred += _ => Interlocked.Increment(ref eventCounter);

            Parallel.For(0, totalReports, i =>
            {
                service.ReportError($"Thread-{i % 8}", $"Message {i}");
            });

            Assert.Equal(totalReports, service.ErrorCount);
            Assert.Equal(totalReports, eventCounter);
            Assert.Equal(totalReports, service.History.Count);
        }

        [Fact]
        public void GetLogDirectory_ReturnsValidPath()
        {
            string dir = ErrorDiagnosticsService.GetLogDirectory();
            Assert.False(string.IsNullOrWhiteSpace(dir));
            Assert.Contains("Forge", dir);
            Assert.Contains("logs", dir);
        }
    }
}
