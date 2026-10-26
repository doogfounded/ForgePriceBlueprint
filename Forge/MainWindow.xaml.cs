using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Forge.Controls;
using Forge.Models;
using Forge.Services;
using Microsoft.Win32;
using Serilog;

namespace Forge
{
    public partial class MainWindow : Window
    {
        private const string AppTitle = "Forge Price Blueprint Studio";

        private readonly PricingEngine _engine = new();
        private readonly RecentFilesService _recent = new();
        private readonly BlueprintDocument _doc = new();
        private readonly HashSet<JsonObject> _collapsed = new();

        private Dictionary<string, object> _values = new();
        private Dictionary<string, object> _defaults = new();
        private List<ContextVariable> _vars = new();
        private string _varSignature = "";

        private string? _filePath;
        private bool _dirty;
        private bool _suppress;

        public MainWindow()
        {
            InitializeComponent();

            BindCommand(ApplicationCommands.New, () => NewBlueprint());
            BindCommand(ApplicationCommands.Open, OpenBlueprint);
            BindCommand(ApplicationCommands.Save, () => Save());
            BindCommand(ApplicationCommands.SaveAs, () => SaveAs());
            InputBindings.Add(new KeyBinding(new RelayCommand(() => ErrorDetails_Click(this, new RoutedEventArgs())), Key.E, ModifierKeys.Control | ModifierKeys.Shift));

            BuildRuleMenus();
            BuildPresetMenu();

            _doc.Changed += OnDocChanged;
            BlueprintNameBox.TextChanged += (_, _) =>
            {
                if (_suppress) return;
                _doc.Name = BlueprintNameBox.Text;
            };
            Closing += (_, e) => { if (!ConfirmDiscard()) e.Cancel = true; };

            ErrorDiagnosticsService.Instance.ErrorOccurred += OnDiagnosticError;
            ErrorDiagnosticsService.Instance.HistoryCleared += OnDiagnosticHistoryCleared;
            Closed += (_, _) =>
            {
                ErrorDiagnosticsService.Instance.ErrorOccurred -= OnDiagnosticError;
                ErrorDiagnosticsService.Instance.HistoryCleared -= OnDiagnosticHistoryCleared;
            };
            UpdateErrorStatus();

            UpdateRecentMenu();
            LoadPreset(Presets.All[0]);

            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1 && File.Exists(args[1])) OpenPath(args[1]);
        }

        // =====================================================================
        // Menus & commands
        // =====================================================================

