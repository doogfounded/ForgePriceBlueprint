// pricing-sdk.js - Standalone Client-side SDK and evaluation engine
class ConditionParser {
    constructor(expr) {
        this.expr = expr;
        this.pos = 0;
    }

    skipWhitespace() {
        while (this.pos < this.expr.length && /\s/.test(this.expr[this.pos])) {
            this.pos++;
        }
    }

    match(token) {
        this.skipWhitespace();
        if (this.pos + token.length <= this.expr.length && this.expr.substring(this.pos, this.pos + token.length) === token) {
            this.pos += token.length;
            return true;
        }
        return false;
    }

    peek(token) {
        this.skipWhitespace();
        if (this.pos + token.length <= this.expr.length && this.expr.substring(this.pos, this.pos + token.length) === token) {
            return true;
        }
        return false;
    }

    parseAndEvaluate(context) {
        if (!this.expr || this.expr.trim() === '') return true;
        this.pos = 0;
        const result = this.parseOr(context);
        this.skipWhitespace();
        return result;
    }

    parseOr(context) {
        let result = this.parseAnd(context);
        while (true) {
            if (this.match("||")) {
                let right = this.parseAnd(context);
                result = result || right;
            } else {
                break;
            }
        }
        return result;
    }

    parseAnd(context) {
        let result = this.parsePrimary(context);
        while (true) {
            if (this.match("&&")) {
                let right = this.parsePrimary(context);
                result = result && right;
            } else {
                break;
            }
        }
        return result;
    }

    parsePrimary(context) {
        this.skipWhitespace();
        if (this.match("(")) {
            const result = this.parseOr(context);
            this.match(")"); // Consume closing parenthesis
            return result;
        }

        const start = this.pos;
        let parenCount = 0;
        while (this.pos < this.expr.length) {
            if (this.expr[this.pos] === '(') {
                parenCount++;
            } else if (this.expr[this.pos] === ')') {
                if (parenCount === 0) break;
                parenCount--;
            } else if (parenCount === 0 && (this.peek("&&") || this.peek("||"))) {
                break;
            }
            this.pos++;
        }
        const sub = this.expr.substring(start, this.pos).trim();
        return this.evaluateLeafCondition(sub, context);
    }

    evaluateLeafCondition(expr, context) {
        if (!expr || expr.trim() === '') return true;
        const operators = ["==", "!=", ">=", "<=", ">", "<", " IN ", " in "];
        let op = null;
        let opPos = -1;

        for (const possibleOp of operators) {
            opPos = expr.indexOf(possibleOp);
            if (opPos !== -1) {
                op = possibleOp;
                break;
            }
        }

        if (!op) {
            const key = expr.trim();
            return !!context[key];
        }

        let key = expr.substring(0, opPos).trim();
        let valStr = expr.substring(opPos + op.length).trim();

        if (context[key] === undefined) {
            return false;
        }

        let left = context[key];
        
        if (typeof left === 'number') {
            if (op === " IN " || op === " in ") {
                const items = valStr.split(',').map(s => parseFloat(s.trim()));
                return items.includes(left);
            }
            let right = parseFloat(valStr);
            if (isNaN(right)) return false;
            if (op === "==") return left === right;
            if (op === "!=") return left != right;
            if (op === ">=") return left >= right;
            if (op === "<=") return left <= right;
            if (op === ">") return left > right;
            if (op === "<") return left < right;
        }
        else if (typeof left === 'boolean') {
            let right = (valStr === "true" || valStr === "1");
            if (op === "==") return left === right;
            if (op === "!=") return left != right;
        }
        else if (typeof left === 'string') {
            if (op === " IN " || op === " in ") {
                const items = valStr.split(',').map(s => {
                    let sTrim = s.trim();
                    if (sTrim.startsWith('"') && sTrim.endsWith('"')) {
                        sTrim = sTrim.substring(1, sTrim.length - 1);
                    }
                    return sTrim;
                });
                return items.includes(left);
            }
            let right = valStr;
            if (right.startsWith('"') && right.endsWith('"')) {
                right = right.substring(1, right.length - 1);
            }
            if (op === "==") return left === right;
            if (op === "!=") return left != right;
        }

        return false;
    }
}

