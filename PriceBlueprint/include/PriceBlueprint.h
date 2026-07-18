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
        std::string operators[] = { "==", "!=", ">=", "<=", ">", "<", " IN ", " in " };
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

        // Split and trim helper
        auto splitAndTrim = [&trim](const std::string& s, char delim) {
            std::vector<std::string> elems;
            size_t start = 0;
            size_t end = s.find(delim);
            while (end != std::string::npos) {
                std::string token = s.substr(start, end - start);
                trim(token);
                elems.push_back(token);
                start = end + 1;
                end = s.find(delim, start);
            }
            std::string token = s.substr(start);
            trim(token);
            elems.push_back(token);
            return elems;
        };

        if (!context.Has(key)) {
            return false;
        }

        // Type matching comparison
        if (context.Get<double>(key).has_value()) {
            double left = context.Get<double>(key).value();
            if (op == " IN " || op == " in ") {
                std::vector<std::string> items = splitAndTrim(valStr, ',');
                for (const auto& item : items) {
                    try {
                        if (left == std::stod(item)) return true;
                    } catch (...) {}
                }
                return false;
            }
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
            if (op == " IN " || op == " in ") {
                std::vector<std::string> items = splitAndTrim(valStr, ',');
                for (auto& item : items) {
                    if (item.front() == '"' && item.back() == '"') {
                        item = item.substr(1, item.length() - 2);
                    }
                    if (left == item) return true;
                }
                return false;
            }
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
        bool enabled;

    public:
        explicit PricingRule(std::string ruleName, bool enabled = true) 
            : name(std::move(ruleName)), enabled(enabled) {}
        virtual ~PricingRule() = default;

        const std::string& GetName() const { return name; }
        bool IsEnabled() const { return enabled; }

        // Evaluates the rule, modifying the price and adding an audit trail entry
        virtual void Execute(const PricingContext& context, double& currentPrice, std::vector<AuditRecord>& auditTrail) const = 0;
    };

    // Rule: Sets the initial base price from a context variable or constant
    class BasePriceRule : public PricingRule {
    private:
        double defaultPrice;
        std::string contextKey;

    public:
        BasePriceRule(std::string name, double defaultPrice, std::string contextKey = "base_price", bool enabled = true)
            : PricingRule(std::move(name), enabled), defaultPrice(defaultPrice), contextKey(std::move(contextKey)) {}

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
        PercentageAdjustmentRule(std::string name, double factor, std::string conditionKey, std::string desc, bool enabled = true)
            : PricingRule(std::move(name), enabled), factor(factor), conditionKey(std::move(conditionKey)), desc(std::move(desc)) {}

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
        FlatAdjustmentRule(std::string name, double amount, std::string conditionKey, std::string desc, bool enabled = true)
            : PricingRule(std::move(name), enabled), amount(amount), conditionKey(std::move(conditionKey)), desc(std::move(desc)) {}

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
        PriceCapRule(std::string name, std::optional<double> minPrice = std::nullopt, std::optional<double> maxPrice = std::nullopt, bool enabled = true)
            : PricingRule(std::move(name), enabled), minPrice(minPrice), maxPrice(maxPrice) {}

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
        TieredPricingRule(std::string name, std::string quantityKey, std::vector<Tier> pricingTiers, bool enabled = true)
            : PricingRule(std::move(name), enabled), quantityKey(std::move(quantityKey)), tiers(std::move(pricingTiers)) {}

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

    // Rule: Rounds the price according to a mode (e.g. NearestDollar, EndsIn99, EndsIn95, NearestNickel, NearestDime)
    class RoundingRule : public PricingRule {
    private:
        std::string roundingMode;

    public:
        RoundingRule(std::string name, std::string roundingMode, bool enabled = true)
            : PricingRule(std::move(name), enabled), roundingMode(std::move(roundingMode)) {}

        void Execute(const PricingContext& context, double& currentPrice, std::vector<AuditRecord>& auditTrail) const override {
            double originalPrice = currentPrice;
            double roundedPrice = currentPrice;

            if (roundingMode == "NearestDollar") {
                roundedPrice = std::round(currentPrice);
            }
            else if (roundingMode == "EndsIn99") {
                roundedPrice = std::round(currentPrice) - 0.01;
            }
            else if (roundingMode == "EndsIn95") {
                roundedPrice = std::round(currentPrice) - 0.05;
            }
            else if (roundingMode == "NearestNickel") {
                roundedPrice = std::round(currentPrice * 20.0) / 20.0;
            }
            else if (roundingMode == "NearestDime") {
                roundedPrice = std::round(currentPrice * 10.0) / 10.0;
            }

            if (roundedPrice != originalPrice) {
                currentPrice = roundedPrice;
                std::string desc = "Applied " + roundingMode + " rounding.";
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
                if (rule->IsEnabled()) {
                    rule->Execute(context, currentPrice, result.auditTrail);
                }
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
                bool enabled = ruleJson.value("Enabled", true);

                if (type == "BasePrice") {
                    double defaultPrice = ruleJson.value("DefaultPrice", 0.0);
                    std::string contextKey = ruleJson.value("ContextKey", "base_price");
                    blueprint->AddRule(std::make_shared<BasePriceRule>(name, defaultPrice, contextKey, enabled));
                }
                else if (type == "PercentageAdjustment") {
                    double factor = ruleJson.value("Factor", 1.0);
                    std::string conditionKey = ruleJson.value("ConditionKey", "");
                    std::string description = ruleJson.value("Description", "");
                    blueprint->AddRule(std::make_shared<PercentageAdjustmentRule>(name, factor, conditionKey, description, enabled));
                }
                else if (type == "FlatAdjustment") {
                    double amount = ruleJson.value("Amount", 0.0);
                    std::string conditionKey = ruleJson.value("ConditionKey", "");
                    std::string description = ruleJson.value("Description", "");
                    blueprint->AddRule(std::make_shared<FlatAdjustmentRule>(name, amount, conditionKey, description, enabled));
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
                    blueprint->AddRule(std::make_shared<PriceCapRule>(name, minPrice, maxPrice, enabled));
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
                    blueprint->AddRule(std::make_shared<TieredPricingRule>(name, quantityKey, tiers, enabled));
                }
                else if (type == "Rounding") {
                    std::string roundingMode = ruleJson.value("RoundingMode", "NearestDollar");
                    blueprint->AddRule(std::make_shared<RoundingRule>(name, roundingMode, enabled));
                }
            }

            return blueprint;
        }
    };

} // namespace Pricing