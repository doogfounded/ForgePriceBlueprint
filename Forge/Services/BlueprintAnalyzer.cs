using System.Text.Json;

namespace Forge.Services
{
    public sealed record Anomaly(string Type, string Rule, string Message);

    public sealed record ValidationReport(bool Valid, int ScenariosTested, IReadOnlyList<Anomaly> Anomalies);

    /// <summary>
    /// Runs a grid of scenarios through the engine and flags anomalies (ported from the old /api/validate).
    /// Scenarios are structured data rather than encoded in names, so no name parsing is needed.
    /// </summary>
    public class BlueprintAnalyzer
    {
        private readonly PricingEngine _engine;

        public BlueprintAnalyzer(PricingEngine engine) => _engine = engine;

        private sealed record Scenario(string Name, double BasePrice, double Quantity, string Region, string Mode, Dictionary<string, object> Context);

        public ValidationReport Analyze(string blueprintJson)
        {
            BlueprintFileService.Validate(blueprintJson);

            var quantityBoundaries = new List<double> { 1, 5, 10, 50, 100, 500 };
            var ruleNames = new List<string>();

            using (var doc = JsonDocument.Parse(blueprintJson))
            {
                if (doc.RootElement.TryGetProperty("Rules", out var rules))
                {
                    foreach (var rule in rules.EnumerateArray())
                    {
                        if (rule.TryGetProperty("Name", out var n) && n.GetString() is string name) ruleNames.Add(name);

                        string type = rule.TryGetProperty("Type", out var t) ? t.GetString() ?? "" : "";
                        if (type == "TieredPricing" && rule.TryGetProperty("Tiers", out var tiers))
                        {
                            foreach (var tier in tiers.EnumerateArray())
                            {
                                if (!tier.TryGetProperty("MinQuantity", out var mq)) continue;
                                double q = mq.GetDouble();
                                quantityBoundaries.Add(q);
                                if (q > 1) quantityBoundaries.Add(q - 1);
                                quantityBoundaries.Add(q + 1);
                            }
                        }
                    }
                }
            }

            var quantities = quantityBoundaries.Distinct().Where(q => q > 0).OrderBy(q => q).ToList();
            var scenarios = BuildGrid(quantities);

            var anomalies = new List<Anomaly>();
            var executed = new HashSet<string>();
            var nonZero = new HashSet<string>();
            var runs = new Dictionary<string, List<(double Qty, double Price)>>();

            foreach (var sc in scenarios)
            {
                var result = _engine.Calculate(blueprintJson, sc.Context);
                double bp = result.BasePrice, fp = result.FinalPrice;

                if (fp <= 0.0)
                {
                    anomalies.Add(new Anomaly("Error", "Negative/Zero Price", $"Zero or negative price calculated: ${fp:F2} (Base: ${bp:F2}) in scenario '{sc.Name}'"));
                }
                else if (fp < 0.2 * bp)
                {
                    anomalies.Add(new Anomaly("Warning", "Extreme Discount", $"Extreme discount: Final price ${fp:F2} is only {fp / bp * 100.0:F1}% of the base price (${bp:F2}) in scenario '{sc.Name}'"));
                }
                else if (fp > 3.0 * bp)
                {
                    anomalies.Add(new Anomaly("Warning", "Extreme Markup", $"Extreme markup: Final price ${fp:F2} is {fp / bp * 100.0:F1}% of the base price (${bp:F2}) in scenario '{sc.Name}'"));
                }

                foreach (var rec in result.AuditTrail)
                {
                    if (string.IsNullOrEmpty(rec.RuleName)) continue;
                    executed.Add(rec.RuleName);
                    if (Math.Abs(rec.Adjustment) > 0.001) nonZero.Add(rec.RuleName);
                }

                string runKey = $"{sc.BasePrice}_{sc.Region}_{sc.Mode}";
                if (!runs.TryGetValue(runKey, out var list)) runs[runKey] = list = new();
                list.Add((sc.Quantity, fp));
            }

            foreach (var run in runs)
            {
                var sorted = run.Value.OrderBy(r => r.Qty).ToList();
                for (int i = 0; i < sorted.Count; i++)
                {
                    for (int j = i + 1; j < sorted.Count; j++)
                    {
                        double total1 = sorted[i].Price * sorted[i].Qty;
                        double total2 = sorted[j].Price * sorted[j].Qty;
                        if (sorted[j].Qty > sorted[i].Qty && total2 < total1)
                        {
                            anomalies.Add(new Anomaly("Error", "Volume Inversion",
                                $"Volume Inversion: Ordering {sorted[j].Qty} units costs a total of ${total2:F2} (${sorted[j].Price:F2} each), which is cheaper than ordering {sorted[i].Qty} units at a total of ${total1:F2} (${sorted[i].Price:F2} each). Run context: {run.Key}"));
                        }
                    }
                }
            }

            foreach (var name in ruleNames)
            {
                if (!executed.Contains(name))
                {
                    anomalies.Add(new Anomaly("Warning", "Inactive Rule", $"Rule '{name}' was never executed in any simulated scenario. Verify if its conditional logic is satisfiable."));
                }
                else if (!nonZero.Contains(name))
                {
                    anomalies.Add(new Anomaly("Warning", "Redundant Rule", $"Rule '{name}' was executed but had zero effect (adjustment was always $0.00) in all simulated scenarios."));
                }
            }

            return new ValidationReport(anomalies.Count == 0, scenarios.Count, anomalies);
        }

        private static List<Scenario> BuildGrid(List<double> quantities)
        {
            var modes = new (string Mode, bool Vip, bool Partner, bool Holiday)[]
            {
                ("Std", false, false, false),
                ("VipPartner", true, true, false),
                ("Holiday", false, false, true),
            };

            var grid = new List<Scenario>();
            foreach (double bp in new[] { 100.0, 250.0 })
            foreach (double qty in quantities)
            foreach (string region in new[] { "US", "EU" })
            foreach (var m in modes)
            {
                grid.Add(new Scenario($"BP_{bp}_Qty_{qty}_Reg_{region}_{m.Mode}", bp, qty, region, m.Mode,
                    new Dictionary<string, object>
                    {
                        ["base_price"] = bp,
                        ["quantity"] = qty,
                        ["region"] = region,
                        ["is_vip"] = m.Vip,
                        ["is_partner"] = m.Partner,
                        ["is_holiday"] = m.Holiday,
                    }));
            }
            return grid;
        }
    }
}
