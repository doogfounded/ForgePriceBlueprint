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

            var flat = doc.AddRule("FlatAdjustment");
            var round = doc.AddRule("Rounding");
            Assert.Equal(3, doc.Rules.Count);

            Assert.True(doc.MoveRule(round, -1));
            Assert.Same(round, doc.Rules[1]);
            Assert.False(doc.MoveRule(doc.Rules[0]!.AsObject(), -1)); // already first

            doc.RemoveRule(flat);
            Assert.Equal(2, doc.Rules.Count);

            doc.Name = "Renamed";
            doc.NotifyEdited();

            Assert.Equal(new[] { true, true, true, true, false, false }, events);
            BlueprintFileService.Validate(doc.ToJson());
        }

        [Theory]
        [InlineData("BasePrice")]
        [InlineData("PercentageAdjustment")]
        [InlineData("FlatAdjustment")]
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
    }
}
