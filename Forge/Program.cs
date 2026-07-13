using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Forge
{
    // C# representation of a pricing rule
    [JsonDerivedType(typeof(BasePriceRuleDto))]
    [JsonDerivedType(typeof(PercentageAdjustmentRuleDto))]
    [JsonDerivedType(typeof(FlatAdjustmentRuleDto))]
    [JsonDerivedType(typeof(TieredPricingRuleDto))]
    public abstract class PricingRuleDto
    {
        public string Type { get; set; }
        public string Name { get; set; }

        protected PricingRuleDto(string type, string name)
        {
            Type = type;
            Name = name;
        }
    }

    public class BasePriceRuleDto : PricingRuleDto
    {
        public double DefaultPrice { get; set; }
        public string ContextKey { get; set; }

        public BasePriceRuleDto(string name, double defaultPrice, string contextKey = "base_price") 
            : base("BasePrice", name)
        {
            DefaultPrice = defaultPrice;
            ContextKey = contextKey;
        }
    }

    public class PercentageAdjustmentRuleDto : PricingRuleDto
    {
        public double Factor { get; set; }
        public string ConditionKey { get; set; }
        public string Description { get; set; }

        public PercentageAdjustmentRuleDto(string name, double factor, string conditionKey, string description) 
            : base("PercentageAdjustment", name)
        {
            Factor = factor;
            ConditionKey = conditionKey;
            Description = description;
        }
    }

    public class FlatAdjustmentRuleDto : PricingRuleDto
    {
        public double Amount { get; set; }
        public string ConditionKey { get; set; }
        public string Description { get; set; }

        public FlatAdjustmentRuleDto(string name, double amount, string conditionKey, string description) 
            : base("FlatAdjustment", name)
        {
            Amount = amount;
            ConditionKey = conditionKey;
            Description = description;
        }
    }

    public class TieredPricingRuleDto : PricingRuleDto
    {
        public string QuantityKey { get; set; }
        public List<TierDto> Tiers { get; set; } = new();

        public TieredPricingRuleDto(string name, string quantityKey) 
            : base("TieredPricing", name)
        {
            QuantityKey = quantityKey;
        }
    }

    public class TierDto
    {
        public double MinQuantity { get; set; }
        public double DiscountPercentage { get; set; }
    }

    // C# representation of the full blueprint
    public class PriceBlueprintDto
    {
        public string BlueprintName { get; set; }
        public List<PricingRuleDto> Rules { get; set; } = new();

        public PriceBlueprintDto(string name)
        {
            BlueprintName = name;
        }
    }

    // Fluent Builder for assembling the pricing blueprint
    public class BlueprintBuilder
    {
        private readonly PriceBlueprintDto _blueprint;

        public BlueprintBuilder(string name)
        {
            _blueprint = new PriceBlueprintDto(name);
        }

        public BlueprintBuilder SetBasePrice(string ruleName, double defaultPrice, string contextKey = "base_price")
        {
            _blueprint.Rules.Add(new BasePriceRuleDto(ruleName, defaultPrice, contextKey));
            return this;
        }

        public BlueprintBuilder AddPercentageAdjustment(string ruleName, double factor, string conditionKey, string description)
        {
            _blueprint.Rules.Add(new PercentageAdjustmentRuleDto(ruleName, factor, conditionKey, description));
            return this;
        }

        public BlueprintBuilder AddFlatAdjustment(string ruleName, double amount, string conditionKey, string description)
        {
            _blueprint.Rules.Add(new FlatAdjustmentRuleDto(ruleName, amount, conditionKey, description));
            return this;
        }

        public BlueprintBuilder AddVolumeTiers(string ruleName, string quantityKey, params (double minQty, double discountPct)[] tiers)
        {
            var rule = new TieredPricingRuleDto(ruleName, quantityKey);
            foreach (var (minQty, discountPct) in tiers)
            {
                rule.Tiers.Add(new TierDto { MinQuantity = minQty, DiscountPercentage = discountPct });
            }
            _blueprint.Rules.Add(rule);
            return this;
        }

        public PriceBlueprintDto Build()
        {
            return _blueprint;
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("=================================================");
            Console.WriteLine("   Forge: Pricing Operating System Assembler     ");
            Console.WriteLine("=================================================");

            // Assemble a pricing blueprint using the Fluent Builder
            var blueprint = new BlueprintBuilder("Enterprise Custom Contract")
                .SetBasePrice("Base Configuration Rate", 250.0)
                .AddVolumeTiers("Volume License Tiers", "quantity", 
                    (10, 0.05),   // 5% off at 10 units
                    (50, 0.10),   // 10% off at 50 units
                    (100, 0.15),  // 15% off at 100 units
                    (500, 0.25)   // 25% off at 500 units
                )
                .AddPercentageAdjustment("Partner Discount", 0.90, "is_partner", "Partner Channel 10% discount")
                .AddFlatAdjustment("Shipping & Handling Surcharge", 15.0, "apply_shipping", "Flat rate standard shipping fee")
                .AddPercentageAdjustment("Federal Tax Surcharge", 1.08, "apply_federal_tax", "Federal standard sales tax of 8%")
                .Build();

            // Configure JSON options for serialization
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            string jsonOutput = JsonSerializer.Serialize(blueprint, options);

            Console.WriteLine("\nGenerated Blueprint JSON Specification:\n");
            Console.WriteLine(jsonOutput);

            Console.WriteLine("\nWriting specification blueprint to disk...");
            string solutionRoot = GetSolutionRoot();
            string path = Path.Combine(solutionRoot, "enterprise_blueprint.json");
            File.WriteAllText(path, jsonOutput);
            Console.WriteLine($"Successfully saved to: {path}\n");
            Console.WriteLine("Forge successfully assembled the final pricing blueprint.");
        }

        private static string GetSolutionRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ForgePriceBlueprint.slnx")))
            {
                dir = dir.Parent;
            }
            return dir?.FullName ?? AppDomain.CurrentDomain.BaseDirectory;
        }
    }
}
