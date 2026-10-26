using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MyPriceCalc.Controls;
using MyPriceCalc.Models;

namespace MyPriceCalc
{
    public partial class MainWindow : Window
    {
        private sealed record RoundingChoice(RoundingMode Mode, string Label)
        {
            public override string ToString() => Label;
        }

        private static readonly RoundingChoice[] RoundingChoices =
        {
            new(RoundingMode.None, "None"),
            new(RoundingMode.NearestDollar, "Nearest dollar"),
            new(RoundingMode.EndsIn99, "Ends in .99"),
            new(RoundingMode.EndsIn95, "Ends in .95"),
            new(RoundingMode.NearestNickel, "Nearest 5¢"),
            new(RoundingMode.NearestDime, "Nearest 10¢")
        };

        private readonly List<Adjustment> _rows = new();
        private readonly List<TextBlock> _rowPrices = new();
        private bool _suspended;

        public MainWindow()
        {
            InitializeComponent();

            RoundingBox.ItemsSource = RoundingChoices;
            RoundingBox.SelectedIndex = 0;

            BasePriceBox.Text = DefaultBase.ToString(CultureInfo.CurrentCulture);
            BasePriceBox.TextChanged += (_, _) => { if (!_suspended) UpdateResults(); };

            BuildRows();
            UpdateResults();
        }

        private const decimal DefaultBase = 100m;

        // =====================================================================
        // Rows
        // =====================================================================

        private void AddRow_Click(object sender, RoutedEventArgs e)
        {
            _rows.Add(new Adjustment());
            BuildRows();
            UpdateResults();
        }

        private void BuildRows()
        {
            RowsPanel.Children.Clear();
            _rowPrices.Clear();

            if (_rows.Count == 0)
            {
                RowsPanel.Children.Add(new TextBlock
                {
                    Text = "No adjustments. Add one to discount, mark up, or add a flat charge.",
                    Foreground = Theme.Muted,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(2, 8, 2, 0)
                });
                return;
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                RowsPanel.Children.Add(BuildRow(_rows[i], i));
            }
        }

        private Border BuildRow(Adjustment row, int index)
        {
            var price = new TextBlock
            {
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                MinWidth = 76,
                TextAlignment = TextAlignment.Right
            };
            _rowPrices.Add(price);

            var enabled = new CheckBox
            {
                IsChecked = row.Enabled,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
                ToolTip = "Apply this adjustment"
            };
            enabled.Click += (_, _) => { row.Enabled = enabled.IsChecked == true; UpdateResults(); };

            var amount = new TextBox
            {
                Text = row.Amount.ToString("0.##", CultureInfo.CurrentCulture),
                Padding = new Thickness(5, 4, 5, 4),
                VerticalAlignment = VerticalAlignment.Center,
                Width = 92,
                TextAlignment = TextAlignment.Right,
                ToolTip = row.Kind == AdjustmentKind.Percent
                    ? "Percent of the running price: -15 = 15% off, 10 = 10% markup."
                    : "Amount added to the running price: -15 = $15 off, 4.99 = $4.99 shipping."
            };
            amount.TextChanged += (_, _) =>
            {
                bool ok = decimal.TryParse(amount.Text.Trim(), NumberStyles.Currency, CultureInfo.CurrentCulture, out var parsed)
                          && Math.Abs(parsed) <= 1_000_000m;
                amount.Background = ok ? Brushes.White : Theme.ErrorSoft;
                amount.ToolTip = ok ? amount.ToolTip : "Enter a number, e.g. -15 or 4.99";
                if (!ok) return;
                row.Amount = parsed;
                UpdateResults();
            };

            var kind = new ComboBox
            {
                ItemsSource = new[] { "%", "$" },
                SelectedIndex = row.Kind == AdjustmentKind.Percent ? 0 : 1,
                Width = 56,
                Padding = new Thickness(4, 3, 4, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                ToolTip = "Percent or flat amount"
            };
            kind.SelectionChanged += (_, _) =>
            {
                row.Kind = kind.SelectedIndex == 0 ? AdjustmentKind.Percent : AdjustmentKind.Flat;
                amount.ToolTip = row.Kind == AdjustmentKind.Percent
                    ? "Percent of the running price: -15 = 15% off, 10 = 10% markup."
                    : "Amount added to the running price: -15 = $15 off, 4.99 = $4.99 shipping.";
                UpdateResults();
            };

            var up = IconButton("▲", "Move up", index > 0);
            up.Click += (_, _) => Move(index, -1);
            var down = IconButton("▼", "Move down", index < _rows.Count - 1);
            down.Click += (_, _) => Move(index, +1);
            var remove = IconButton("✕", "Remove", true);
            remove.Foreground = Theme.Error;
            remove.Click += (_, _) => { _rows.RemoveAt(index); BuildRows(); UpdateResults(); };

            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            actions.Children.Add(up);
            actions.Children.Add(down);
            actions.Children.Add(remove);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(enabled, 0); grid.Children.Add(enabled);
            Grid.SetColumn(amount, 1); grid.Children.Add(amount);
            Grid.SetColumn(kind, 2); grid.Children.Add(kind);
            Grid.SetColumn(price, 3); grid.Children.Add(price);
            Grid.SetColumn(actions, 4); grid.Children.Add(actions);

            return new Border
            {
                Background = Theme.Panel,
                BorderBrush = Theme.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, row.Enabled ? 8 : 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 8),
                Opacity = row.Enabled ? 1.0 : 0.6,
                Child = grid
            };
        }

