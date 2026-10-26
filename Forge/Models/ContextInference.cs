using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Forge.Models
{
    public enum ContextKind { Number, Bool, Text }

    public sealed record ContextVariable(string Key, ContextKind Kind, object Default);

    /// <summary>
    /// Works out which simulator inputs a blueprint needs by reading the rules the same way the
    /// engine's condition parser does (operators are located in the engine's precedence order).
    /// </summary>
    public static class ContextInference
    {
        // Same order the C++ ConditionParser searches in.
        private static readonly string[] Operators = { "==", "!=", ">=", "<=", ">", "<", " IN ", " in " };

        public static List<ContextVariable> Infer(JsonArray rules)
        {
            var result = new List<ContextVariable>();
            var seen = new HashSet<string>();

            void Add(string? key, ContextKind kind, object def)
            {
                key = key?.Trim();
                if (string.IsNullOrEmpty(key) || !seen.Add(key)) return;
                result.Add(new ContextVariable(key, kind, def));
            }

            foreach (var rule in rules.OfType<JsonObject>())
            {
                switch (BlueprintDocument.AsString(rule["Type"]))
                {
                    case "BasePrice":
                        Add(BlueprintDocument.AsString(rule["ContextKey"]) ?? "base_price",
                            ContextKind.Number, BlueprintDocument.AsNumber(rule["DefaultPrice"]) ?? 0.0);
                        break;


                    case "PercentageAdjustment":
                    case "FlatAdjustment":
                        foreach (var v in FromCondition(BlueprintDocument.AsString(rule["ConditionKey"]) ?? ""))
                            Add(v.Key, v.Kind, v.Default);
                        break;
                }
            }
            return result;
        }

        public static IEnumerable<ContextVariable> FromCondition(string expression)
        {
            foreach (var raw in Regex.Split(expression, @"&&|\|\|"))
            {
                string leaf = raw.Trim().Trim('(', ')', ' ', '\t');
                if (leaf.Length == 0) continue;

                string? op = null;
                int pos = -1;
                foreach (var candidate in Operators)
                {
                    pos = leaf.IndexOf(candidate, StringComparison.Ordinal);
                    if (pos >= 0) { op = candidate; break; }
                }

                if (op == null)
                {
                    yield return new ContextVariable(leaf, ContextKind.Bool, false);
                    continue;
                }

                string key = leaf[..pos].Trim();
                string value = leaf[(pos + op.Length)..].Trim();
                if (key.Length == 0) continue;

                if (op.Trim().Equals("IN", StringComparison.OrdinalIgnoreCase))
                {
                    var items = value.Split(',').Select(s => s.Trim().Trim('"')).Where(s => s.Length > 0).ToList();
                    if (items.Count > 0 && items.All(i => double.TryParse(i, NumberStyles.Float, CultureInfo.InvariantCulture, out _)))
                        yield return new ContextVariable(key, ContextKind.Number, double.Parse(items[0], CultureInfo.InvariantCulture));
                    else
                        yield return new ContextVariable(key, ContextKind.Text, items.FirstOrDefault() ?? "");
                }
                else if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                {
                    yield return new ContextVariable(key, ContextKind.Number, number);
                }
                else if (value is "true" or "false")
                {
                    yield return new ContextVariable(key, ContextKind.Bool, value == "true");
                }
                else
                {
                    string text = value.Trim('"');
                    yield return new ContextVariable(key, ContextKind.Text, op == "!=" ? "other" : text);
                }
            }
        }
    }
}
