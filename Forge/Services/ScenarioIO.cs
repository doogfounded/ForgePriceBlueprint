using System.Text.Json;
using System.Text.Json.Nodes;
using Forge.Models;

namespace Forge.Services
{
    /// <summary>Scenario import/export using the same shape the old API used: [{scenario_name, context}].</summary>
    public static class ScenarioIO
    {
        public static string Serialize(IEnumerable<ScenarioItem> scenarios)
        {
            var arr = new JsonArray();
            foreach (var s in scenarios)
            {
                var ctx = new JsonObject();
                foreach (var kv in s.Variables)
                {
                    ctx[kv.Key] = kv.Value switch
                    {
                        double d => d,
                        bool b => b,
                        _ => kv.Value?.ToString()
                    };
                }
                arr.Add(new JsonObject { ["scenario_name"] = s.Name, ["context"] = ctx });
            }
            return arr.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        public static List<ScenarioItem> Parse(string json)
        {
            if (JsonNode.Parse(json) is not JsonArray arr)
            {
                throw new JsonException("Scenarios must be a JSON array.");
            }

            var result = new List<ScenarioItem>();
            int n = 0;
            foreach (var node in arr.OfType<JsonObject>())
            {
                n++;
                var item = new ScenarioItem { Name = BlueprintDocument.AsString(node["scenario_name"]) ?? $"Scenario {n}" };
                if (node["context"] is JsonObject ctx)
                {
                    foreach (var kv in ctx)
                    {
                        if (kv.Value is not JsonValue v) continue;
                        if (v.TryGetValue<bool>(out var b)) item.Variables[kv.Key] = b;
                        else if (v.TryGetValue<double>(out var d)) item.Variables[kv.Key] = d;
                        else if (v.TryGetValue<string>(out var s)) item.Variables[kv.Key] = s;
                    }
                }
                result.Add(item);
            }
            return result;
        }
    }
}
