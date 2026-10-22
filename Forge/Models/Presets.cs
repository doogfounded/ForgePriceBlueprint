namespace Forge.Models
{
    public sealed class ScenarioItem
    {
        public string Name { get; set; } = "";
        public Dictionary<string, object> Variables { get; set; } = new();
    }

    public sealed record Preset(
        string Id,
        string DisplayName,
        string Json,
        Dictionary<string, object> Context,
        List<ScenarioItem> Scenarios);

    public static class Presets
    {
        public static IReadOnlyList<Preset> All { get; } = new List<Preset>
        {
            new("enterprise", "Enterprise Contract", """
            {
              "Version": "1.0.0",
              "BlueprintName": "Enterprise Custom Contract",
              "Rules": [
                { "Type": "BasePrice", "Name": "Base Configuration Rate", "Enabled": true, "DefaultPrice": 250.0, "ContextKey": "base_price" },
                { "Type": "TieredPricing", "Name": "Volume License Tiers", "Enabled": true, "QuantityKey": "quantity", "Graduated": false,
                  "Tiers": [
                    { "MinQuantity": 10, "DiscountPercentage": 0.05 },
                    { "MinQuantity": 50, "DiscountPercentage": 0.10 },
                    { "MinQuantity": 100, "DiscountPercentage": 0.15 },
                    { "MinQuantity": 500, "DiscountPercentage": 0.25 }
                  ] },
                { "Type": "PercentageAdjustment", "Name": "Partner Discount", "Enabled": true, "Factor": 0.90, "ConditionKey": "is_partner", "Description": "Partner Channel 10% discount" },
                { "Type": "FlatAdjustment", "Name": "Shipping & Handling Surcharge", "Enabled": true, "Amount": 15.0, "ConditionKey": "quantity < 10", "Description": "Flat rate standard shipping fee for small orders" },
                { "Type": "PercentageAdjustment", "Name": "International Tax Surcharge", "Enabled": true, "Factor": 1.15, "ConditionKey": "region != US", "Description": "International regional sales tax of 15%" },
                { "Type": "PriceCap", "Name": "Contract Price Floor Cap", "Enabled": true, "MinPrice": 130.0, "MaxPrice": null }
              ]
            }
            """,
            new() { ["base_price"] = 250.0, ["quantity"] = 120.0, ["is_partner"] = false, ["region"] = "EU" },
            new()
            {
                new() { Name = "Scenario A: Regular Small Order", Variables = new() { ["base_price"] = 150.0, ["quantity"] = 5.0, ["region"] = "US", ["is_partner"] = false } },
                new() { Name = "Scenario B: Enterprise Bulk Order", Variables = new() { ["base_price"] = 150.0, ["quantity"] = 120.0, ["region"] = "EU", ["is_partner"] = false } },
                new() { Name = "Scenario C: Partner Promo", Variables = new() { ["base_price"] = 150.0, ["quantity"] = 50.0, ["region"] = "US", ["is_partner"] = true } },
            }),

            new("saas", "SaaS Subscription", """
            {
              "Version": "1.0.0",
              "BlueprintName": "SaaS Tiered Subscription",
              "Rules": [
                { "Type": "BasePrice", "Name": "SaaS Base Subscription", "Enabled": true, "DefaultPrice": 50.0, "ContextKey": "plan_base_price" },
                { "Type": "TieredPricing", "Name": "User Seats Tiered Pricing", "Enabled": true, "QuantityKey": "seats", "Graduated": false,
                  "Tiers": [
                    { "MinQuantity": 5, "DiscountPercentage": 0.05 },
                    { "MinQuantity": 10, "DiscountPercentage": 0.10 },
                    { "MinQuantity": 25, "DiscountPercentage": 0.20 },
                    { "MinQuantity": 100, "DiscountPercentage": 0.35 }
                  ] },
                { "Type": "FlatAdjustment", "Name": "Dedicated Support Add-on", "Enabled": true, "Amount": 150.0, "ConditionKey": "is_enterprise", "Description": "Dedicated enterprise support engineer SLA surcharge" },
                { "Type": "PercentageAdjustment", "Name": "Annual Billing Discount", "Enabled": true, "Factor": 0.833, "ConditionKey": "billing_interval == annual", "Description": "Get 2 months free with annual subscription" },
                { "Type": "PriceCap", "Name": "Subscription Price Floor", "Enabled": true, "MinPrice": 30.0, "MaxPrice": null }
              ]
            }
            """,
            new() { ["plan_base_price"] = 50.0, ["seats"] = 25.0, ["is_enterprise"] = false, ["billing_interval"] = "annual" },
            new()),

            new("ecommerce", "E-commerce Promo", """
            {
              "Version": "1.0.0",
              "BlueprintName": "E-commerce Holiday Promo",
              "Rules": [
                { "Type": "BasePrice", "Name": "Cart Checkout Price", "Enabled": true, "DefaultPrice": 100.0, "ContextKey": "cart_total" },
                { "Type": "PercentageAdjustment", "Name": "Holiday Coupon Promo", "Enabled": true, "Factor": 0.75, "ConditionKey": "coupon_code == HOLIDAY25", "Description": "Apply 25% Christmas coupon discount" },
                { "Type": "PercentageAdjustment", "Name": "VIP Customer Discount", "Enabled": true, "Factor": 0.95, "ConditionKey": "is_vip", "Description": "Additional 5% discount for loyalty members" },
                { "Type": "FlatAdjustment", "Name": "Free Shipping Discount", "Enabled": true, "Amount": -10.0, "ConditionKey": "cart_total >= 50", "Description": "Waive shipping charge for orders over $50" },
                { "Type": "FlatAdjustment", "Name": "Standard Shipping Surcharge", "Enabled": true, "Amount": 10.0, "ConditionKey": "cart_total < 50", "Description": "Add standard shipping and handling fee" }
              ]
            }
            """,
            new() { ["cart_total"] = 100.0, ["coupon_code"] = "HOLIDAY25", ["is_vip"] = true },
            new()),
        };

        public static Preset Get(string id) => All.First(p => p.Id == id);
    }
}
