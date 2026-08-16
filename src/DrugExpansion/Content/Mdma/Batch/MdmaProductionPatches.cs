#if IL2CPPMELON
using S1ItemFramework = Il2CppScheduleOne.ItemFramework;
using S1Product = Il2CppScheduleOne.Product;
using S1StationFramework = Il2CppScheduleOne.StationFramework;
using S1Storage = Il2CppScheduleOne.Storage;
#elif MONOMELON
using S1ItemFramework = ScheduleOne.ItemFramework;
using S1Product = ScheduleOne.Product;
using S1StationFramework = ScheduleOne.StationFramework;
using S1Storage = ScheduleOne.Storage;
#endif

using HarmonyLib;
using UnityEngine;

namespace DrugExpansion.Content.Mdma.Batch;

internal static class MdmaProductionPatches
{
    [HarmonyPatch(
        typeof(S1StationFramework.StationRecipe),
        nameof(S1StationFramework.StationRecipe.GetProductInstance),
        new[] { typeof(S1ItemFramework.EQuality) })]
    private static class StationRecipeGetProductInstancePatch
    {
        private static void Postfix(
            S1ItemFramework.EQuality quality,
            S1Storage.StorableItemInstance __result)
        {
            S1ItemFramework.QualityItemInstance? item = AsQuality(__result);
            if (item == null ||
                !MdmaProductIds.TryGetForm(
                    item.ID,
                    out MdmaProductForm form))
            {
                return;
            }

            MdmaBatchProfile crystals = MdmaBatchFactory.CreateReaction(
                ingredientQuality: Math.Clamp((int)quality, 0, 4),
                processControl: 0.72d,
                contaminationPressure:
                    0.15d + UnityEngine.Random.value * 0.35d);
            MdmaBatchProfile profile =
                form == MdmaProductForm.Crystals
                    ? crystals
                    : crystals.Press(
                        MdmaTabletColor.Pink,
                        MdmaTabletImprint.Heart,
                        string.Empty);

            MdmaBatchRegistry.Attach(item, profile);
        }
    }

#if IL2CPPMELON
    private static S1ItemFramework.QualityItemInstance? AsQuality(
        S1Storage.StorableItemInstance? instance) =>
        instance?.TryCast<S1ItemFramework.QualityItemInstance>();
#else
    private static S1ItemFramework.QualityItemInstance? AsQuality(
        S1Storage.StorableItemInstance? instance) =>
        instance as S1ItemFramework.QualityItemInstance;
#endif
}
