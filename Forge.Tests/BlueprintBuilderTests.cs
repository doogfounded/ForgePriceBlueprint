using Xunit;
using Forge;

namespace Forge.Tests
{
    public class BlueprintBuilderTests
    {
        [Fact]
        public void Build_ShouldCreateBlueprintWithCorrectName()
        {
            // Arrange
            var builder = new BlueprintBuilder("Test Blueprint");

            // Act
            var blueprint = builder.Build();

            // Assert
            Assert.Equal("Test Blueprint", blueprint.BlueprintName);
        }

        [Fact]
        public void AddPercentageAdjustment_ShouldAddRuleToBlueprint()
        {
            // Arrange
            var builder = new BlueprintBuilder("Test Blueprint")
                .AddPercentageAdjustment("Partner Discount", 0.90, "is_partner", "Partner Channel 10% discount");

            // Act
            var blueprint = builder.Build();

            // Assert
            var rule = Assert.Single(blueprint.Rules);
            var percentageRule = Assert.IsType<PercentageAdjustmentRuleDto>(rule);
            Assert.Equal("Partner Discount", percentageRule.Name);
            Assert.Equal(0.90, percentageRule.Factor);
            Assert.Equal("is_partner", percentageRule.ConditionKey);
        }

        [Fact]
        public void AddFlatAdjustment_ShouldAddRuleToBlueprint()
        {
            // Arrange
            var builder = new BlueprintBuilder("Test Blueprint")
                .AddFlatAdjustment("Shipping surcharge", 15.0, "quantity < 10", "Flat shipping fee");

            // Act
            var blueprint = builder.Build();

            // Assert
            var rule = Assert.Single(blueprint.Rules);
            var flatRule = Assert.IsType<FlatAdjustmentRuleDto>(rule);
            Assert.Equal("Shipping surcharge", flatRule.Name);
            Assert.Equal(15.0, flatRule.Amount);
            Assert.Equal("quantity < 10", flatRule.ConditionKey);
        }

        [Fact]
        public void AddPriceCap_ShouldAddRuleToBlueprint()
        {
            // Arrange
            var builder = new BlueprintBuilder("Test Blueprint")
                .AddPriceCap("Price Floor", minPrice: 100.0, maxPrice: 500.0);

            // Act
            var blueprint = builder.Build();

            // Assert
            var rule = Assert.Single(blueprint.Rules);
            var capRule = Assert.IsType<PriceCapRuleDto>(rule);
            Assert.Equal("Price Floor", capRule.Name);
            Assert.Equal(100.0, capRule.MinPrice);
            Assert.Equal(500.0, capRule.MaxPrice);
        }
    }
}
