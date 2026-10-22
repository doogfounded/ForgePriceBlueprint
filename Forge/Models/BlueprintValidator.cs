using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Forge
{
    /// <summary>Structural (schema) validation of a blueprint JSON document.</summary>
    public static class BlueprintValidator
    {
        public static List<string> Validate(JsonElement root)
        {
            var errors = new List<string>();
            if (root.ValueKind != JsonValueKind.Object)
            {
                errors.Add("Blueprint must be a JSON object.");
                return errors;
            }

            if (!root.TryGetProperty("BlueprintName", out var nameProp) || nameProp.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(nameProp.GetString()))
            {
                errors.Add("Blueprint must contain a non-empty string property 'BlueprintName'.");
            }

            if (!root.TryGetProperty("Version", out var versionProp) || versionProp.ValueKind != JsonValueKind.String)
            {
                errors.Add("Blueprint must contain a string property 'Version'.");
            }

            if (!root.TryGetProperty("Rules", out var rulesProp) || rulesProp.ValueKind != JsonValueKind.Array)
            {
                errors.Add("Blueprint must contain an array property 'Rules'.");
                return errors;
            }

            int index = 0;
            foreach (var rule in rulesProp.EnumerateArray())
            {
                index++;
                if (rule.ValueKind != JsonValueKind.Object)
                {
                    errors.Add($"Rule #{index} must be an object.");
                    continue;
                }

                if (!rule.TryGetProperty("Type", out var typeProp) || typeProp.ValueKind != JsonValueKind.String)
                {
                    errors.Add($"Rule #{index}: missing required string property 'Type'.");
                    continue;
                }

                if (!rule.TryGetProperty("Name", out var ruleNameProp) || ruleNameProp.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(ruleNameProp.GetString()))
                {
                    errors.Add($"Rule #{index}: missing required string property 'Name'.");
                }

                string type = typeProp.GetString() ?? "";
                if (type == "BasePrice")
                {
                    if (!rule.TryGetProperty("DefaultPrice", out var dpProp) || dpProp.ValueKind != JsonValueKind.Number)
                    {
                        errors.Add($"Rule #{index} (BasePrice) '{ruleNameProp.GetString()}': missing required number property 'DefaultPrice'.");
                    }
                }
                else if (type == "PercentageAdjustment")
                {
                    if (!rule.TryGetProperty("Factor", out var fProp) || fProp.ValueKind != JsonValueKind.Number)
                    {
                        errors.Add($"Rule #{index} (PercentageAdjustment) '{ruleNameProp.GetString()}': missing required number property 'Factor'.");
                    }
                    if (!rule.TryGetProperty("ConditionKey", out var ckProp) || ckProp.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(ckProp.GetString()))
                    {
                        errors.Add($"Rule #{index} (PercentageAdjustment) '{ruleNameProp.GetString()}': missing required string property 'ConditionKey'.");
                    }
                }
                else if (type == "FlatAdjustment")
                {
                    if (!rule.TryGetProperty("Amount", out var aProp) || aProp.ValueKind != JsonValueKind.Number)
                    {
                        errors.Add($"Rule #{index} (FlatAdjustment) '{ruleNameProp.GetString()}': missing required number property 'Amount'.");
                    }
                    if (!rule.TryGetProperty("ConditionKey", out var ckProp) || ckProp.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(ckProp.GetString()))
                    {
                        errors.Add($"Rule #{index} (FlatAdjustment) '{ruleNameProp.GetString()}': missing required string property 'ConditionKey'.");
                    }
                }
                else if (type == "PriceCap")
                {
                    bool hasMin = rule.TryGetProperty("MinPrice", out var minProp) && minProp.ValueKind != JsonValueKind.Null;
                    bool hasMax = rule.TryGetProperty("MaxPrice", out var maxProp) && maxProp.ValueKind != JsonValueKind.Null;
                    if (!hasMin && !hasMax)
                    {
                        errors.Add($"Rule #{index} (PriceCap) '{ruleNameProp.GetString()}': must specify at least one of 'MinPrice' or 'MaxPrice'.");
                    }
                    if (hasMin && minProp.ValueKind != JsonValueKind.Number)
                    {
                        errors.Add($"Rule #{index} (PriceCap) '{ruleNameProp.GetString()}': 'MinPrice' must be a number.");
                    }
                    if (hasMax && maxProp.ValueKind != JsonValueKind.Number)
                    {
                        errors.Add($"Rule #{index} (PriceCap) '{ruleNameProp.GetString()}': 'MaxPrice' must be a number.");
                    }
                }
                else if (type == "TieredPricing")
                {
                    if (!rule.TryGetProperty("QuantityKey", out var qkProp) || qkProp.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(qkProp.GetString()))
                    {
                        errors.Add($"Rule #{index} (TieredPricing) '{ruleNameProp.GetString()}': missing required string property 'QuantityKey'.");
                    }
                    if (!rule.TryGetProperty("Tiers", out var tiersProp) || tiersProp.ValueKind != JsonValueKind.Array || tiersProp.GetArrayLength() == 0)
                    {
                        errors.Add($"Rule #{index} (TieredPricing) '{ruleNameProp.GetString()}': missing required non-empty array property 'Tiers'.");
                    }
                    else
                    {
                        int tIdx = 0;
                        foreach (var tier in tiersProp.EnumerateArray())
                        {
                            tIdx++;
                            if (tier.ValueKind != JsonValueKind.Object)
                            {
                                errors.Add($"Rule #{index} (TieredPricing) '{ruleNameProp.GetString()}', Tier #{tIdx}: tier must be an object.");
                                continue;
                            }
                            if (!tier.TryGetProperty("MinQuantity", out var mqProp) || mqProp.ValueKind != JsonValueKind.Number)
                            {
                                errors.Add($"Rule #{index} (TieredPricing) '{ruleNameProp.GetString()}', Tier #{tIdx}: missing required number property 'MinQuantity'.");
                            }
                            if (!tier.TryGetProperty("DiscountPercentage", out var dpProp) || dpProp.ValueKind != JsonValueKind.Number)
                            {
                                errors.Add($"Rule #{index} (TieredPricing) '{ruleNameProp.GetString()}', Tier #{tIdx}: missing required number property 'DiscountPercentage'.");
                            }
                        }
                    }
                }
                else if (type == "Rounding")
                {
                    if (!rule.TryGetProperty("RoundingMode", out var rmProp) || rmProp.ValueKind != JsonValueKind.String)
                    {
                        errors.Add($"Rule #{index} (Rounding) '{ruleNameProp.GetString()}': missing required string property 'RoundingMode'.");
                    }
                    else
                    {
                        string mode = rmProp.GetString() ?? "";
                        var validModes = new[] { "NearestDollar", "EndsIn99", "EndsIn95", "NearestNickel", "NearestDime" };
                        if (!validModes.Contains(mode))
                        {
                            errors.Add($"Rule #{index} (Rounding) '{ruleNameProp.GetString()}': 'RoundingMode' must be one of: " + string.Join(", ", validModes));
                        }
                    }
                }
                else
                {
                    errors.Add($"Rule #{index}: unknown rule type '{type}'.");
                }
            }

            return errors;
        }
    }
}
