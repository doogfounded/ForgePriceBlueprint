using System.Text.Json;
using System.Text.Json.Nodes;
using Forge.Models;
using Forge.Services;

namespace Forge.Tests
{
    public class ModelTests
    {
        private static readonly PricingEngine Engine = new();

        [Theory]
        [InlineData("enterprise")]
        [InlineData("saas")]
        [InlineData("ecommerce")]
        public void Presets_AreValidAndPriceWithTheirOwnDefaults(string id)
        {
            var preset = Presets.Get(id);
            BlueprintFileService.Validate(preset.Json);

            var doc = new BlueprintDocument();
            doc.Load(preset.Json);
            var vars = ContextInference.Infer(doc.Rules);

            // every inferred variable is covered by the preset's defaults with the right type
            foreach (var v in vars)
            {
                Assert.True(preset.Context.TryGetValue(v.Key, out var val), $"preset missing '{v.Key}'");
                Assert.Equal(v.Kind switch { ContextKind.Number => typeof(double), ContextKind.Bool => typeof(bool), _ => typeof(string) }, val!.GetType());
            }

            var result = Engine.Calculate(preset.Json, preset.Context);
            Assert.True(result.FinalPrice > 0);
            Assert.NotEmpty(result.AuditTrail);
        }

        [Fact]
        public void EnterprisePreset_MatchesKnownPrice()
        {
            // same inputs as the documented API example, plus the preset's extra rules
            var p = Presets.Get("enterprise");
            var r = Engine.Calculate(p.Json, new Dictionary<string, object>
            { ["base_price"] = 250.0, ["quantity"] = 120.0, ["is_partner"] = false, ["region"] = "EU" });
            // 250 * 0.85 (tier) * 1.15 (international) = 244.375
            Assert.Equal(244.375, r.FinalPrice, 6);
        }

        [Fact]
        public void Document_AddMoveRemoveAndNotify()
        {
            var doc = BlueprintDocument.CreateBlank();
            var events = new List<bool>();
            doc.Changed += s => events.Add(s);

            var cap = doc.AddRule("PriceCap");
            var round = doc.AddRule("Rounding");
            Assert.Equal(3, doc.Rules.Count);

            Assert.True(doc.MoveRule(round, -1));
            Assert.Same(round, doc.Rules[1]);
            Assert.False(doc.MoveRule(doc.Rules[0]!.AsObject(), -1)); // already first

            doc.RemoveRule(cap);
            Assert.Equal(2, doc.Rules.Count);

            doc.Name = "Renamed";
            doc.NotifyEdited();

            Assert.Equal(new[] { true, true, true, true, false, false }, events);
            BlueprintFileService.Validate(doc.ToJson());
        }

        [Theory]
        [InlineData("BasePrice")]
        [InlineData("TieredPricing")]
        [InlineData("PercentageAdjustment")]
        [InlineData("FlatAdjustment")]
        [InlineData("PriceCap")]
        [InlineData("Rounding")]
        public void NewRules_AreSchemaValidAndEngineAccepts(string type)
        {
            var doc = BlueprintDocument.CreateBlank();
            if (type != "BasePrice") doc.AddRule(type);
            BlueprintFileService.Validate(doc.ToJson());
            var vars = ContextInference.Infer(doc.Rules).ToDictionary(v => v.Key, v => v.Default);
            Engine.Calculate(doc.ToJson(), vars); // must not throw
        }

        [Fact]
        public void Inference_ParsesCompoundConditionsLikeTheEngine()
        {
            var vars = ContextInference.FromCondition("(quantity >= 50 && is_partner == true) || region != US || tier IN gold, silver || is_vip").ToList();

            Assert.Equal(ContextKind.Number, vars.Single(v => v.Key == "quantity").Kind);
            Assert.Equal(ContextKind.Bool, vars.Single(v => v.Key == "is_partner").Kind);
            Assert.Equal(ContextKind.Text, vars.Single(v => v.Key == "region").Kind);
            var tier = vars.Single(v => v.Key == "tier");
            Assert.Equal(ContextKind.Text, tier.Kind);
            Assert.Equal("gold", tier.Default);
            Assert.Equal(ContextKind.Bool, vars.Single(v => v.Key == "is_vip").Kind);
        }

