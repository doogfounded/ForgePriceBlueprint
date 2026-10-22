using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Forge.Services
{
    /// <summary>JSON &lt;-&gt; YAML conversion that preserves scalar types (numbers, booleans, nulls, strings).</summary>
    public static class YamlConverter
    {
        private static readonly Regex NumberPattern = new(@"^[-+]?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?$", RegexOptions.Compiled);

        public static string JsonToYaml(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var serializer = new SerializerBuilder().WithQuotingNecessaryStrings().Build();
            return serializer.Serialize(ToPlain(doc.RootElement));
        }

        public static string YamlToJson(string yaml)
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(yaml));
            if (stream.Documents.Count == 0)
            {
                throw new YamlException("The YAML document is empty.");
            }
            JsonNode? node = ToNode(stream.Documents[0].RootNode);
            return node?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? "null";
        }

        private static object? ToPlain(JsonElement e) => e.ValueKind switch
        {
            JsonValueKind.Object => e.EnumerateObject().ToDictionary(p => p.Name, p => ToPlain(p.Value)),
            JsonValueKind.Array => e.EnumerateArray().Select(ToPlain).ToList(),
            JsonValueKind.String => e.GetString(),
            JsonValueKind.Number => e.TryGetInt64(out var l) ? l : e.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };

        private static JsonNode? ToNode(YamlNode node)
        {
            switch (node)
            {
                case YamlMappingNode map:
                    var obj = new JsonObject();
                    foreach (var pair in map.Children)
                    {
                        string key = (pair.Key as YamlScalarNode)?.Value ?? throw new YamlException("Only scalar keys are supported.");
                        obj[key] = ToNode(pair.Value);
                    }
                    return obj;

                case YamlSequenceNode seq:
                    var arr = new JsonArray();
                    foreach (var child in seq.Children) arr.Add(ToNode(child));
                    return arr;

                case YamlScalarNode scalar:
                    return ToScalar(scalar);

                default:
                    return null;
            }
        }

        private static JsonNode? ToScalar(YamlScalarNode scalar)
        {
            string? v = scalar.Value;
            if (scalar.Style != ScalarStyle.Plain) return v ?? "";

            if (v is null or "" or "~" or "null" or "Null" or "NULL") return null;
            if (v.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (v.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;

            if (NumberPattern.IsMatch(v))
            {
                if (long.TryParse(v, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var l)) return l;
                if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
            }
            return v;
        }
    }
}
