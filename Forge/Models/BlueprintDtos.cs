using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Forge
{
    // C# representation of a pricing rule
    [JsonDerivedType(typeof(BasePriceRuleDto))]
    [JsonDerivedType(typeof(PercentageAdjustmentRuleDto))]
    [JsonDerivedType(typeof(FlatAdjustmentRuleDto))]
    [JsonDerivedType(typeof(PriceCapRuleDto))]
    [JsonDerivedType(typeof(TieredPricingRuleDto))]
    [JsonDerivedType(typeof(RoundingRuleDto))]
    public abstract class PricingRuleDto
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public bool Enabled { get; set; } = true;

        protected PricingRuleDto(string type, string name, bool enabled = true)
        {
            Type = type;
            Name = name;
            Enabled = enabled;
        }
    }

    public class BasePriceRuleDto : PricingRuleDto
    {
        public double DefaultPrice { get; set; }
        public string ContextKey { get; set; }

        public BasePriceRuleDto(string name, double defaultPrice, string contextKey = "base_price", bool enabled = true) 
            : base("BasePrice", name, enabled)
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

        public PercentageAdjustmentRuleDto(string name, double factor, string conditionKey, string description, bool enabled = true) 
            : base("PercentageAdjustment", name, enabled)
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

        public FlatAdjustmentRuleDto(string name, double amount, string conditionKey, string description, bool enabled = true) 
            : base("FlatAdjustment", name, enabled)
        {
            Amount = amount;
            ConditionKey = conditionKey;
            Description = description;
        }
    }

    public class PriceCapRuleDto : PricingRuleDto
    {
        public double? MinPrice { get; set; }
        public double? MaxPrice { get; set; }

        public PriceCapRuleDto(string name, double? minPrice = null, double? maxPrice = null, bool enabled = true) 
            : base("PriceCap", name, enabled)
        {
            MinPrice = minPrice;
            MaxPrice = maxPrice;
        }
    }

    public class TieredPricingRuleDto : PricingRuleDto
    {
        public string QuantityKey { get; set; }
        public bool Graduated { get; set; } = false;
        public List<TierDto> Tiers { get; set; } = new();

        public TieredPricingRuleDto(string name, string quantityKey, bool graduated = false, bool enabled = true) 
            : base("TieredPricing", name, enabled)
        {
            QuantityKey = quantityKey;
            Graduated = graduated;
        }
    }

    public class RoundingRuleDto : PricingRuleDto
    {
        public string RoundingMode { get; set; }

        public RoundingRuleDto(string name, string roundingMode, bool enabled = true) 
            : base("Rounding", name, enabled)
        {
            RoundingMode = roundingMode;
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
        public string Version { get; set; } = "1.0.0";
        public string BlueprintName { get; set; }
        public List<PricingRuleDto> Rules { get; set; } = new();

        public PriceBlueprintDto(string name, string version = "1.0.0")
        {
            BlueprintName = name;
            Version = version;
        }
    }
}
