using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Forge.Services;

namespace Forge
{
    public partial class ErrorDetailsWindow : Window
    {
        private static ErrorDetailsWindow? _activeInstance;

        public static void ShowWindow(ErrorDiagnosticsService? service = null)
        {
            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => ShowWindow(service));
                return;
            }

            if (_activeInstance == null || !_activeInstance.IsLoaded)
            {
                _activeInstance = new ErrorDetailsWindow(service);
                _activeInstance.Closed += (_, _) => _activeInstance = null;
                _activeInstance.Show();
            }
            else
            {
                if (_activeInstance.WindowState == WindowState.Minimized)
                {
                    _activeInstance.WindowState = WindowState.Normal;
                }
                _activeInstance.Activate();
            }
        }

        private readonly ErrorDiagnosticsService _service;
        private readonly ObservableCollection<DiagnosticErrorEntry> _entries = new();

        public ErrorDetailsWindow() : this(null)
        {
        }

        public ErrorDetailsWindow(ErrorDiagnosticsService? service)
        {
            InitializeComponent();

            _service = service ?? ErrorDiagnosticsService.Instance;

            foreach (var item in _service.History)
            {
                _entries.Add(item);
            }

            ErrorsListBox.ItemsSource = _entries;

            if (_entries.Count > 0)
            {
                ErrorsListBox.SelectedIndex = _entries.Count - 1;
            }

            _service.ErrorOccurred += OnErrorOccurred;
            _service.HistoryCleared += OnHistoryCleared;

            Closed += (_, _) =>
            {
                _service.ErrorOccurred -= OnErrorOccurred;
                _service.HistoryCleared -= OnHistoryCleared;
            };
        }

        private void OnErrorOccurred(DiagnosticErrorEntry entry)
        {
            Dispatcher.InvokeAsync(() =>
            {
                _entries.Add(entry);
                ErrorsListBox.SelectedItem = entry;
                ErrorsListBox.ScrollIntoView(entry);
            });
        }

        private void OnHistoryCleared()
        {
            Dispatcher.InvokeAsync(() =>
            {
                _entries.Clear();
                DetailsTextBox.Text = string.Empty;
            });
        }

        private void ErrorsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ErrorsListBox.SelectedItem is DiagnosticErrorEntry entry)
            {
                DetailsTextBox.Text = entry.ToDetailedReport();
            }
            else
            {
                DetailsTextBox.Text = string.Empty;
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(DetailsTextBox.Text))
            {
                try
                {
                    Clipboard.SetText(DetailsTextBox.Text);
                }
                catch
                {
                    // Ignore transient clipboard access lock
                }
            }
        }

        private void OpenLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string dir = ErrorDiagnosticsService.GetLogDirectory();
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not open log folder: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            _service.Clear();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // =====================================================================
        // Test hooks
        // =====================================================================
        internal string DetailsText => DetailsTextBox.Text;
        internal int ErrorCount => _entries.Count;
        internal void SelectIndex(int index) => ErrorsListBox.SelectedIndex = index;
    }
}
