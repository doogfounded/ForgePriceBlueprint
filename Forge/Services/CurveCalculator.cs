using System.Text.Json.Nodes;
using Forge.Models;

namespace Forge.Services
{
    public sealed record CurvePoint(double Quantity, double Price);

    public sealed record CurveData(string QuantityKey, IReadOnlyList<CurvePoint> Points, double CurrentQuantity);

    /// <summary>Samples the engine across a quantity range to draw the discount curve.</summary>
    public static class CurveCalculator
    {
        public static CurveData? Compute(PricingEngine engine, string blueprintJson,
            IReadOnlyDictionary<string, object> context, int samples = 60)
        {
            var rules = (JsonNode.Parse(blueprintJson) as JsonObject)?["Rules"] as JsonArray;
            var tiered = rules?.OfType<JsonObject>().FirstOrDefault(r =>
                BlueprintDocument.AsString(r["Type"]) == "TieredPricing" &&
                BlueprintDocument.AsBool(r["Enabled"], true));
            if (tiered == null) return null;

            string? key = BlueprintDocument.AsString(tiered["QuantityKey"]);
            if (string.IsNullOrWhiteSpace(key)) return null;

            var mins = (tiered["Tiers"] as JsonArray)?.OfType<JsonObject>()
                .Select(t => BlueprintDocument.AsNumber(t["MinQuantity"]) ?? 0).ToList() ?? new List<double>();

            double current = context.TryGetValue(key, out var c) && c is double d ? d : 1.0;
            double upper = Math.Max(Math.Max((mins.Count > 0 ? mins.Max() : 10) * 1.5, current * 1.1), 10);

            var quantities = new SortedSet<double> { Math.Round(current, 2) };
            for (int i = 0; i <= samples; i++) quantities.Add(Math.Round(1 + (upper - 1) * i / samples, 2));
            foreach (var m in mins)
            {
                quantities.Add(m);
                if (m > 1) quantities.Add(Math.Round(m - 0.01, 2));
            }

            var points = new List<CurvePoint>();
            try
            {
                foreach (double q in quantities)
                {
                    var ctx = new Dictionary<string, object>(context) { [key] = q };
                    points.Add(new CurvePoint(q, engine.Calculate(blueprintJson, ctx).FinalPrice));
                }
            }
            catch (PricingEngineException)
            {
                return null;
            }

            return new CurveData(key, points, current);
        }
    }
}
