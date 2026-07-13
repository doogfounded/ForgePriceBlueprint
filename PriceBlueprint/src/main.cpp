#include "../include/PriceBlueprint.h"
#include <iostream>
#include <memory>

int main() {
    using namespace Pricing;

    std::cout << "Starting PriceBlueprint Core Engine...\n\n";

    // 1. Create a blueprint
    auto blueprint = std::make_shared<PriceBlueprint>("Enterprise Pricing Plan");

    // Rule 1: Set base price (defaults to 100.0 if not provided in context)
    blueprint->AddRule(std::make_shared<BasePriceRule>(
        "Base Price Setup", 
        100.0, 
        "base_price"
    ));

    // Rule 2: Tiered pricing volume discount (e.g. quantity tiers)
    std::vector<TieredPricingRule::Tier> volumeTiers = {
        { 10.0, 0.05 }, // >= 10 items: 5% off
        { 50.0, 0.10 }, // >= 50 items: 10% off
        { 100.0, 0.20 } // >= 100 items: 20% off
    };
    blueprint->AddRule(std::make_shared<TieredPricingRule>(
        "Volume Tier Discount",
        "quantity",
        volumeTiers
    ));

    // Rule 3: VIP Customer discount (additional 10% off)
    blueprint->AddRule(std::make_shared<PercentageAdjustmentRule>(
        "VIP Reward Program",
        0.90, // 10% discount
        "is_vip",
        "Applied 10% loyalty discount for VIP status."
    ));

    // Rule 4: Holiday Season promotion (additional 5% off)
    blueprint->AddRule(std::make_shared<PercentageAdjustmentRule>(
        "Holiday Season Special",
        0.95, // 5% discount
        "is_holiday",
        "Applied 5% seasonal holiday discount."
    ));

    // 2. Set up context scenario A: Regular order, small quantity
    {
        std::cout << "--- SCENARIO A: Regular Customer, Qty 5 ---\n";
        PricingContext context;
        context.Set("base_price", 150.0);
        context.Set("quantity", 5.0);
        context.Set("is_vip", false);
        context.Set("is_holiday", false);

        PriceResult result = blueprint->Calculate(context);
        result.Print();
        std::cout << "\n";
    }

    // 3. Set up context scenario B: VIP Customer, Bulk order (120 items) during Holiday Season
    {
        std::cout << "--- SCENARIO B: VIP Customer, Qty 120, Holiday Promotion ---\n";
        PricingContext context;
        context.Set("base_price", 150.0);
        context.Set("quantity", 120.0);
        context.Set("is_vip", true);
        context.Set("is_holiday", true);

        PriceResult result = blueprint->Calculate(context);
        result.Print();
        std::cout << "\n";
    }

    return 0;
}
