using System.IO;
using System.Windows;
using Serilog;

namespace Forge
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            string logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Forge", "logs");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(Path.Combine(logDir, "forge-.txt"), rollingInterval: RollingInterval.Day)
                .CreateLogger();

            DispatcherUnhandledException += (_, args) =>
            {
                Log.Error(args.Exception, "Unhandled UI exception");
                MessageBox.Show(args.Exception.Message, "Forge — unexpected error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