function evaluateCondition(expr, context) {
    const parser = new ConditionParser(expr);
    return parser.parseAndEvaluate(context);
}

class PricingEngine {
    constructor(blueprint) {
        this.blueprint = blueprint;
    }

    calculate(context) {
        let currentPrice = 0.0;
        let initialBase = 0.0;
        const auditTrail = [];

        if (!this.blueprint || !Array.isArray(this.blueprint.Rules)) {
            return { basePrice: 0.0, finalPrice: 0.0, auditTrail: [] };
        }

        this.blueprint.Rules.forEach(rule => {
            let step = {
                ruleName: rule.Name,
                type: rule.Type,
                active: false,
                inputPrice: currentPrice,
                outputPrice: currentPrice,
                adjustment: 0.0,
                description: ""
            };

            const isEnabled = rule.Enabled !== false;
            if (!isEnabled) {
                step.description = `Rule is disabled. Skipped.`;
                auditTrail.push(step);
                return;
            }

            if (rule.Type === 'BasePrice') {
                const key = rule.ContextKey || "base_price";
                const val = (context[key] !== undefined) ? context[key] : rule.DefaultPrice;
                
                initialBase = val;
                currentPrice = val;
                step.active = true;
                step.adjustment = currentPrice - step.inputPrice;
                step.outputPrice = currentPrice;
                step.description = `Initialized base price to $${val.toFixed(2)} from ${context[key] !== undefined ? 'context key "'+key+'"' : 'rule default'}.`;
            }
            else if (rule.Type === 'PercentageAdjustment') {
                const isActive = evaluateCondition(rule.ConditionKey, context);
                if (isActive) {
                    currentPrice *= rule.Factor;
                    step.active = true;
                    step.adjustment = currentPrice - step.inputPrice;
                    step.outputPrice = currentPrice;
                    step.description = rule.Description || `Applied factor multiplier of ${rule.Factor}x under condition "${rule.ConditionKey}".`;
                } else {
                    step.description = `Condition "${rule.ConditionKey || 'None'}" was not met. Rule skipped.`;
                }
            }
            else if (rule.Type === 'FlatAdjustment') {
                const isActive = evaluateCondition(rule.ConditionKey, context);
                if (isActive) {
                    currentPrice += rule.Amount;
                    step.active = true;
                    step.adjustment = rule.Amount;
                    step.outputPrice = currentPrice;
                    step.description = rule.Description || `Applied flat adjustment of $${rule.Amount.toFixed(2)} under condition "${rule.ConditionKey}".`;
                } else {
                    step.description = `Condition "${rule.ConditionKey || 'None'}" was not met. Rule skipped.`;
                }
            }
            else if (rule.Type === 'PriceCap') {
                let adjusted = currentPrice;
                let capped = false;
                let capDetails = [];

                if (rule.MinPrice !== null && rule.MinPrice !== undefined && adjusted < rule.MinPrice) {
                    adjusted = rule.MinPrice;
                    capped = true;
                    capDetails.push(`floor of $${rule.MinPrice.toFixed(2)}`);
                }
                if (rule.MaxPrice !== null && rule.MaxPrice !== undefined && adjusted > rule.MaxPrice) {
                    adjusted = rule.MaxPrice;
                    capped = true;
                    capDetails.push(`ceiling of $${rule.MaxPrice.toFixed(2)}`);
                }

                if (capped) {
                    currentPrice = adjusted;
                    step.active = true;
                    step.adjustment = currentPrice - step.inputPrice;
                    step.outputPrice = currentPrice;
                    step.description = `Price capped at ${capDetails.join(' and ')}.`;
                } else {
                    step.description = `Price ($${currentPrice.toFixed(2)}) is within caps (Min: ${rule.MinPrice != null ? '$'+rule.MinPrice.toFixed(2) : 'None'}, Max: ${rule.MaxPrice != null ? '$'+rule.MaxPrice.toFixed(2) : 'None'}).`;
                }
            }
            else if (rule.Type === 'TieredPricing') {
                const qKey = rule.QuantityKey || "quantity";
                const qty = context[qKey] || 0.0;
                let discount = 0.0;
                const graduated = !!rule.Graduated;

                const sortedTiers = [...(rule.Tiers || [])].sort((a, b) => a.MinQuantity - b.MinQuantity);
                
                if (graduated && qty > 0.0) {
                    let totalDiscountUnits = 0.0;
                    let prevQty = 0.0;
                    let prevDiscount = 0.0;
                    for (const tier of sortedTiers) {
                        if (qty > tier.MinQuantity) {
                            totalDiscountUnits += (tier.MinQuantity - prevQty) * prevDiscount;
                            prevQty = tier.MinQuantity;
                            prevDiscount = tier.DiscountPercentage;
                        } else {
                            totalDiscountUnits += (qty - prevQty) * prevDiscount;
                            prevQty = qty;
                            break;
                        }
                    }
                    if (qty > prevQty) {
                        totalDiscountUnits += (qty - prevQty) * prevDiscount;
                    }
                    discount = totalDiscountUnits / qty;
                } else {
                    for (const tier of sortedTiers) {
                        if (qty >= tier.MinQuantity) {
                            discount = tier.DiscountPercentage;
                        }
                    }
                }

                if (discount > 0.0) {
                    currentPrice *= (1.0 - discount);
                    step.active = true;
                    step.adjustment = currentPrice - step.inputPrice;
                    step.outputPrice = currentPrice;
                    const formattedDiscount = graduated ? (discount * 100).toFixed(1) : (discount * 100).toFixed(0);
                    step.description = `Applied ${graduated ? "graduated blended" : "volume"} discount of ${formattedDiscount}% for quantity of ${qty}.`;
                } else {
                    step.description = `Quantity of ${qty} did not qualify for volume tiers.`;
                }
            }
            else if (rule.Type === 'Rounding') {
                let rounded = currentPrice;
                const mode = rule.RoundingMode || "NearestDollar";

                if (mode === "NearestDollar") {
                    rounded = Math.round(currentPrice);
                }
                else if (mode === "EndsIn99") {
                    rounded = Math.round(currentPrice) - 0.01;
                }
                else if (mode === "EndsIn95") {
                    rounded = Math.round(currentPrice) - 0.05;
                }
                else if (mode === "NearestNickel") {
                    rounded = Math.round(currentPrice * 20.0) / 20.0;
                }
                else if (mode === "NearestDime") {
                    rounded = Math.round(currentPrice * 10.0) / 10.0;
                }

                if (rounded !== currentPrice) {
                    step.active = true;
                    step.adjustment = rounded - currentPrice;
                    currentPrice = rounded;
                    step.outputPrice = currentPrice;
                    step.description = `Applied ${mode} rounding.`;
                } else {
                    step.description = `Price already matches ${mode} rounding.`;
                }
            }

            auditTrail.push(step);
        });

        return {
            basePrice: initialBase,
            finalPrice: currentPrice,
            auditTrail: auditTrail
        };
    }
}

// Exports for bundlers/CommonJS and standard script globals
if (typeof exports !== 'undefined') {
    exports.ConditionParser = ConditionParser;
    exports.evaluateCondition = evaluateCondition;
    exports.PricingEngine = PricingEngine;
}
if (typeof window !== 'undefined') {
    window.ConditionParser = ConditionParser;
    window.evaluateCondition = evaluateCondition;
    window.PricingEngine = PricingEngine;
}
