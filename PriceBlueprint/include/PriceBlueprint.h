#pragma once
#include <string>
#include <vector>
#include <unordered_map>
#include <variant>
#include <memory>
#include <iostream>
#include <iomanip>
#include <optional>
#include <cmath>
#include <fstream>
#include <stdexcept>
#include <nlohmann/json.hpp>

namespace Pricing {

    // A variant to represent context variable values (numbers, booleans, or strings)
    using Value = std::variant<double, bool, std::string>;

    // The evaluation context containing inputs (e.g., customer tier, quantity, holiday season)
    class PricingContext {
    private:
        std::unordered_map<std::string, Value> variables;

    public:
        void Set(const std::string& key, Value val) {
            variables[key] = val;
        }

        template<typename T>
        std::optional<T> Get(const std::string& key) const {
            auto it = variables.find(key);
            if (it != variables.end() && std::holds_alternative<T>(it->second)) {
                return std::get<T>(it->second);
            }
            return std::nullopt;
        }

        bool Has(const std::string& key) const {
            return variables.find(key) != variables.end();
        }
    };

    // Evaluates simple expressions (e.g. "quantity >= 50", "region == US") on the PricingContext
    inline bool EvaluateCondition(const PricingContext& context, const std::string& conditionExpr) {
        if (conditionExpr.empty()) {
            return true;
        }

        // Search for operators
        std::string op = "";
        size_t opPos = std::string::npos;
        std::string operators[] = { "==", "!=", ">=", "<=", ">", "<" };
        for (const auto& possibleOp : operators) {
            opPos = conditionExpr.find(possibleOp);
            if (opPos != std::string::npos) {
                op = possibleOp;
                break;
            }
        }

        // Backwards compatibility: if no operator is present, treat the expression as a simple boolean key lookup
        if (op.empty()) {
            return context.Get<bool>(conditionExpr).value_or(false);
        }

        std::string key = conditionExpr.substr(0, opPos);
        std::string valStr = conditionExpr.substr(opPos + op.length());

        // Trim helper
        auto trim = [](std::string& s) {
            s.erase(0, s.find_first_not_of(" \t\r\n"));
            s.erase(s.find_last_not_of(" \t\r\n") + 1);
        };
        trim(key);
        trim(valStr);

        if (!context.Has(key)) {
            return false;
        }

        // Type matching comparison
        if (context.Get<double>(key).has_value()) {
            double left = context.Get<double>(key).value();
            double right = std::stod(valStr);
            if (op == "==") return left == right;
            if (op == "!=") return left != right;
            if (op == ">=") return left >= right;
            if (op == "<=") return left <= right;
            if (op == ">") return left > right;
            if (op == "<") return left < right;
        }
        else if (context.Get<bool>(key).has_value()) {
            bool left = context.Get<bool>(key).value();
            bool right = (valStr == "true" || valStr == "1");
            if (op == "==") return left == right;
            if (op == "!=") return left != right;
        }
        else if (context.Get<std::string>(key).has_value()) {
            std::string left = context.Get<std::string>(key).value();
            std::string right = valStr;
            if (right.front() == '"' && right.back() == '"') {
                right = right.substr(1, right.length() - 2);
            }
            if (op == "==") return left == right;
            if (op == "!=") return left != right;
        }

        return false;
    }

    // Audit record detailing modifications made by a rule
    struct AuditRecord {
        std::string ruleName;
        std::string description;
        double inputPrice;
        double outputPrice;
        double adjustment;
    };

    // The output result of the pricing operating system calculation
    struct PriceResult {
        double basePrice = 0.0;
        double finalPrice = 0.0;
        std::vector<AuditRecord> auditTrail;

        void Print() const {
            std::cout << "==========================================\n";
            std::cout << "Pricing Execution Report\n";
            std::cout << "==========================================\n";
            std::cout << std::fixed << std::setprecision(2);
            std::cout << "Initial Base Price: $" << basePrice << "\n\n";
            std::cout << "Audit Trail:\n";
            for (size_t i = 0; i < auditTrail.size(); ++i) {
                const auto& record = auditTrail[i];
                std::cout << "  " << i + 1 << ". [" << record.ruleName << "] " << record.description << "\n";
                std::cout << "     Price: $" << record.inputPrice << " -> $" << record.outputPrice 
                          << " (Adjustment: " << (record.adjustment >= 0 ? "+" : "") << record.adjustment << ")\n";
            }
            std::cout << "------------------------------------------\n";
            std::cout << "Final Price: $" << finalPrice << "\n";
            std::cout << "==========================================\n";
        }
    };

    // Abstract base class representing a single pricing rule or calculation step
    class PricingRule {
    protected:
        std::string name;

    public:
        explicit PricingRule(std::string ruleName) : name(std::move(ruleName)) {}
        virtual ~PricingRule() = default;

        const std::string& GetName() const { return name; }

