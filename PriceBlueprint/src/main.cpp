#include "../include/PriceBlueprint.h"
#include <iostream>
#include <memory>
#include <fstream>

int main() {
    using namespace Pricing;

    std::cout << "Starting PriceBlueprint Core Engine...\n\n";

    // 1. Load the blueprint dynamically from JSON
    std::shared_ptr<PriceBlueprint> blueprint;
    try {
        std::string paths[] = {
            "ForgePriceBlueprint/enterprise_blueprint.json",
            "../enterprise_blueprint.json",
            "../../enterprise_blueprint.json",
            "enterprise_blueprint.json"
        };
        std::string foundPath = "";
        for (const auto& path : paths) {
            std::ifstream f(path);
            if (f.good()) {
                foundPath = path;
                break;
            }
        }
        if (foundPath.empty()) {
            throw std::runtime_error("Could not find enterprise_blueprint.json in any search path.");
        }
        std::cout << "Loading blueprint from: " << foundPath << "\n\n";
        blueprint = PriceBlueprint::LoadFromFile(foundPath);
    } catch (const std::exception& e) {
        std::cerr << "Error loading blueprint: " << e.what() << "\n";
        return 1;
    }

    // 2. Set up context scenario A: Regular order, small quantity (< 10)
    {
        std::cout << "--- SCENARIO A: Regular Customer, Qty 5, Shipping Surcharge (Relational) ---\n";
        PricingContext context;
        context.Set("base_price", 150.0);
        context.Set("quantity", 5.0);
        context.Set("is_vip", false);
        context.Set("is_holiday", false);
        context.Set("region", std::string("US"));

        PriceResult result = blueprint->Calculate(context);
        result.Print();
        std::cout << "\n";
    }

    // 3. Set up context scenario B: Bulk order, International region (region != US)
    {
        std::cout << "--- SCENARIO B: Bulk Order, Qty 120, International Region (EU) ---\n";
        PricingContext context;
        context.Set("base_price", 150.0);
        context.Set("quantity", 120.0);
        context.Set("region", std::string("EU"));

        PriceResult result = blueprint->Calculate(context);
        result.Print();
        std::cout << "\n";
    }

    return 0;
}
