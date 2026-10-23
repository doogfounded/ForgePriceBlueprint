using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Forge.Services;
using Serilog;

namespace Forge
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            string logDir = ErrorDiagnosticsService.GetLogDirectory();
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(Path.Combine(logDir, "forge-.txt"), rollingInterval: RollingInterval.Day)
                .CreateLogger();

            SetupExceptionHandling();

            base.OnStartup(e);
        }

        internal void SetupExceptionHandling()
        {
            DispatcherUnhandledException += (_, args) =>
            {
                HandleUnhandledCrash(args.Exception);
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    HandleUnhandledCrash(ex);
                }
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                HandleUnhandledCrash(args.Exception);
                args.SetObserved();
            };
        }

        internal static void HandleUnhandledCrash(Exception ex)
        {
            try
            {
                ErrorDiagnosticsService.Instance.ReportUnhandledException(ex);
                ErrorDetailsWindow.ShowWindow();
            }
            catch (Exception fallbackEx)
            {
                Log.Fatal(fallbackEx, "Failed to display ErrorDetailsWindow during unhandled crash");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
