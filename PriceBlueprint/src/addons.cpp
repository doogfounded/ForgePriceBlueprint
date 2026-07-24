#include "../include/PriceBlueprint.h"
#include <unordered_map>
#include <cmath>

namespace Pricing {

    // A custom rule that applies a percentage discount if the context contains a matching promo code.
    class PromoCodeDiscountRule : public PricingRule {
    private:
        std::unordered_map<std::string, double> promoDiscounts; // Maps code -> discount factor (e.g. "SAVE20" -> 0.80)
        std::string promoKey;

    public:
        PromoCodeDiscountRule(std::string name, std::unordered_map<std::string, double> promoDiscounts, std::string promoKey = "promo_code", bool enabled = true)
            : PricingRule(std::move(name), enabled), promoDiscounts(std::move(promoDiscounts)), promoKey(std::move(promoKey)) {}

        void Execute(const PricingContext& context, double& currentPrice, std::vector<AuditRecord>& auditTrail) const override {
            auto optCode = context.Get<std::string>(promoKey);
            if (optCode.has_value()) {
                std::string code = optCode.value();
                auto it = promoDiscounts.find(code);
                if (it != promoDiscounts.end()) {
                    double factor = it->second;
                    double originalPrice = currentPrice;
                    currentPrice *= factor;
                    auditTrail.push_back({
                        name,
                        "Applied promo code '" + code + "' (" + std::to_string(static_cast<int>(std::round((1.0 - factor) * 100))) + "% discount).",
                        originalPrice,
                        currentPrice,
                        currentPrice - originalPrice
                    });
                }
            }
        }
    };

    // The registration hook function called by the core engine.
    void RegisterAddonRules() {
        auto& reg = RuleRegistry::Instance();

        reg.Register("PromoCodeDiscount", [](const std::string& name, bool enabled, const nlohmann::json& j) {
            std::unordered_map<std::string, double> promoDiscounts;
            if (j.contains("PromoDiscounts") && j["PromoDiscounts"].is_object()) {
                for (auto& [key, val] : j["PromoDiscounts"].items()) {
                    if (val.is_number()) {
                        promoDiscounts[key] = val.get<double>();
                    }
                }
            }
            std::string promoKey = j.value("PromoKey", "promo_code");
            return std::make_shared<PromoCodeDiscountRule>(name, promoDiscounts, promoKey, enabled);
        });
    }

} // namespace Pricing
