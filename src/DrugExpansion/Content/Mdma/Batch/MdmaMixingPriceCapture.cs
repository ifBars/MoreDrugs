#if IL2CPPMELON
using S1Effects = Il2CppScheduleOne.Effects;
using EffectList = Il2CppSystem.Collections.Generic.List<Il2CppScheduleOne.Effects.Effect>;
#elif MONOMELON
using S1Effects = ScheduleOne.Effects;
using EffectList = System.Collections.Generic.List<ScheduleOne.Effects.Effect>;
#endif

using System.Collections.Generic;
using HarmonyLib;

namespace DrugExpansion.Content.Mdma.Batch;

/// <summary>
/// Captures the native mixing calculator's most recently resolved effect list.
/// </summary>
/// <remarks>
/// S1API's custom-product mixing output factory (used by
/// <c>MdmaModule.RegisterMixing</c>) is only given the pre-mix price, not the
/// effects the native calculator just applied. S1API computes that effect
/// list one step earlier in the same synchronous call by invoking
/// <see cref="S1Effects.EffectMixCalculator.MixProperties"/> directly, so
/// capturing its result here makes it available to the output factory a few
/// lines later in that same call.
/// </remarks>
internal static class MdmaMixingPriceCapture
{
    private static readonly List<string> LastResolvedEffectIds = new();

    internal static IReadOnlyList<string> ConsumeLastResolvedEffectIds()
    {
        List<string> snapshot = new(LastResolvedEffectIds);
        LastResolvedEffectIds.Clear();
        return snapshot;
    }

    [HarmonyPatch(typeof(S1Effects.EffectMixCalculator), "MixProperties")]
    private static class MixPropertiesCapturePatch
    {
        // Runs after S1API's own reaction postfix (default Normal priority),
        // so mod-added reaction effects are already reflected in the capture.
        [HarmonyPriority(Priority.High)]
        private static void Postfix(EffectList __result)
        {
            LastResolvedEffectIds.Clear();
            if (__result == null)
                return;

            for (int i = 0; i < __result.Count; i++)
            {
                S1Effects.Effect? effect = __result[i];
                if (effect != null)
                    LastResolvedEffectIds.Add(effect.ID);
            }
        }
    }
}
