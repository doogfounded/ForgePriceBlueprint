using System.IO;
using System.Text.Json;
using Forge.Services;

namespace Forge.Tests
{
    public class ServiceTests
    {
        private static string SampleBlueprint() => BlueprintFileService.Serialize(
            new BlueprintBuilder("Test")
                .SetBasePrice("Base", 100.0)
                .AddVolumeTiers("Tiers", "quantity", (10, 0.05), (50, 0.10))
                .AddPercentageAdjustment("Partner", 0.90, "is_partner", "10% off")
                .Build());

        [Fact]
        public void Engine_ComputesPriceAndAuditTrail()
        {
            var r = new PricingEngine().Calculate(SampleBlueprint(), new Dictionary<string, object>
            {
                ["base_price"] = 200.0, ["quantity"] = 15.0, ["is_partner"] = true
            });

            Assert.Equal(200.0, r.BasePrice);
            Assert.Equal(171.0, r.FinalPrice, 6);
            Assert.Equal(3, r.AuditTrail.Count);
        }

        [Fact]
        public void Engine_ReportsErrorsAsException()
        {
            Assert.Throws<PricingEngineException>(() => PricingEngine.Parse(new PricingEngine().CalculateRaw("not json", "{}")));
        }

        [Fact]
        public void Engine_HandlesLargeOutputViaRetry()
        {
            var b = new BlueprintBuilder("Big").SetBasePrice("Base", 10.0);
            for (int i = 0; i < 200; i++) b.AddFlatAdjustment($"Rule {i} " + new string('x', 40), 0.0, "quantity >= 0", new string('d', 40));
            var r = new PricingEngine().Calculate(BlueprintFileService.Serialize(b.Build()), new Dictionary<string, object> { ["quantity"] = 1.0 });
            Assert.True(r.AuditTrail.Count > 100);
        }

        [Fact]
        public void Engine_IsThreadSafe()
        {
            var engine = new PricingEngine();
            string bp = SampleBlueprint();
            int bad = 0;
            Parallel.For(0, 1000, i =>
            {
                var r = engine.Calculate(bp, new Dictionary<string, object> { ["base_price"] = 100.0 + i, ["quantity"] = 1.0 });
                if (r.BasePrice != 100.0 + i) Interlocked.Increment(ref bad);
            });
            Assert.Equal(0, bad);
        }

        [Fact]
        public void FileService_RoundTripsAndRejectsInvalid()
        {
            string dir = Directory.CreateTempSubdirectory().FullName;
            string path = Path.Combine(dir, "bp.json");
            try
            {
                BlueprintFileService.Save(path, SampleBlueprint());
                Assert.False(File.Exists(path + ".tmp"));
                Assert.Equal(SampleBlueprint(), BlueprintFileService.Open(path));

                var ex = Assert.Throws<BlueprintLoadException>(() => BlueprintFileService.Save(path, "{\"BlueprintName\":\"\"}"));
                Assert.NotEmpty(ex.Errors);
                Assert.Equal(SampleBlueprint(), File.ReadAllText(path)); // original untouched

                Assert.Throws<BlueprintLoadException>(() => BlueprintFileService.Validate("{ nope"));
                Assert.Throws<BlueprintLoadException>(() => BlueprintFileService.Open(Path.Combine(dir, "missing.json")));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void RecentFiles_DedupesCapsAndPersists()
        {
            string dir = Directory.CreateTempSubdirectory().FullName;
            try
            {
                var svc = new RecentFilesService(Path.Combine(dir, "recent.json"), max: 3);
                foreach (var n in new[] { "a", "b", "c", "a", "d" }) svc.Add(Path.Combine(dir, n + ".json"));

                var list = new RecentFilesService(Path.Combine(dir, "recent.json"), 3).Load();
                Assert.Equal(new[] { "d", "a", "c" }, list.Select(p => Path.GetFileNameWithoutExtension(p)));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void Analyzer_FlagsNegativePrice()
        {
            string bp = BlueprintFileService.Serialize(new BlueprintBuilder("Bad")
                .SetBasePrice("Base", 100.0)
                .AddFlatAdjustment("Huge discount", -500.0, "quantity >= 1", "oops")
                .Build());

            var report = new BlueprintAnalyzer(new PricingEngine()).Analyze(bp);

            Assert.False(report.Valid);
            Assert.Contains(report.Anomalies, a => a.Rule == "Negative/Zero Price");
        }

        [Fact]
        public void Analyzer_RunsFullGrid()
        {
            var report = new BlueprintAnalyzer(new PricingEngine()).Analyze(SampleBlueprint());
            Assert.True(report.ScenariosTested > 0);
            Assert.Equal(0, report.ScenariosTested % 12); // 2 base prices x 2 regions x 3 modes per quantity
        }
    }
}
