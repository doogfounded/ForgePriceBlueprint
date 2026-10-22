#include "../include/PriceBlueprint.h"
#include <cstring>
#include <mutex>
#include <string>

#if defined(_WIN32) || defined(_WIN64)
    #define DLL_EXPORT __declspec(dllexport)
#else
    #define DLL_EXPORT __attribute__((visibility("default")))
#endif

namespace {
    // Rule registration mutates a global singleton; do it exactly once, thread-safely.
    std::once_flag g_registerOnce;

    std::string ErrorJson(const std::string& message) {
        nlohmann::json j;
        j["error"] = message;
        return j.dump();
    }

    // Pure function of its inputs: no shared mutable state, safe to call concurrently.
    std::string CalculateImpl(const char* blueprintJson, const char* contextJson) {
        try {
            if (blueprintJson == nullptr || contextJson == nullptr) {
                return ErrorJson("Null argument");
            }

            auto jBlueprint = nlohmann::json::parse(blueprintJson);
            auto jContext = nlohmann::json::parse(contextJson);

            std::string name = jBlueprint.value("BlueprintName", "Unnamed");
            std::string version = jBlueprint.value("Version", "1.0.0");
            auto blueprint = std::make_shared<Pricing::PriceBlueprint>(name, version);

            std::call_once(g_registerOnce, [] {
                if (!Pricing::RuleRegistry::Instance().IsRegistered("BasePrice")) {
                    Pricing::RegisterStandardRules();
                }
            });

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

            return resJson.dump();
        } catch (const std::exception& e) {
            return ErrorJson(e.what());
        } catch (...) {
            return ErrorJson("Unknown error");
        }
    }
}

extern "C" {
    // Thread-safe. Writes a NUL-terminated UTF-8 JSON result into the caller's buffer.
    // Returns the number of bytes required INCLUDING the terminating NUL.
    // If outCapacity is smaller than that (or outBuffer is null), nothing is written
    // and the caller should retry with a buffer of at least the returned size.
    DLL_EXPORT int PB_Calculate(const char* blueprintJson, const char* contextJson,
                                char* outBuffer, int outCapacity) {
        const std::string result = CalculateImpl(blueprintJson, contextJson);
        const size_t required = result.size() + 1;
        if (outBuffer != nullptr && outCapacity >= 0 && static_cast<size_t>(outCapacity) >= required) {
            std::memcpy(outBuffer, result.c_str(), required);
        }
        return static_cast<int>(required);
    }
}
