using System.Text.Json;
using System.Text.Json.Nodes;

namespace Forge.Models
{
    /// <summary>
    /// The blueprint being edited, held as a JSON tree so the designer, the JSON editor and the
    /// engine all see exactly the same document (and unknown properties survive round trips).
    /// </summary>
    public class BlueprintDocument
    {
        public static readonly string[] RuleTypes =
        {
            "BasePrice", "PercentageAdjustment", "FlatAdjustment", "Rounding"
        };

        public static readonly string[] RoundingModes =
        {
            "NearestDollar", "EndsIn99", "EndsIn95", "NearestNickel", "NearestDime"
        };

        private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

        private JsonObject _root = CreateRoot("New Blueprint");

        /// <summary>Raised after any change. The argument is true when the set/order of rules changed.</summary>
        public event Action<bool>? Changed;

        public JsonObject Root => _root;

        public string Name
        {
            get => AsString(_root["BlueprintName"]) ?? "";
            set
            {
                if (Name == value) return;
                _root["BlueprintName"] = value;
                Changed?.Invoke(false);
            }
        }

        public JsonArray Rules
        {
            get
            {
                if (_root["Rules"] is not JsonArray rules)
                {
                    rules = new JsonArray();
                    _root["Rules"] = rules;
                }
                return rules;
            }
        }

        public IEnumerable<JsonObject> RuleObjects => Rules.OfType<JsonObject>();

        public static BlueprintDocument CreateBlank()
        {
            var doc = new BlueprintDocument();
            doc.Rules.Add(CreateRule("BasePrice"));
            return doc;
        }

        public void Load(string json)
        {
            if (JsonNode.Parse(json) is not JsonObject obj)
            {
                throw new JsonException("Blueprint must be a JSON object.");
            }
            _root = obj;
            _ = Rules; // normalise a missing Rules array
            Changed?.Invoke(true);
        }

        public string ToJson() => _root.ToJsonString(Indented);

        /// <summary>Called by editors after they mutate a rule's fields in place.</summary>
        public void NotifyEdited() => Changed?.Invoke(false);

        public JsonObject AddRule(string type)
        {
            var rule = CreateRule(type);
            Rules.Add(rule);
            Changed?.Invoke(true);
            return rule;
        }

        public void RemoveRule(JsonObject rule)
        {
            if (Rules.Remove(rule)) Changed?.Invoke(true);
        }

        public bool MoveRule(JsonObject rule, int delta)
        {
            int index = Rules.IndexOf(rule);
            int target = index + delta;
            if (index < 0 || target < 0 || target >= Rules.Count) return false;

            Rules.RemoveAt(index);
            Rules.Insert(target, rule);
            Changed?.Invoke(true);
            return true;
        }

        public static JsonObject CreateRoot(string name) => new()
        {
            ["Version"] = "1.0.0",
            ["BlueprintName"] = name,
            ["Rules"] = new JsonArray()
        };

        public static JsonObject CreateRule(string type) => type switch
        {
            "BasePrice" => new JsonObject
            {
                ["Type"] = "BasePrice", ["Name"] = "Base Configuration Rate", ["Enabled"] = true,
                ["DefaultPrice"] = 250.0, ["ContextKey"] = "base_price"
            },
            "PercentageAdjustment" => new JsonObject
            {
                ["Type"] = "PercentageAdjustment", ["Name"] = "Partner Discount", ["Enabled"] = true,
                ["Factor"] = 0.90, ["ConditionKey"] = "is_partner", ["Description"] = "Partner Channel 10% discount"
            },
            "FlatAdjustment" => new JsonObject
            {
                ["Type"] = "FlatAdjustment", ["Name"] = "Shipping Surcharge", ["Enabled"] = true,
                ["Amount"] = 15.0, ["ConditionKey"] = "quantity < 10", ["Description"] = "Flat rate standard shipping fee for small orders"
            },
            "Rounding" => new JsonObject
            {
                ["Type"] = "Rounding", ["Name"] = "Charm Rounding", ["Enabled"] = true,
                ["RoundingMode"] = "EndsIn99"
            },
            _ => throw new ArgumentException($"Unknown rule type '{type}'.", nameof(type))
        };

        public static string? AsString(JsonNode? node) =>
            node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

        public static double? AsNumber(JsonNode? node) =>
            node is JsonValue v && v.TryGetValue<double>(out var d) ? d : null;

        public static bool AsBool(JsonNode? node, bool fallback) =>
            node is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;
    }
}
