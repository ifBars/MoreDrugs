using DrugExpansion.Content.Mdma;

namespace DrugExpansion.Tests;

public sealed class MdmaMixingPricingTests
{
    [Fact]
    public void NoResolvedEffectsPricesAtTheUnmixedBase()
    {
        Assert.Equal(
            MdmaEconomyPolicy.ProductPrice,
            MdmaMixingPricing.CalculatePrice(Array.Empty<string>()));
    }

    [Fact]
    public void SingleKnownEffectAppliesItsMultiplier()
    {
        // Energizing: base price * (1 + 0.22) = 190 * 1.22 = 231.8 -> 232.
        Assert.Equal(
            232f,
            MdmaMixingPricing.CalculatePrice(new[] { "energizing" }));
    }

    [Fact]
    public void MultipleEffectsStackAdditively()
    {
        // Energizing (0.22) + Focused (0.16) = 0.38 -> 190 * 1.38 = 262.2 -> 262.
        Assert.Equal(
            262f,
            MdmaMixingPricing.CalculatePrice(new[] { "energizing", "focused" }));
    }

    [Fact]
    public void UnknownEffectIdsContributeNoMultiplier()
    {
        Assert.Equal(
            MdmaEconomyPolicy.ProductPrice,
            MdmaMixingPricing.CalculatePrice(new[] { "not-a-real-effect" }));
    }

    [Fact]
    public void EffectIdLookupIsCaseInsensitive()
    {
        Assert.Equal(
            MdmaMixingPricing.CalculatePrice(new[] { "energizing" }),
            MdmaMixingPricing.CalculatePrice(new[] { "ENERGIZING" }));
    }

    [Fact]
    public void PriceNeverExceedsTheNativeMixingCeiling()
    {
        // A summed multiplier this large would push the price past 999 uncapped.
        string[] maxedEffects =
        {
            "shrinking",
            "zombifying",
            "cyclopean",
            "antigravity",
            "longfaced",
            "electrifying",
            "glowie",
            "tropicthunder",
            "sedating",
        };

        Assert.Equal(
            999f,
            MdmaMixingPricing.CalculatePrice(maxedEffects));
    }
}
