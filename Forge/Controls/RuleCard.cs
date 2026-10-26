using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Forge.Models;

namespace Forge.Controls
{
    /// <summary>
    /// Editor card for one pricing rule. Edits mutate the rule's JsonObject in place and raise
    /// <see cref="Edited"/>; structural actions (move/remove) are delegated to the owner.
    /// </summary>
    public sealed class RuleCard : Border
    {
        private const string ConditionHint =
            "A boolean flag (is_partner) or a comparison: quantity >= 10, region != US, tier IN gold, silver.\n" +
            "Combine with && and ||, group with ( ).\nA bare number key (e.g. 'quantity') is never true.";

        private readonly JsonObject _rule;
        private readonly StackPanel _body = new() { Margin = new Thickness(0, 6, 0, 0) };
        private readonly TextBlock _title = new() { FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };

        public event Action? Edited;
        public event Action<RuleCard, int>? MoveRequested;
        public event Action<RuleCard>? RemoveRequested;
        public event Action<RuleCard, bool>? ExpandedChanged;

        public JsonObject Rule => _rule;

        public RuleCard(JsonObject rule, bool expanded)
        {
            _rule = rule;
            string type = BlueprintDocument.AsString(rule["Type"]) ?? "";

            Background = Theme.Panel;
            BorderBrush = Theme.Border;
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(8);
            Margin = new Thickness(0, 0, 0, 8);
            Padding = new Thickness(8, 6, 8, 8);

            _title.Text = BlueprintDocument.AsString(rule["Name"]) ?? "(unnamed)";
            _body.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;

            var stack = new StackPanel();
            stack.Children.Add(BuildHeader(type, expanded));
            BuildBody(type);
            stack.Children.Add(_body);
            Child = stack;
        }

        // ---------- header ----------

        private UIElement BuildHeader(string type, bool expanded)
        {
            var dock = new DockPanel { LastChildFill = true };

            var remove = IconButton("✕", "Remove rule");
            remove.Foreground = Theme.Error;
            remove.Click += (_, _) => RemoveRequested?.Invoke(this);
            var down = IconButton("▼", "Move down");
            down.Click += (_, _) => MoveRequested?.Invoke(this, +1);
            var up = IconButton("▲", "Move up");
            up.Click += (_, _) => MoveRequested?.Invoke(this, -1);
            foreach (var b in new[] { remove, down, up }) { DockPanel.SetDock(b, Dock.Right); dock.Children.Add(b); }

            var chevron = IconButton(expanded ? "▾" : "▸", "Expand / collapse");
            chevron.Click += (_, _) =>
            {
                bool open = _body.Visibility != Visibility.Visible;
                _body.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
                chevron.Content = open ? "▾" : "▸";
                ExpandedChanged?.Invoke(this, open);
            };
            DockPanel.SetDock(chevron, Dock.Left);
            dock.Children.Add(chevron);

            var enabled = new CheckBox
            {
                IsChecked = BlueprintDocument.AsBool(_rule["Enabled"], true),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 8, 0),
                ToolTip = "Enabled"
            };
            enabled.Click += (_, _) => { _rule["Enabled"] = enabled.IsChecked == true; Edited?.Invoke(); };
            DockPanel.SetDock(enabled, Dock.Left);
            dock.Children.Add(enabled);