        private static Button IconButton(string glyph, string tip, bool enabled) => new()
        {
            Content = glyph,
            ToolTip = tip,
            Width = 24,
            Height = 24,
            Padding = new Thickness(0),
            Margin = new Thickness(1, 0, 0, 0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            FontSize = 11,
            IsEnabled = enabled
        };

        private void Move(int index, int delta)
        {
            int target = index + delta;
            if (target < 0 || target >= _rows.Count) return;

            (_rows[index], _rows[target]) = (_rows[target], _rows[index]);
            BuildRows();
            UpdateResults();
        }

        // =====================================================================
        // Calculation
        // =====================================================================

        private RoundingMode CurrentRounding =>
            RoundingBox.SelectedItem is RoundingChoice choice ? choice.Mode : RoundingMode.None;

        private void Rounding_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_suspended) UpdateResults();
        }

        private void UpdateResults()
        {
            if (!TryReadBase(out decimal basePrice))
            {
                BasePriceBox.Background = Theme.ErrorSoft;
                ClearResult();
                return;
            }
            BasePriceBox.Background = Brushes.White;

            var result = Calculator.Evaluate(basePrice, _rows, CurrentRounding);

            for (int i = 0; i < _rowPrices.Count && i < result.Rows.Count; i++)
            {
                bool applied = result.Rows[i].Row.Enabled;
                _rowPrices[i].Text = Money(result.Rows[i].After);
                _rowPrices[i].Foreground = applied ? Theme.Text : Theme.Muted;
            }

            TotalText.Text = Money(result.Total);
            BaseEchoText.Text = Money(result.BasePrice);
            BeforeRoundingText.Text = Money(result.BeforeRounding);

            decimal change = result.BeforeRounding - result.BasePrice;
            AdjustEchoText.Text = change == 0
                ? "none"
                : $"{Money(change)} ({(result.BasePrice == 0 ? 0 : change / result.BasePrice * 100m).ToString("0.#", CultureInfo.CurrentCulture)}%)";
            AdjustEchoText.Foreground = change < 0 ? Theme.Ok : change > 0 ? Theme.Warn : Theme.Text;

            decimal delta = result.Total - result.BasePrice;
            DeltaText.Text = delta == 0
                ? "No change from the starting price."
                : (delta > 0 ? "▲ " : "▼ ") + Money(Math.Abs(delta)) + " vs. the starting price.";

            UpdateRowOpacity();
        }

        /// <summary>Rebuilt cards carry the row's enabled state, so opacity is reapplied after edits too.</summary>
        private void UpdateRowOpacity()
        {
            for (int i = 0; i < RowsPanel.Children.Count && i < _rows.Count; i++)
            {
                if (RowsPanel.Children[i] is Border card) card.Opacity = _rows[i].Enabled ? 1.0 : 0.6;
            }
        }

        private bool TryReadBase(out decimal value) =>
            decimal.TryParse(BasePriceBox.Text.Trim(), NumberStyles.Currency, CultureInfo.CurrentCulture, out value)
            && Math.Abs(value) <= 1_000_000_000m;

        private void ClearResult()
        {
            TotalText.Text = "—";
            DeltaText.Text = "Enter a starting price.";
            BaseEchoText.Text = "—";
            AdjustEchoText.Text = "—";
            BeforeRoundingText.Text = "—";
            foreach (var label in _rowPrices) label.Text = "—";
        }

        internal static string Money(decimal value) =>
            value.ToString("$#,##0.00;-$#,##0.00", CultureInfo.CurrentCulture);

        // =====================================================================
        // Reset & copy
        // =====================================================================

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            _suspended = true;
            _rows.Clear();
            BasePriceBox.Text = DefaultBase.ToString(CultureInfo.CurrentCulture);
            RoundingBox.SelectedIndex = 0;
            _suspended = false;

            BuildRows();
            UpdateResults();
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (!TryReadBase(out decimal basePrice)) return;

            var result = Calculator.Evaluate(basePrice, _rows, CurrentRounding);
            var lines = new List<string> { $"Starting price\t{Money(result.BasePrice)}" };

            foreach (var outcome in result.Rows)
            {
                string label = outcome.Row.Describe() + (outcome.Row.Enabled ? "" : " (off)");
                lines.Add($"{label}\t{Money(outcome.After)}");
            }

            if (CurrentRounding != RoundingMode.None)
            {
                string label = RoundingChoices.First(c => c.Mode == CurrentRounding).Label;
                lines.Add($"Rounding ({label})\t{Money(result.Total)}");
            }

            lines.Add($"Total\t{Money(result.Total)}");

            try
            {
                Clipboard.SetText(string.Join(Environment.NewLine, lines));
                CopyButton.Content = "Copied";
            }
            catch
            {
                CopyButton.Content = "Could not copy";
            }
        }

        // =====================================================================
        // Test hooks
        // =====================================================================

        internal string TotalDisplay => TotalText.Text;
        internal int RowCount => _rows.Count;
        internal IReadOnlyList<Adjustment> Rows => _rows;

        internal void SetBasePrice(string text)
        {
            BasePriceBox.Text = text;
            UpdateResults();
        }

        internal void SetRounding(RoundingMode mode)
        {
            _suspended = true;
            RoundingBox.SelectedIndex = Array.FindIndex(RoundingChoices, c => c.Mode == mode);
            _suspended = false;
            UpdateResults();
        }

        internal void AddRow(Adjustment row)
        {
            _rows.Add(row);
            BuildRows();
            UpdateResults();
        }

        internal void ClickAddRow() => AddRow_Click(this, new RoutedEventArgs());
        internal void ClickReset() => Reset_Click(this, new RoutedEventArgs());
    }
}