        // Evaluates the rule, modifying the price and adding an audit trail entry
        virtual void Execute(const PricingContext& context, double& currentPrice, std::vector<AuditRecord>& auditTrail) const = 0;
    };

    // Rule: Sets the initial base price from a context variable or constant
    class BasePriceRule : public PricingRule {
    private:
        double defaultPrice;
        std::string contextKey;

    public:
        BasePriceRule(std::string name, double defaultPrice, std::string contextKey = "base_price")
            : PricingRule(std::move(name)), defaultPrice(defaultPrice), contextKey(std::move(contextKey)) {}

        void Execute(const PricingContext& context, double& currentPrice, std::vector<AuditRecord>& auditTrail) const override {
            double startPrice = context.Get<double>(contextKey).value_or(defaultPrice);
            double original = currentPrice;
            currentPrice = startPrice;
            auditTrail.push_back({
                name,
                "Initialized base price from context/default.",
                original,
                currentPrice,
                currentPrice - original
            });
        }
    };

    // Rule: Apply percentage discount/markup under a condition
    class PercentageAdjustmentRule : public PricingRule {
    private:
        double factor; // e.g., 0.90 for 10% off, 1.05 for 5% markup
        std::string conditionKey; // Boolean context variable key required to activate the rule
        std::string desc;

    public:
        PercentageAdjustmentRule(std::string name, double factor, std::string conditionKey, std::string desc)
            : PricingRule(std::move(name)), factor(factor), conditionKey(std::move(conditionKey)), desc(std::move(desc)) {}

        void Execute(const PricingContext& context, double& currentPrice, std::vector<AuditRecord>& auditTrail) const override {
            bool conditionActive = EvaluateCondition(context, conditionKey);
            if (conditionActive) {
                double originalPrice = currentPrice;
                currentPrice *= factor;
                auditTrail.push_back({
                    name,
                    desc,
                    originalPrice,
                    currentPrice,
                    currentPrice - originalPrice
                });
            }
        }
    };

    // Rule: Apply flat adjustment (addition/subtraction) under a condition
    class FlatAdjustmentRule : public PricingRule {
    private:
        double amount; // e.g., 15.0 for standard shipping, -10.0 for coupon
        std::string conditionKey; // Boolean context variable key required to activate the rule
        std::string desc;

    public:
        FlatAdjustmentRule(std::string name, double amount, std::string conditionKey, std::string desc)
            : PricingRule(std::move(name)), amount(amount), conditionKey(std::move(conditionKey)), desc(std::move(desc)) {}

        void Execute(const PricingContext& context, double& currentPrice, std::vector<AuditRecord>& auditTrail) const override {
            bool conditionActive = EvaluateCondition(context, conditionKey);
            if (conditionActive) {
                double originalPrice = currentPrice;
                currentPrice += amount;
                auditTrail.push_back({
                    name,
                    desc,
                    originalPrice,
                    currentPrice,
                    amount
                });
            }
        }
    };

    // Rule: Apply floor and/or ceiling limits to the price
    class PriceCapRule : public PricingRule {
    private:
        std::optional<double> minPrice;
        std::optional<double> maxPrice;

    public:
        PriceCapRule(std::string name, std::optional<double> minPrice = std::nullopt, std::optional<double> maxPrice = std::nullopt)
            : PricingRule(std::move(name)), minPrice(minPrice), maxPrice(maxPrice) {}

        void Execute(const PricingContext& context, double& currentPrice, std::vector<AuditRecord>& auditTrail) const override {
            double originalPrice = currentPrice;
            double adjustedPrice = currentPrice;

            if (minPrice.has_value() && adjustedPrice < minPrice.value()) {
                adjustedPrice = minPrice.value();
            }
            if (maxPrice.has_value() && adjustedPrice > maxPrice.value()) {
                adjustedPrice = maxPrice.value();
            }

            if (adjustedPrice != originalPrice) {
                currentPrice = adjustedPrice;
                std::string desc = "Price capped (limits: Min " + 
                                   (minPrice.has_value() ? std::to_string(minPrice.value()) : "None") + ", Max " +
                                   (maxPrice.has_value() ? std::to_string(maxPrice.value()) : "None") + ").";
                auditTrail.push_back({
                    name,
                    desc,
                    originalPrice,
                    currentPrice,
                    currentPrice - originalPrice
                });
            }
        }
    };

    // Rule: Tiered pricing discount based on quantity
    class TieredPricingRule : public PricingRule {
    public:
        struct Tier {
            double minQuantity;
            double discountPercentage; // e.g., 0.10 for 10% off
        };

    private:
        std::string quantityKey;
        std::vector<Tier> tiers; // Ordered by minQuantity ascending

    public:
        TieredPricingRule(std::string name, std::string quantityKey, std::vector<Tier> pricingTiers)
            : PricingRule(std::move(name)), quantityKey(std::move(quantityKey)), tiers(std::move(pricingTiers)) {}