            var chip = new Border
            {
                Background = Theme.AccentSoft,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 1, 8, 1),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = Friendly(type), Foreground = Theme.Accent, FontSize = 11, FontWeight = FontWeights.SemiBold }
            };
            DockPanel.SetDock(chip, Dock.Left);
            dock.Children.Add(chip);

            dock.Children.Add(_title);
            return dock;
        }

        public static string Friendly(string type) => type switch
        {
            "BasePrice" => "Base Price",
            "TieredPricing" => "Volume Tiers",
            "PercentageAdjustment" => "% Adjustment",
            "FlatAdjustment" => "Flat Adjustment",
            "PriceCap" => "Price Cap",
            "Rounding" => "Rounding",
            _ => type
        };

        private static Button IconButton(string glyph, string tip) => new()
        {
            Content = glyph,
            ToolTip = tip,
            Width = 24,
            Height = 24,
            Padding = new Thickness(0),
            Margin = new Thickness(1, 0, 1, 0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            FontSize = 11
        };

        // ---------- body ----------

        private void BuildBody(string type)
        {
            var name = TextBoxFor(_rule, "Name");
            name.TextChanged += (_, _) => _title.Text = string.IsNullOrWhiteSpace(name.Text) ? "(unnamed)" : name.Text;
            Row("Name", name);

            switch (type)
            {
                case "BasePrice":
                    Row("Default price", NumberBoxFor(_rule, "DefaultPrice", false), "Used when the context has no value for the key below.");
                    Row("Context key", TextBoxFor(_rule, "ContextKey"), "Simulator input that supplies the starting price.");
                    break;

                case "PercentageAdjustment":
                    Row("Factor", NumberBoxFor(_rule, "Factor", false), "Multiplier: 0.90 = 10% off, 1.15 = 15% markup.");
                    Row("Condition", TextBoxFor(_rule, "ConditionKey"), ConditionHint);
                    Row("Description", TextBoxFor(_rule, "Description"));
                    break;

                case "FlatAdjustment":
                    Row("Amount", NumberBoxFor(_rule, "Amount", false), "Added to the price (negative to subtract).");
                    Row("Condition", TextBoxFor(_rule, "ConditionKey"), ConditionHint);
                    Row("Description", TextBoxFor(_rule, "Description"));
                    break;

                case "Rounding":
                    var combo = new ComboBox { ItemsSource = BlueprintDocument.RoundingModes, Padding = new Thickness(4, 3, 4, 3) };
                    combo.SelectedItem = BlueprintDocument.AsString(_rule["RoundingMode"]);
                    combo.SelectionChanged += (_, _) =>
                    {
                        if (combo.SelectedItem is string s) { _rule["RoundingMode"] = s; Edited?.Invoke(); }
                    };
                    Row("Mode", combo);
                    break;
            }
        }

        private void Row(string label, FrameworkElement editor, string? tip = null)
        {
            var grid = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            var text = new TextBlock { Text = label, Foreground = Theme.Muted, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(editor, 1);
            grid.Children.Add(text);
            grid.Children.Add(editor);
            if (tip != null) { editor.ToolTip = tip; text.ToolTip = tip; }
            _body.Children.Add(grid);
        }

        private TextBox TextBoxFor(JsonObject target, string key)
        {
            var box = new TextBox { Text = BlueprintDocument.AsString(target[key]) ?? "", Padding = new Thickness(4, 3, 4, 3) };
            box.TextChanged += (_, _) => { target[key] = box.Text; Edited?.Invoke(); };
            return box;
        }

        private TextBox NumberBoxFor(JsonObject target, string key, bool nullable)
        {
            double? current = BlueprintDocument.AsNumber(target[key]);
            var box = new TextBox
            {
                Text = current?.ToString("R", CultureInfo.InvariantCulture) ?? "",
                Padding = new Thickness(4, 3, 4, 3)
            };
            box.TextChanged += (_, _) =>
            {
                string t = box.Text.Trim();
                if (nullable && t.Length == 0)
                {
                    target[key] = null;
                    MarkValid(box, true);
                    Edited?.Invoke();
                }
                else if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d))
                {
                    target[key] = d;
                    MarkValid(box, true);
                    Edited?.Invoke();
                }
                else
                {
                    MarkValid(box, false); // keep the last good value in the document
                }
            };
            return box;
        }

        private static void MarkValid(Control box, bool valid)
        {
            box.Background = valid ? Brushes.White : Theme.ErrorSoft;
            box.ToolTip = valid ? box.ToolTip : "Enter a number (use . for decimals).";
        }


    }
}