        private sealed class RelayCommand : ICommand
        {
            private readonly Action _run;
            public RelayCommand(Action run) => _run = run;
            public event EventHandler? CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object? parameter) => true;
            public void Execute(object? parameter) => _run();
        }

        private void BindCommand(RoutedCommand command, Action run) =>
            CommandBindings.Add(new CommandBinding(command, (_, _) => run()));

        private void BuildRuleMenus()
        {
            var popup = new ContextMenu();
            foreach (string type in BlueprintDocument.RuleTypes)
            {
                string captured = type;
                void Add(object? s, RoutedEventArgs e) { _doc.AddRule(captured); ScrollRulesToEnd(); }

                var item = new MenuItem { Header = RuleCard.Friendly(type) };
                item.Click += Add;
                RuleMenu.Items.Add(item);

                var popupItem = new MenuItem { Header = RuleCard.Friendly(type) };
                popupItem.Click += Add;
                popup.Items.Add(popupItem);
            }
            AddRuleButton.Click += (_, _) =>
            {
                popup.PlacementTarget = AddRuleButton;
                popup.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
                popup.IsOpen = true;
            };
        }

        private void BuildPresetMenu()
        {
            foreach (var preset in Presets.All)
            {
                var captured = preset;
                var item = new MenuItem { Header = "New from: " + preset.DisplayName };
                item.Click += (_, _) => { if (ConfirmDiscard()) LoadPreset(captured); };
                PresetsMenu.Items.Add(item);
            }
        }

        private void UpdateRecentMenu()
        {
            RecentMenu.Items.Clear();
            var files = _recent.Load();
            RecentMenu.IsEnabled = files.Count > 0;
            foreach (string path in files)
            {
                string captured = path;
                var item = new MenuItem { Header = path.Replace("_", "__") };
                item.Click += (_, _) => { if (ConfirmDiscard()) OpenPath(captured); };
                RecentMenu.Items.Add(item);
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e) => Close();

        // =====================================================================
        // Document lifecycle
        // =====================================================================

        internal BlueprintDocument Document => _doc;
        internal bool IsDirty => _dirty;

        /// <summary>Replaces the document, resets simulator state, and clears the dirty flag unless asked otherwise.</summary>
        internal void SetDocument(string json, string? path, bool dirty, Preset? preset = null)
        {
            _values.Clear();
            _defaults = preset != null ? new Dictionary<string, object>(preset.Context) : new Dictionary<string, object>();
            _collapsed.Clear();
            _varSignature = "";

            _doc.Load(json);

            _filePath = path;
            _dirty = dirty;
            UpdateTitle();
        }

        private void LoadPreset(Preset preset) => SetDocument(preset.Json, null, false, preset);

        private void NewBlueprint()
        {
            if (!ConfirmDiscard()) return;
            SetDocument(BlueprintDocument.CreateBlank().ToJson(), null, false);
        }

        private void OpenBlueprint()
        {
            if (!ConfirmDiscard()) return;
            var dialog = new OpenFileDialog { Filter = "Blueprint (*.json)|*.json|All files (*.*)|*.*", Title = "Open blueprint" };
            if (dialog.ShowDialog(this) == true) OpenPath(dialog.FileName);
        }

        internal bool OpenPath(string path)
        {
            try
            {
                string json = BlueprintFileService.Open(path);
                SetDocument(json, path, false);
                _recent.Add(path);
                UpdateRecentMenu();
                SetStatus($"Opened {path}");
                return true;
            }
            catch (BlueprintLoadException ex)
            {
                ShowLoadError("Could not open blueprint", ex);
                return false;
            }
        }

        private bool Save() => _filePath == null ? SaveAs() : SaveTo(_filePath);

        private bool SaveAs()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Blueprint (*.json)|*.json",
                Title = "Save blueprint",
                FileName = Sanitize(_doc.Name) + ".json",
                InitialDirectory = _filePath != null ? Path.GetDirectoryName(_filePath) : null
            };
            return dialog.ShowDialog(this) == true && SaveTo(dialog.FileName);
        }

        internal bool SaveTo(string path)
        {
            try
            {
                BlueprintFileService.Save(path, _doc.ToJson());
                _filePath = path;
                _dirty = false;
                _recent.Add(path);
                UpdateRecentMenu();
                UpdateTitle();
                SetStatus($"Saved {path}");
                return true;
            }
            catch (BlueprintLoadException ex)
            {
                ShowLoadError("Could not save blueprint", ex);
                return false;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Error(ex, "Save failed for {Path}", path);
                ErrorDiagnosticsService.Instance.ReportException("Blueprint Save", ex, $"Path: {path}");
                SetStatus($"Could not save blueprint: {ex.Message}", error: true);
                return false;
            }
        }

        private static string Sanitize(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return string.IsNullOrWhiteSpace(name) ? "blueprint" : name.Trim();
        }

        private bool ConfirmDiscard()
        {
            if (!_dirty) return true;
            var answer = MessageBox.Show(this, $"Save changes to \"{_doc.Name}\"?", AppTitle,
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            return answer switch
            {
                MessageBoxResult.Yes => Save(),
                MessageBoxResult.No => true,
                _ => false
            };
        }

        private void ShowLoadError(string title, BlueprintLoadException ex)
        {
            string detail = ex.Errors.Count == 0 ? "" : "\n\n• " + string.Join("\n• ", ex.Errors.Take(8)) +
                (ex.Errors.Count > 8 ? $"\n…and {ex.Errors.Count - 8} more" : "");
            ErrorDiagnosticsService.Instance.ReportException(title, ex, detail);
            SetStatus($"{title}: {ex.Message}", error: true);
        }

        private void UpdateTitle()
        {
            string file = _filePath != null ? Path.GetFileName(_filePath) : "Untitled";
            Title = $"{(_dirty ? "● " : "")}{_doc.Name} — {file} — {AppTitle}";
            FileText.Text = _filePath ?? "Untitled (not saved)";
        }

        private void SetStatus(string text, bool error = false)
        {
            StatusText.Text = text;
            StatusText.Foreground = error ? Theme.Error : Theme.Text;
        }

        // =====================================================================
        // Document change handling
        // =====================================================================

        private void OnDocChanged(bool structural)
        {
            if (structural) RebuildCards();

            if (BlueprintNameBox.Text != _doc.Name)
            {
                _suppress = true;
                BlueprintNameBox.Text = _doc.Name;
                _suppress = false;
            }

            _dirty = true;
            UpdateTitle();
            RefreshSimulation();
        }

        // =====================================================================
        // Designer
        // =====================================================================

        private void RebuildCards()
        {
            RulesPanel.Children.Clear();
            if (_doc.Rules.Count == 0)
            {
                RulesPanel.Children.Add(new TextBlock
                {
                    Text = "No rules yet. Use “Add Rule” below.",
                    Foreground = Theme.Muted,
                    Margin = new Thickness(4, 12, 4, 0)
                });
                return;
            }

            foreach (var rule in _doc.RuleObjects)
            {
                var card = new RuleCard(rule, expanded: !_collapsed.Contains(rule));
                card.Edited += _doc.NotifyEdited;
                card.MoveRequested += (c, delta) => _doc.MoveRule(c.Rule, delta);
                card.RemoveRequested += c => _doc.RemoveRule(c.Rule);
                card.ExpandedChanged += (c, open) => { if (open) _collapsed.Remove(c.Rule); else _collapsed.Add(c.Rule); };
                RulesPanel.Children.Add(card);
            }
        }

        private void ScrollRulesToEnd()
        {
            if (RulesPanel.Parent is ScrollViewer sv) sv.Dispatcher.BeginInvoke(sv.ScrollToEnd, DispatcherPriority.Background);
        }

        // =====================================================================
        // Simulator
        // =====================================================================

        private void RefreshSimulation()
        {
            string json = _doc.ToJson();

            _vars = ContextInference.Infer(_doc.Rules);
            string signature = string.Join("|", _vars.Select(v => $"{v.Key}:{v.Kind}"));
            ReconcileValues();
            if (signature != _varSignature)
            {
                _varSignature = signature;
                BuildContextPanel();
            }

            try
            {
                BlueprintFileService.Validate(json);
            }
            catch (BlueprintLoadException ex)
            {
                ClearResults();
                SetStatus($"Blueprint is incomplete: {ex.Errors.FirstOrDefault() ?? ex.Message}", error: true);
                return;
            }

            Recompute(json);
        }

        private void ReconcileValues()
        {
            foreach (var v in _vars)
            {
                bool matches = _values.TryGetValue(v.Key, out var current) && current.GetType() == v.Default.GetType();
                if (matches) continue;

                _values[v.Key] = _defaults.TryGetValue(v.Key, out var preferred) && preferred.GetType() == v.Default.GetType()
                    ? preferred
                    : v.Default;
            }
        }

        private Dictionary<string, object> CurrentContext() => _vars.ToDictionary(v => v.Key, v => _values[v.Key]);

        private void BuildContextPanel()
        {
            ContextPanel.Children.Clear();
            if (_vars.Count == 0)
            {
                ContextPanel.Children.Add(new TextBlock { Text = "This blueprint needs no inputs.", Foreground = Theme.Muted });
                return;
            }

            foreach (var v in _vars)
            {
                string key = v.Key;
                var cell = new StackPanel { Margin = new Thickness(0, 0, 18, 8), MinWidth = 120 };
                cell.Children.Add(new TextBlock { Text = key, Foreground = Theme.Muted, FontSize = 11, Margin = new Thickness(0, 0, 0, 2) });

                switch (v.Kind)
                {
                    case ContextKind.Bool:
                        var check = new CheckBox { IsChecked = (bool)_values[key], Content = "true", Margin = new Thickness(0, 4, 0, 0) };
                        check.Click += (_, _) => { _values[key] = check.IsChecked == true; Recompute(); };
                        cell.Children.Add(check);
                        break;

                    case ContextKind.Number:
                        var num = new TextBox
                        {
                            Text = ((double)_values[key]).ToString("R", CultureInfo.InvariantCulture),
                            Padding = new Thickness(4, 3, 4, 3)
                        };
                        num.TextChanged += (_, _) =>
                        {
                            if (double.TryParse(num.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d))
                            {
                                num.Background = Brushes.White;
                                _values[key] = d;
                                Recompute();
                            }
                            else num.Background = Theme.ErrorSoft;
                        };
                        cell.Children.Add(num);
                        break;

                    default:
                        var text = new TextBox { Text = (string)_values[key], Padding = new Thickness(4, 3, 4, 3) };
                        text.TextChanged += (_, _) => { _values[key] = text.Text; Recompute(); };
                        cell.Children.Add(text);
                        break;
                }
                ContextPanel.Children.Add(cell);
            }
        }

        private void ResetContext_Click(object sender, RoutedEventArgs e)
        {
            _values.Clear();
            _varSignature = "";
            RefreshSimulation();
        }

        private void Recompute() => Recompute(_doc.ToJson());

        private void Recompute(string json)
        {
            try
            {
                var result = _engine.Calculate(json, CurrentContext());

                FinalPriceText.Text = "$" + result.FinalPrice.ToString("N2");
                BasePriceText.Text = "$" + result.BasePrice.ToString("N2");
                double delta = result.FinalPrice - result.BasePrice;
                AdjustmentText.Text = (delta >= 0 ? "+" : "-") + "$" + Math.Abs(delta).ToString("N2");
                AdjustmentText.Foreground = delta < 0 ? Theme.Ok : delta > 0 ? Theme.Warn : Theme.Text;
                SavingsText.Text = result.BasePrice > 0 ? (-delta / result.BasePrice * 100).ToString("0.#") + "%" : "—";
                TraceList.ItemsSource = result.AuditTrail;

                SetEngineStatus(true, "Engine: ready");
                SetStatus(_dirty ? "Modified" : "Ready");
            }
            catch (PricingEngineException ex)
            {
                ClearResults();
                string snapshot = $"Blueprint JSON:\n{json}\n\nContext Variables:\n{JsonSerializer.Serialize(CurrentContext(), new JsonSerializerOptions { WriteIndented = true })}";
                ErrorDiagnosticsService.Instance.ReportException("Pricing Engine", ex, snapshot);
                SetStatus("Engine: " + ex.Message, error: true);
            }
            catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
            {
                Log.Error(ex, "Pricing engine could not be loaded");
                ClearResults();
                ErrorDiagnosticsService.Instance.ReportException("Pricing Engine Native DLL", ex, "Native library 'price_blueprint.dll' failed to load or missing entry point.");
                SetEngineStatus(false, "Engine: price_blueprint.dll missing or invalid");
                SetStatus(ex.Message, error: true);
            }
        }

        private void SetEngineStatus(bool ok, string text)
        {
            EngineStatus.Text = text;
            EngineStatus.Foreground = ok ? Theme.Ok : Theme.Error;
        }

        private void ClearResults()
        {
            FinalPriceText.Text = "—";
            BasePriceText.Text = AdjustmentText.Text = SavingsText.Text = "—";
            AdjustmentText.Foreground = Theme.Text;
            TraceList.ItemsSource = null;
        }

        // =====================================================================
        // Diagnostics & Error UI
        // =====================================================================

        private void OnDiagnosticError(DiagnosticErrorEntry _) => Dispatcher.InvokeAsync(UpdateErrorStatus);
        private void OnDiagnosticHistoryCleared() => Dispatcher.InvokeAsync(UpdateErrorStatus);

        private void UpdateErrorStatus()
        {
            int count = ErrorDiagnosticsService.Instance.ErrorCount;
            if (count > 0)
            {
                ErrorDetailsButton.Content = $"⚠ {count} Error{(count == 1 ? "" : "s")}";
                ErrorDetailsButton.Visibility = Visibility.Visible;
            }
            else
            {
                ErrorDetailsButton.Visibility = Visibility.Collapsed;
            }
        }

        private void ErrorDetailsButton_Click(object sender, RoutedEventArgs e) => ErrorDetailsWindow.ShowWindow();
        private void ErrorDetails_Click(object sender, RoutedEventArgs e) => ErrorDetailsWindow.ShowWindow();

        // =====================================================================
        // Test hooks
        // =====================================================================

        internal string FinalPriceDisplay => FinalPriceText.Text;
        internal int CardCount => RulesPanel.Children.OfType<RuleCard>().Count();
        internal IReadOnlyDictionary<string, object> Values => _values;
        internal string StatusMessage => StatusText.Text;
        internal Visibility ErrorButtonVisibility => ErrorDetailsButton.Visibility;
        internal string ErrorButtonText => (string)ErrorDetailsButton.Content;

        internal void SetValue(string key, object value)
        {
            _values[key] = value;
            _varSignature = "";
            RefreshSimulation();
        }

        internal void RecomputeForTesting(string json) => Recompute(json);
    }
}