        void Execute(const PricingContext& context, double& currentPrice, std::vector<AuditRecord>& auditTrail) const override {
            double quantity = context.Get<double>(quantityKey).value_or(0.0);
            double applicableDiscount = 0.0;
            
            for (const auto& tier : tiers) {
                if (quantity >= tier.minQuantity) {
                    applicableDiscount = tier.discountPercentage;
                }
            }

            if (applicableDiscount > 0.0) {
                double originalPrice = currentPrice;
                currentPrice *= (1.0 - applicableDiscount);
                
                std::string desc = "Applied volume discount of " + std::to_string(static_cast<int>(std::round(applicableDiscount * 100))) + 
                                   "% for quantity of " + std::to_string(static_cast<int>(std::round(quantity))) + ".";
                auditTrail.push_back({
                    name,
                    desc,
                    originalPrice,
                    currentPrice,
                    currentPrice - originalPrice
                });
            }
        }
    };

    // The blueprint/specification template representing a structured pipeline of rules
    class PriceBlueprint {
    private:
        std::string blueprintName;
        std::vector<std::shared_ptr<PricingRule>> rules;

    public:
        explicit PriceBlueprint(std::string name) : blueprintName(std::move(name)) {}

        void AddRule(std::shared_ptr<PricingRule> rule) {
            rules.push_back(std::move(rule));
        }

        PriceResult Calculate(const PricingContext& context) const {
            PriceResult result;
            double currentPrice = 0.0;
            
            for (const auto& rule : rules) {
                rule->Execute(context, currentPrice, result.auditTrail);
            }
            
            result.basePrice = result.auditTrail.empty() ? 0.0 : result.auditTrail.front().outputPrice;
            result.finalPrice = currentPrice;
            return result;
        }

        const std::string& GetName() const { return blueprintName; }

        static std::shared_ptr<PriceBlueprint> LoadFromFile(const std::string& filePath) {
            std::ifstream file(filePath);
            if (!file.is_open()) {
                throw std::runtime_error("Could not open blueprint file: " + filePath);
            }
            nlohmann::json j;
            file >> j;

            std::string blueprintName = j.value("BlueprintName", "Unnamed Blueprint");
            auto blueprint = std::make_shared<PriceBlueprint>(blueprintName);

            for (const auto& ruleJson : j["Rules"]) {
                std::string type = ruleJson.value("Type", "");
                std::string name = ruleJson.value("Name", "");

                if (type == "BasePrice") {
                    double defaultPrice = ruleJson.value("DefaultPrice", 0.0);
                    std::string contextKey = ruleJson.value("ContextKey", "base_price");
                    blueprint->AddRule(std::make_shared<BasePriceRule>(name, defaultPrice, contextKey));
                }
                else if (type == "PercentageAdjustment") {
                    double factor = ruleJson.value("Factor", 1.0);
                    std::string conditionKey = ruleJson.value("ConditionKey", "");
                    std::string description = ruleJson.value("Description", "");
                    blueprint->AddRule(std::make_shared<PercentageAdjustmentRule>(name, factor, conditionKey, description));
                }
                else if (type == "FlatAdjustment") {
                    double amount = ruleJson.value("Amount", 0.0);
                    std::string conditionKey = ruleJson.value("ConditionKey", "");
                    std::string description = ruleJson.value("Description", "");
                    blueprint->AddRule(std::make_shared<FlatAdjustmentRule>(name, amount, conditionKey, description));
                }
                else if (type == "PriceCap") {
                    std::optional<double> minPrice;
                    std::optional<double> maxPrice;
                    if (ruleJson.contains("MinPrice") && !ruleJson["MinPrice"].is_null()) {
                        minPrice = ruleJson.value("MinPrice", 0.0);
                    }
                    if (ruleJson.contains("MaxPrice") && !ruleJson["MaxPrice"].is_null()) {
                        maxPrice = ruleJson.value("MaxPrice", 0.0);
                    }
                    blueprint->AddRule(std::make_shared<PriceCapRule>(name, minPrice, maxPrice));
                }
                else if (type == "TieredPricing") {
                    std::string quantityKey = ruleJson.value("QuantityKey", "");
                    std::vector<TieredPricingRule::Tier> tiers;
                    if (ruleJson.contains("Tiers")) {
                        for (const auto& tierJson : ruleJson["Tiers"]) {
                            double minQuantity = tierJson.value("MinQuantity", 0.0);
                            double discountPercentage = tierJson.value("DiscountPercentage", 0.0);
                            tiers.push_back({ minQuantity, discountPercentage });
                        }
                    }
                    blueprint->AddRule(std::make_shared<TieredPricingRule>(name, quantityKey, tiers));
                }
            }

            return blueprint;
        }
    };

} // namespace Pricing