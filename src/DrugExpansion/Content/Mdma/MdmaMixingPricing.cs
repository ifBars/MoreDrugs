using System;
using System.Collections.Generic;

namespace DrugExpansion.Content.Mdma;

/// <summary>
/// Prices an MDMA mixing output from its resolved effects instead of a flat
/// constant.
/// </summary>
/// <remarks>
/// S1API's custom-product mixing output factory only supplies the pre-mix
/// price, not the effects the native mixer just applied, so it cannot itself
/// reproduce the native "base price times one plus the sum of each active
/// effect's price multiplier" formula. This mirrors that formula against a
/// fixed table of Schedule I's known per-effect multipliers; the resolved
/// effect IDs are supplied separately by <see cref="Batch.MdmaMixingPriceCapture"/>.
/// </remarks>
internal static class MdmaMixingPricing
{
    private static readonly IReadOnlyDictionary<string, float> EffectPriceMultipliers =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
        {
            ["antigravity"] = 0.54f,
            ["athletic"] = 0.32f,
            ["balding"] = 0.30f,
            ["brighteyed"] = 0.40f,
            ["calming"] = 0.10f,
            ["caloriedense"] = 0.28f,
            ["cyclopean"] = 0.56f,
            ["disorienting"] = 0f,
            ["electrifying"] = 0.50f,
            ["energizing"] = 0.22f,
            ["euphoric"] = 0.18f,
            ["explosive"] = 0f,
            ["focused"] = 0.16f,
            ["foggy"] = 0.36f,
            ["gingeritis"] = 0.20f,
            ["glowie"] = 0.48f,
            ["jennerising"] = 0.42f,
            ["laxative"] = 0f,
            ["lethal"] = 0f,
            ["longfaced"] = 0.52f,
            ["munchies"] = 0.12f,
            ["paranoia"] = 0f,
            ["refreshing"] = 0.14f,
            ["schizophrenic"] = 0f,
            ["sedating"] = 0.26f,
            ["seizure"] = 0f,
            ["shrinking"] = 0.60f,
            ["slippery"] = 0.34f,
            ["smelly"] = 0f,
            ["sneaky"] = 0.24f,
            ["spicy"] = 0.38f,
            ["thoughtprovoking"] = 0.44f,
            ["toxic"] = 0f,
            ["tropicthunder"] = 0.46f,
            ["zombifying"] = 0.58f,
        };

    /// <summary>
    /// Calculates the native-equivalent mixed price for MDMA from its final
    /// resolved effect IDs: base price times one plus the sum of each known
    /// effect's price multiplier, clamped to the native 1-999 output range.
    /// </summary>
    internal static float CalculatePrice(IReadOnlyList<string> resolvedEffectIds)
    {
        double multiplierSum = 0d;
        for (int i = 0; i < resolvedEffectIds.Count; i++)
        {
            if (EffectPriceMultipliers.TryGetValue(
                    resolvedEffectIds[i],
                    out float multiplier))
            {
                multiplierSum += multiplier;
            }
        }

        double price = MdmaEconomyPolicy.ProductPrice * (1d + multiplierSum);
        return Math.Clamp(
            (float)Math.Round(price, MidpointRounding.AwayFromZero),
            1f,
            999f);
    }
}
