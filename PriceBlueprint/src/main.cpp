#include "../include/PriceBlueprint.h"
#include <iostream>
#include <memory>
#include <fstream>
#include <nlohmann/json.hpp>

int main(int argc, char* argv[]) {
    using namespace Pricing;

    if (argc > 2) {
        try {
            std::string blueprintPath = argv[1];
            std::string scenariosPath = argv[2];

            auto blueprint = PriceBlueprint::LoadFromFile(blueprintPath);

            std::ifstream f(scenariosPath);
            if (!f.good()) {
                throw std::runtime_error("Scenarios file not found: " + scenariosPath);
            }
            nlohmann::json scenariosJson;
            f >> scenariosJson;

            nlohmann::json resultsJson = nlohmann::json::array();

            for (const auto& scenarioItem : scenariosJson) {
                std::string scenarioName = scenarioItem.value("scenario_name", "Unnamed Scenario");
                PricingContext context;

                if (scenarioItem.contains("context") && scenarioItem["context"].is_object()) {
                    for (auto& [key, val] : scenarioItem["context"].items()) {
                        if (val.is_number()) {
                            context.Set(key, val.get<double>());
                        } else if (val.is_boolean()) {
                            context.Set(key, val.get<bool>());
                        } else if (val.is_string()) {
                            context.Set(key, val.get<std::string>());
                        }
                    }
                }

                PriceResult result = blueprint->Calculate(context);

                nlohmann::json resultJson;
                resultJson["scenarioName"] = scenarioName;
                resultJson["basePrice"] = result.basePrice;
                resultJson["finalPrice"] = result.finalPrice;

                nlohmann::json auditTrailJson = nlohmann::json::array();
                for (const auto& record : result.auditTrail) {
                    nlohmann::json recordJson;
                    recordJson["ruleName"] = record.ruleName;
                    recordJson["description"] = record.description;
                    recordJson["inputPrice"] = record.inputPrice;
                    recordJson["outputPrice"] = record.outputPrice;
                    recordJson["adjustment"] = record.adjustment;
                    auditTrailJson.push_back(recordJson);
                }
                resultJson["auditTrail"] = auditTrailJson;
                resultsJson.push_back(resultJson);
            }

            std::cout << resultsJson.dump(4) << std::endl;
            return 0;

        } catch (const std::exception& e) {
            nlohmann::json errorJson;
            errorJson["error"] = e.what();
            std::cerr << errorJson.dump() << std::endl;
            return 1;
        }
    }

    std::cout << "Starting PriceBlueprint Core Engine...\n\n";

    // 1. Load the blueprint dynamically from JSON
    std::shared_ptr<PriceBlueprint> blueprint;
    try {
        std::string foundPath = "";
        if (argc > 1) {
            foundPath = argv[1];
            std::ifstream f(foundPath);
            if (!f.good()) {
                throw std::runtime_error("Specified blueprint file not found: " + foundPath);
            }
        } else {
            std::string paths[] = {
                "ForgePriceBlueprint/enterprise_blueprint.json",
                "../enterprise_blueprint.json",
                "../../enterprise_blueprint.json",
                "enterprise_blueprint.json"
            };
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
        }
        blueprint = PriceBlueprint::LoadFromFile(foundPath);
        std::cout << "Loading blueprint from: " << foundPath << " (Schema Version: " << blueprint->GetVersion() << ")\n\n";
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
