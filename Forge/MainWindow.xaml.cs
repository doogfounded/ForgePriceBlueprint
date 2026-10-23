using System.Data;
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
    public sealed record ValidationRow(string Text, Brush Brush);

    public partial class MainWindow : Window
    {
        private enum EditSource { None, Json, Yaml, Name }

        private const string AppTitle = "Forge Price Blueprint Studio";

        private readonly PricingEngine _engine = new();
        private readonly RecentFilesService _recent = new();
        private readonly BlueprintDocument _doc = new();
        private readonly HashSet<JsonObject> _collapsed = new();
        private readonly List<ScenarioItem> _scenarios = new();
        private readonly DispatcherTimer _jsonTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
        private readonly DispatcherTimer _yamlTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };

        private Dictionary<string, object> _values = new();
        private Dictionary<string, object> _defaults = new();
        private List<ContextVariable> _vars = new();
        private string _varSignature = "";

        private string? _filePath;
        private bool _dirty;
        private bool _suppress;
        private EditSource _source = EditSource.None;

        public MainWindow()
        {
            InitializeComponent();

            BindCommand(ApplicationCommands.New, () => NewBlueprint());
            BindCommand(ApplicationCommands.Open, OpenBlueprint);
            BindCommand(ApplicationCommands.Save, () => Save());
            BindCommand(ApplicationCommands.SaveAs, () => SaveAs());
            InputBindings.Add(new KeyBinding(new RelayCommand(() => RunValidation_Click(this, new RoutedEventArgs())), Key.F6, ModifierKeys.None));
            InputBindings.Add(new KeyBinding(new RelayCommand(() => ErrorDetails_Click(this, new RoutedEventArgs())), Key.E, ModifierKeys.Control | ModifierKeys.Shift));

            BuildRuleMenus();
            BuildPresetMenu();

            _doc.Changed += OnDocChanged;
            _jsonTimer.Tick += (_, _) => { _jsonTimer.Stop(); ApplyJsonEditor(); };
            _yamlTimer.Tick += (_, _) => { _yamlTimer.Stop(); ApplyYamlEditor(); };
            JsonEditor.TextChanged += (_, _) => { if (!_suppress) { _jsonTimer.Stop(); _jsonTimer.Start(); } };
            YamlEditor.TextChanged += (_, _) => { if (!_suppress) { _yamlTimer.Stop(); _yamlTimer.Start(); } };
            BlueprintNameBox.TextChanged += (_, _) =>
            {
                if (_suppress) return;
                _source = EditSource.Name;
                _doc.Name = BlueprintNameBox.Text;
                _source = EditSource.None;
            };
            MatrixGrid.MouseDoubleClick += (_, _) => LoadScenario_Click(this, new RoutedEventArgs());
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
            _scenarios.Clear();
            if (preset != null)
            {
                _scenarios.AddRange(preset.Scenarios.Select(s => new ScenarioItem { Name = s.Name, Variables = new(s.Variables) }));
            }
            _collapsed.Clear();
            _varSignature = "";
            _source = EditSource.None;

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
        // YAML import / export
        // =====================================================================

        private void ImportYaml_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfirmDiscard()) return;
            var dialog = new OpenFileDialog { Filter = "YAML (*.yaml;*.yml)|*.yaml;*.yml|All files (*.*)|*.*", Title = "Import YAML blueprint" };
            if (dialog.ShowDialog(this) != true) return;

            try
            {
                string json = YamlConverter.YamlToJson(File.ReadAllText(dialog.FileName));
                BlueprintFileService.Validate(json);
                SetDocument(json, null, true);
                SetStatus($"Imported {dialog.FileName} (unsaved)");
            }
            catch (BlueprintLoadException ex) { ShowLoadError("Could not import YAML", ex); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or YamlDotNet.Core.YamlException)
            {
                ErrorDiagnosticsService.Instance.ReportException("YAML Import", ex, $"File: {dialog.FileName}");
                SetStatus($"Could not import YAML: {ex.Message}", error: true);
            }
        }

        private void ExportYaml_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "YAML (*.yaml)|*.yaml",
                Title = "Export YAML",
                FileName = Sanitize(_doc.Name) + ".yaml"
            };
            if (dialog.ShowDialog(this) != true) return;

            try
            {
                File.WriteAllText(dialog.FileName, YamlConverter.JsonToYaml(_doc.ToJson()));
                SetStatus($"Exported {dialog.FileName}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ErrorDiagnosticsService.Instance.ReportException("YAML Export", ex, $"File: {dialog.FileName}");
                SetStatus($"Could not export YAML: {ex.Message}", error: true);
            }
        }

        // =====================================================================
        // Document change handling & editor sync
        // =====================================================================

        private void OnDocChanged(bool structural)
        {
            if (structural) RebuildCards();

            if (_source != EditSource.Name && BlueprintNameBox.Text != _doc.Name)
            {
                _suppress = true;
                BlueprintNameBox.Text = _doc.Name;
                _suppress = false;
            }

            _dirty = true;
            UpdateTitle();
            UpdateEditors(_source);
            RefreshSimulation();
        }

        private void UpdateEditors(EditSource skip)
        {
            _suppress = true;
            try
            {
                string json = _doc.ToJson();
                if (skip != EditSource.Json)
                {
                    JsonEditor.Text = json;
                    JsonErrorText.Visibility = Visibility.Collapsed;
                }
                if (skip != EditSource.Yaml)
                {
                    YamlEditor.Text = YamlConverter.JsonToYaml(json);
                    YamlErrorText.Visibility = Visibility.Collapsed;
                }
            }
            finally { _suppress = false; }
        }

        private void ApplyJsonEditor() => ApplyEditor(JsonEditor.Text, EditSource.Json, JsonErrorText, text => text);

        private void ApplyYamlEditor() => ApplyEditor(YamlEditor.Text, EditSource.Yaml, YamlErrorText, YamlConverter.YamlToJson);

        private void ApplyEditor(string text, EditSource source, TextBlock errorText, Func<string, string> toJson)
        {
            try
            {
                string json = toJson(text);
                BlueprintFileService.Validate(json);
                errorText.Visibility = Visibility.Collapsed;

                _source = source;
                try { _doc.Load(json); }
                finally { _source = EditSource.None; }
            }
            catch (BlueprintLoadException ex)
            {
                string first = ex.Errors.Count > 0 ? "\n• " + string.Join("\n• ", ex.Errors.Take(4)) : "";
                errorText.Text = ex.Message + first;
                errorText.Visibility = Visibility.Visible;
            }
            catch (Exception ex) when (ex is YamlDotNet.Core.YamlException or System.Text.Json.JsonException)
            {
                errorText.Text = ex.Message;
                errorText.Visibility = Visibility.Visible;
            }
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
                ChartView.Data = CurveCalculator.Compute(_engine, json, CurrentContext());
                BuildMatrix(json, result.FinalPrice);

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
            ChartView.Data = null;
            MatrixGrid.ItemsSource = null;
        }

        // =====================================================================
        // What-if scenarios
        // =====================================================================

        private Dictionary<string, object> ScenarioContext(ScenarioItem s)
        {
            var ctx = new Dictionary<string, object>();
            foreach (var v in _vars)
            {
                bool usable = s.Variables.TryGetValue(v.Key, out var val) && val.GetType() == v.Default.GetType();
                ctx[v.Key] = usable ? val! : _values.TryGetValue(v.Key, out var cur) ? cur : v.Default;
            }
            return ctx;
        }

        private static string Show(object value) => value switch
        {
            double d => d.ToString("0.####", CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            _ => value.ToString() ?? ""
        };

        private void BuildMatrix(string json, double currentFinal)
        {
            var table = new DataTable();
            var used = new HashSet<string> { "Scenario", "Final Price", "Δ vs current" };
            table.Columns.Add("Scenario");
            var keyColumns = new Dictionary<string, string>();
            foreach (var v in _vars)
            {
                string col = v.Key;
                while (!used.Add(col)) col += "_";
                keyColumns[v.Key] = col;
                table.Columns.Add(col);
            }
            table.Columns.Add("Final Price");
            table.Columns.Add("Δ vs current");

            var live = table.NewRow();
            live["Scenario"] = "▶ Current (live)";
            foreach (var v in _vars) live[keyColumns[v.Key]] = Show(_values[v.Key]);
            live["Final Price"] = "$" + currentFinal.ToString("N2");
            live["Δ vs current"] = "—";
            table.Rows.Add(live);

            foreach (var s in _scenarios)
            {
                var ctx = ScenarioContext(s);
                var row = table.NewRow();
                row["Scenario"] = s.Name;
                foreach (var v in _vars) row[keyColumns[v.Key]] = Show(ctx[v.Key]);
                try
                {
                    double price = _engine.Calculate(json, ctx).FinalPrice;
                    row["Final Price"] = "$" + price.ToString("N2");
                    double d = price - currentFinal;
                    row["Δ vs current"] = (d >= 0 ? "+" : "-") + "$" + Math.Abs(d).ToString("N2");
                }
                catch (PricingEngineException)
                {
                    row["Final Price"] = "error";
                    row["Δ vs current"] = "—";
                }
                table.Rows.Add(row);
            }

            int selected = MatrixGrid.SelectedIndex;
            MatrixGrid.ItemsSource = table.DefaultView;
            if (selected >= 0 && selected < table.Rows.Count) MatrixGrid.SelectedIndex = selected;
        }

        private void SaveScenario_Click(object sender, RoutedEventArgs e)
        {
            string? name = InputDialog.Show(this, "Save scenario", "Scenario name:", $"Scenario {_scenarios.Count + 1}");
            if (string.IsNullOrWhiteSpace(name)) return;

            _scenarios.Add(new ScenarioItem { Name = name, Variables = new Dictionary<string, object>(CurrentContext()) });
            Recompute();
        }

        private ScenarioItem? SelectedScenario()
        {
            int index = MatrixGrid.SelectedIndex - 1; // row 0 is the live row
            return index >= 0 && index < _scenarios.Count ? _scenarios[index] : null;
        }

        private void LoadScenario_Click(object sender, RoutedEventArgs e)
        {
            var scenario = SelectedScenario();
            if (scenario == null) return;

            foreach (var kv in ScenarioContext(scenario)) _values[kv.Key] = kv.Value;
            _varSignature = "";
            RefreshSimulation();
        }

        private void DeleteScenario_Click(object sender, RoutedEventArgs e)
        {
            var scenario = SelectedScenario();
            if (scenario == null) return;
            _scenarios.Remove(scenario);
            Recompute();
        }

        private void ImportScenarios_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "Scenarios (*.json)|*.json", Title = "Import scenarios" };
            if (dialog.ShowDialog(this) != true) return;

            try
            {
                var imported = ScenarioIO.Parse(File.ReadAllText(dialog.FileName));
                _scenarios.AddRange(imported);
                Recompute();
                SetStatus($"Imported {imported.Count} scenario(s)");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                ErrorDiagnosticsService.Instance.ReportException("Scenario Import", ex, $"File: {dialog.FileName}");
                SetStatus($"Could not import scenarios: {ex.Message}", error: true);
            }
        }

        private void ExportScenarios_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog { Filter = "Scenarios (*.json)|*.json", Title = "Export scenarios", FileName = "scenarios.json" };
            if (dialog.ShowDialog(this) != true) return;

            try
            {
                File.WriteAllText(dialog.FileName, ScenarioIO.Serialize(_scenarios));
                SetStatus($"Exported {_scenarios.Count} scenario(s)");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ErrorDiagnosticsService.Instance.ReportException("Scenario Export", ex, $"File: {dialog.FileName}");
                SetStatus($"Could not export scenarios: {ex.Message}", error: true);
            }
        }

        // =====================================================================
        // Validation
        // =====================================================================

        private async void RunValidation_Click(object sender, RoutedEventArgs e)
        {
            string json = _doc.ToJson();
            ValidationSummary.Text = "Running validation…";
            ValidationSummary.Foreground = Theme.Muted;
            ValidationList.ItemsSource = null;

            try
            {
                var report = await Task.Run(() => new BlueprintAnalyzer(_engine).Analyze(json));

                ValidationSummary.Text = report.Valid
                    ? $"✓ No issues across {report.ScenariosTested} scenarios."
                    : $"{report.Anomalies.Count} issue(s) across {report.ScenariosTested} scenarios.";
                ValidationSummary.Foreground = report.Valid ? Theme.Ok : Theme.Warn;
                ValidationList.ItemsSource = report.Anomalies
                    .OrderBy(a => a.Type == "Error" ? 0 : 1)
                    .Select(a => new ValidationRow(
                        (a.Type == "Error" ? "✖ " : "⚠ ") + a.Rule + ": " + a.Message,
                        a.Type == "Error" ? Theme.Error : Theme.Warn))
                    .ToList();
            }
            catch (BlueprintLoadException ex)
            {
                ValidationSummary.Text = "The blueprint is not structurally valid.";
                ValidationSummary.Foreground = Theme.Error;
                ValidationList.ItemsSource = ex.Errors.Select(m => new ValidationRow("✖ " + m, Theme.Error)).ToList();
                ErrorDiagnosticsService.Instance.ReportException("Blueprint Validation", ex, string.Join("\n", ex.Errors));
            }
            catch (Exception ex) when (ex is PricingEngineException or DllNotFoundException or BadImageFormatException)
            {
                ValidationSummary.Text = "Validation failed: " + ex.Message;
                ValidationSummary.Foreground = Theme.Error;
                ErrorDiagnosticsService.Instance.ReportException("Blueprint Validation", ex, $"Blueprint JSON:\n{json}");
            }
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
        internal string JsonEditorText { get => JsonEditor.Text; set => JsonEditor.Text = value; }
        internal string YamlEditorText { get => YamlEditor.Text; set => YamlEditor.Text = value; }
        internal int CardCount => RulesPanel.Children.OfType<RuleCard>().Count();
        internal IReadOnlyDictionary<string, object> Values => _values;
        internal int ScenarioCount => _scenarios.Count;
        internal string StatusMessage => StatusText.Text;
        internal Visibility ErrorButtonVisibility => ErrorDetailsButton.Visibility;
        internal string ErrorButtonText => (string)ErrorDetailsButton.Content;

        internal void SetValue(string key, object value)
        {
            _values[key] = value;
            _varSignature = "";
            RefreshSimulation();
        }

        internal void ApplyJsonEditorForTesting() => ApplyJsonEditor();
        internal void RecomputeForTesting(string json) => Recompute(json);
    }
}
