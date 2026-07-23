#include "../include/PriceBlueprint.h"
#include <cstring>

#if defined(_WIN32) || defined(_WIN64)
    #define DLL_EXPORT __declspec(dllexport)
#else
    #define DLL_EXPORT __attribute__((visibility("default")))
#endif

extern "C" {
    DLL_EXPORT const char* CalculatePrice(const char* blueprintJson, const char* contextJson) {
        try {
            auto jBlueprint = nlohmann::json::parse(blueprintJson);
            auto jContext = nlohmann::json::parse(contextJson);

            std::string name = jBlueprint.value("BlueprintName", "Unnamed");
            std::string version = jBlueprint.value("Version", "1.0.0");
            auto blueprint = std::make_shared<Pricing::PriceBlueprint>(name, version);

            if (!Pricing::RuleRegistry::Instance().IsRegistered("BasePrice")) {
                Pricing::RegisterStandardRules();
            }

            for (const auto& ruleJson : jBlueprint["Rules"]) {
                std::string type = ruleJson.value("Type", "");
                std::string rName = ruleJson.value("Name", "");
                bool enabled = ruleJson.value("Enabled", true);
                auto rule = Pricing::RuleRegistry::Instance().Create(type, rName, enabled, ruleJson);
                blueprint->AddRule(std::move(rule));
            }

            Pricing::PricingContext context;
            for (auto& [key, val] : jContext.items()) {
                if (val.is_number()) {
                    context.Set(key, val.get<double>());
                } else if (val.is_boolean()) {
                    context.Set(key, val.get<bool>());
                } else if (val.is_string()) {
                    context.Set(key, val.get<std::string>());
                }
            }

            Pricing::PriceResult result = blueprint->Calculate(context);

            nlohmann::json resJson;
            resJson["basePrice"] = result.basePrice;
            resJson["finalPrice"] = result.finalPrice;
            
            nlohmann::json auditArray = nlohmann::json::array();
            for (const auto& rec : result.auditTrail) {
                nlohmann::json auditItem;
                auditItem["ruleName"] = rec.ruleName;
                auditItem["description"] = rec.description;
                auditItem["inputPrice"] = rec.inputPrice;
                auditItem["outputPrice"] = rec.outputPrice;
                auditItem["adjustment"] = rec.adjustment;
                auditArray.push_back(auditItem);
            }
            resJson["auditTrail"] = auditArray;

            static std::string outputStr;
            outputStr = resJson.dump();
            return outputStr.c_str();

        } catch (const std::exception& e) {
            static std::string errStr;
            errStr = "{\"error\": \"" + std::string(e.what()) + "\"}";
            return errStr.c_str();
        }
    }
}
