using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Serilog;

namespace Forge.Services
{
    public sealed record DiagnosticErrorEntry(
        DateTime Timestamp,
        string Source,
        string Message,
        string? ExceptionType = null,
        string? StackTrace = null,
        string? ContextSnapshot = null,
        bool IsFatal = false)
    {
        public string ToDetailedReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== FORGE ERROR REPORT ===");
            sb.AppendLine($"Timestamp:      {Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
            sb.AppendLine($"Source:         {Source}");
            sb.AppendLine($"Severity:       {(IsFatal ? "FATAL CRASH" : "ERROR")}");
            if (!string.IsNullOrWhiteSpace(ExceptionType))
            {
                sb.AppendLine($"Exception Type: {ExceptionType}");
            }
            sb.AppendLine($"Message:        {Message}");

            if (!string.IsNullOrWhiteSpace(StackTrace))
            {
                sb.AppendLine();
                sb.AppendLine("--- STACK TRACE ---");
                sb.AppendLine(StackTrace);
            }

            if (!string.IsNullOrWhiteSpace(ContextSnapshot))
            {
                sb.AppendLine();
                sb.AppendLine("--- CONTEXT SNAPSHOT ---");
                sb.AppendLine(ContextSnapshot);
            }

            sb.AppendLine("==========================");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Thread-safe service for recording, tracking, and dispatching diagnostic error entries.
    /// Connects to Serilog for persistent logging and notifies listeners for UI updates.
    /// </summary>
    public class ErrorDiagnosticsService
    {
        private static ErrorDiagnosticsService? _instance;
        private static readonly object InstanceLock = new();

        public static ErrorDiagnosticsService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (InstanceLock)
                    {
                        _instance ??= new ErrorDiagnosticsService();
                    }
                }
                return _instance;
            }
            internal set
            {
                lock (InstanceLock)
                {
                    _instance = value;
                }
            }
        }

        private readonly List<DiagnosticErrorEntry> _history = new();
        private readonly object _lock = new();

        public event Action<DiagnosticErrorEntry>? ErrorOccurred;
        public event Action? HistoryCleared;

        public IReadOnlyList<DiagnosticErrorEntry> History
        {
            get
            {
                lock (_lock)
                {
                    return _history.ToList();
                }
            }
        }

        public bool HasErrors
        {
            get
            {
                lock (_lock)
                {
                    return _history.Count > 0;
                }
            }
        }

        public int ErrorCount
        {
            get
            {
                lock (_lock)
                {
                    return _history.Count;
                }
            }
        }

        public DiagnosticErrorEntry? LatestError
        {
            get
            {
                lock (_lock)
                {
                    return _history.Count > 0 ? _history[^1] : null;
                }
            }
        }

        public DiagnosticErrorEntry ReportError(
            string source,
            string message,
            Exception? ex = null,
            string? contextSnapshot = null,
            bool isFatal = false)
        {
            string? exType = ex?.GetType().FullName;
            string? stackTrace = ex?.ToString() ?? (isFatal ? Environment.StackTrace : null);

            var entry = new DiagnosticErrorEntry(
                Timestamp: DateTime.Now,
                Source: source,
                Message: message,
                ExceptionType: exType,
                StackTrace: stackTrace,
                ContextSnapshot: contextSnapshot,
                IsFatal: isFatal);

            lock (_lock)
            {
                _history.Add(entry);
            }

            // Write to Serilog
            if (ex != null)
            {
                if (isFatal)
                {
                    Log.Fatal(ex, "[{Source}] {Message}", source, message);
                }
                else
                {
                    Log.Error(ex, "[{Source}] {Message}", source, message);
                }
            }
            else
            {
                if (isFatal)
                {
                    Log.Fatal("[{Source}] {Message}", source, message);
                }
                else
                {
                    Log.Error("[{Source}] {Message}", source, message);
                }
            }

            // Dispatch event to listeners (UI, status bar, error window)
            try
            {
                ErrorOccurred?.Invoke(entry);
            }
            catch (Exception eventEx)
            {
                Log.Warning(eventEx, "Failed to invoke ErrorOccurred listener");
            }

            return entry;
        }

        public DiagnosticErrorEntry ReportException(
            string source,
            Exception ex,
            string? contextSnapshot = null,
            bool isFatal = false)
        {
            return ReportError(source, ex.Message, ex, contextSnapshot, isFatal);
        }

        public DiagnosticErrorEntry ReportUnhandledException(
            Exception ex,
            string? contextSnapshot = null)
        {
            return ReportError("Unhandled Crash", ex.Message, ex, contextSnapshot, isFatal: true);
        }

        public void Clear()
        {
            lock (_lock)
            {
                _history.Clear();
            }

            try
            {
                HistoryCleared?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to invoke HistoryCleared listener");
            }
        }

        public static string GetLogDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Forge",
                "logs");
        }
    }
}