        [Fact]
        public void Curve_IsMonotonicForVolumeDiscountsAndHitsTierEdges()
        {
            var p = Presets.Get("enterprise");
            var curve = CurveCalculator.Compute(Engine, p.Json, p.Context);

            Assert.NotNull(curve);
            Assert.Equal("quantity", curve!.QuantityKey);
            Assert.Contains(curve.Points, pt => pt.Quantity == 10);
            Assert.Contains(curve.Points, pt => pt.Quantity == 9.99);

            var tail = curve.Points.Where(pt => pt.Quantity >= 10).ToList();
            for (int i = 1; i < tail.Count; i++) Assert.True(tail[i].Price <= tail[i - 1].Price + 1e-9);
        }

        [Fact]
        public void Curve_IsNullWithoutTieredRule()
        {
            string json = BlueprintFileService.Serialize(new BlueprintBuilder("x").SetBasePrice("b", 10).Build());
            Assert.Null(CurveCalculator.Compute(Engine, json, new Dictionary<string, object>()));
        }
    }

    public class YamlTests
    {
        [Fact]
        public void RoundTrip_PreservesTypesForEveryPreset()
        {
            foreach (var p in Presets.All)
            {
                string yaml = YamlConverter.JsonToYaml(p.Json);
                string back = YamlConverter.YamlToJson(yaml);

                Assert.True(JsonNode.DeepEquals(JsonNode.Parse(p.Json), JsonNode.Parse(back)),
                    $"{p.Id} changed in round trip.\n{yaml}");
                BlueprintFileService.Validate(back);
            }
        }

        [Fact]
        public void RoundTrip_KeepsAmbiguousStringsAsStrings()
        {
            string json = """{"a":"true","b":"123","c":"null","d":true,"e":123,"f":null,"g":1.5,"h":"1e3"}""";
            string back = YamlConverter.YamlToJson(YamlConverter.JsonToYaml(json));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(back)), back);
        }

        [Fact]
        public void HandwrittenYaml_ConvertsAndValidates()
        {
            const string yaml = """
                Version: "1.0.0"
                BlueprintName: Hand Written
                Rules:
                  - Type: BasePrice
                    Name: Base
                    DefaultPrice: 99.5
                  - Type: PriceCap
                    Name: Cap
                    MinPrice: 10
                    MaxPrice:
                """;
            string json = YamlConverter.YamlToJson(yaml);
            BlueprintFileService.Validate(json);
            var rules = (JsonNode.Parse(json)!["Rules"] as JsonArray)!;
            Assert.Equal(99.5, (double)rules[0]!["DefaultPrice"]!);
            Assert.Null(rules[1]!["MaxPrice"]);
        }

        [Fact]
        public void BadYaml_Throws()
        {
            Assert.ThrowsAny<Exception>(() => YamlConverter.YamlToJson("a: [unclosed"));
        }
    }

    public class ScenarioIoTests
    {
        [Fact]
        public void RoundTrip_AndApiPayloadCompatibility()
        {
            var items = new List<ScenarioItem>
            {
                new() { Name = "A", Variables = new() { ["q"] = 5.0, ["vip"] = true, ["region"] = "EU" } }
            };
            var back = ScenarioIO.Parse(ScenarioIO.Serialize(items));
            Assert.Equal("A", back[0].Name);
            Assert.Equal(5.0, back[0].Variables["q"]);
            Assert.Equal(true, back[0].Variables["vip"]);
            Assert.Equal("EU", back[0].Variables["region"]);

            // exact payload from the old API guide still parses
            var api = ScenarioIO.Parse("""[{"scenario_name":"Standard Promotion Check","context":{"base_price":200,"quantity":15,"region":"EU","is_partner":true}}]""");
            Assert.Equal(15.0, api[0].Variables["quantity"]);
            Assert.Equal(true, api[0].Variables["is_partner"]);
        }

        [Fact]
        public void NonArray_Throws() => Assert.Throws<JsonException>(() => ScenarioIO.Parse("{}"));
    }
}
