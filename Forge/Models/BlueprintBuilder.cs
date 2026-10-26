namespace Forge
{
    // Fluent Builder for assembling the pricing blueprint
    public class BlueprintBuilder
    {
        private readonly PriceBlueprintDto _blueprint;

        public BlueprintBuilder(string name, string version = "1.0.0")
        {
            _blueprint = new PriceBlueprintDto(name, version);
        }

        public BlueprintBuilder SetBasePrice(string ruleName, double defaultPrice, string contextKey = "base_price", bool enabled = true)
        {
            _blueprint.Rules.Add(new BasePriceRuleDto(ruleName, defaultPrice, contextKey, enabled));
            return this;
        }

        public BlueprintBuilder AddPercentageAdjustment(string ruleName, double factor, string conditionKey, string description, bool enabled = true)
        {
            _blueprint.Rules.Add(new PercentageAdjustmentRuleDto(ruleName, factor, conditionKey, description, enabled));
            return this;
        }

        public BlueprintBuilder AddFlatAdjustment(string ruleName, double amount, string conditionKey, string description, bool enabled = true)
        {
            _blueprint.Rules.Add(new FlatAdjustmentRuleDto(ruleName, amount, conditionKey, description, enabled));
            return this;
        }


        public BlueprintBuilder AddRounding(string ruleName, string roundingMode, bool enabled = true)
        {
            _blueprint.Rules.Add(new RoundingRuleDto(ruleName, roundingMode, enabled));
            return this;
        }

        public PriceBlueprintDto Build()
        {
            return _blueprint;
        }
    }
}
