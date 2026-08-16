#if IL2CPPMELON
using FishReader = Il2CppFishNet.Serializing.Reader;
using FishWriter = Il2CppFishNet.Serializing.Writer;
using S1ItemFramework = Il2CppScheduleOne.ItemFramework;
using S1Persistence = Il2CppScheduleOne.Persistence;
using S1PersistenceData = Il2CppScheduleOne.Persistence.Datas;
using S1Product = Il2CppScheduleOne.Product;
#elif MONOMELON
using FishReader = FishNet.Serializing.Reader;
using FishWriter = FishNet.Serializing.Writer;
using S1ItemFramework = ScheduleOne.ItemFramework;
using S1Persistence = ScheduleOne.Persistence;
using S1PersistenceData = ScheduleOne.Persistence.Datas;
using S1Product = ScheduleOne.Product;
#endif

using HarmonyLib;
using MelonLoader;

namespace DrugExpansion.Content.Mdma.Batch;

internal static class MdmaBatchPersistencePatches
{
    private const string JsonField = "_moreDrugsMdmaBatchV1";
    private const string JsonNeedle = "\"" + JsonField + "\":\"";

    [HarmonyPatch(
        typeof(S1Product.ProductItemInstance),
        nameof(S1Product.ProductItemInstance.CanStackWith))]
    private static class CanStackWithPatch
    {
        private static void Postfix(
            S1Product.ProductItemInstance __instance,
            S1ItemFramework.ItemInstance other,
            ref bool __result)
        {
            if (!__result ||
                !IsTablets(__instance.ID))
            {
                return;
            }

            S1Product.ProductItemInstance? otherProduct =
                MdmaBatchRegistry.AsProduct(other);
            __result =
                otherProduct != null &&
                IsTablets(otherProduct.ID) &&
                MdmaBatchRegistry.GetOrCreate(__instance)
                    .Equals(MdmaBatchRegistry.GetOrCreate(otherProduct));
        }
    }

    [HarmonyPatch(
        typeof(S1Product.ProductItemInstance),
        nameof(S1Product.ProductItemInstance.GetCopy))]
    private static class GetCopyPatch
    {
        private static void Postfix(
            S1Product.ProductItemInstance __instance,
            S1ItemFramework.ItemInstance __result)
        {
            S1Product.ProductItemInstance? copy =
                MdmaBatchRegistry.AsProduct(__result);
            if (copy != null && IsTablets(__instance.ID))
                MdmaBatchRegistry.Copy(__instance, copy);
        }
    }

    [HarmonyPatch(
        typeof(S1Product.ProductItemInstance),
        nameof(S1Product.ProductItemInstance.GetItemData))]
    private static class GetItemDataPatch
    {
        private static void Postfix(
            S1Product.ProductItemInstance __instance,
            S1PersistenceData.ItemData __result)
        {
            if (__result != null && IsTablets(__instance.ID))
            {
                MdmaBatchRegistry.AssociateSaveData(
                    __result,
                    MdmaBatchRegistry.GetOrCreate(__instance));
            }
        }
    }

    [HarmonyPatch(
        typeof(S1PersistenceData.SaveData),
        nameof(S1PersistenceData.SaveData.GetJson))]
    private static class SaveDataGetJsonPatch
    {
        private static void Postfix(
            S1PersistenceData.SaveData __instance,
            ref string __result)
        {
            if (!MdmaBatchRegistry.TryGetSaveProfile(
                    __instance,
                    out MdmaBatchProfile? profile) ||
                profile == null ||
                string.IsNullOrEmpty(__result))
            {
                return;
            }

            int closingBrace = __result.LastIndexOf('}');
            if (closingBrace < 0)
                return;

            string separator =
                HasJsonProperties(__result, closingBrace) ? "," : string.Empty;
            string field =
                separator +
                JsonNeedle +
                MdmaBatchCodec.Encode(profile) +
                "\"";
            __result = __result.Insert(closingBrace, field);
        }
    }

    [HarmonyPatch(
        typeof(S1Persistence.ItemDeserializer),
        nameof(S1Persistence.ItemDeserializer.LoadItem))]
    private static class ItemDeserializerLoadItemPatch
    {
        private static void Postfix(
            string itemString,
            ref S1ItemFramework.ItemInstance __result)
        {
            S1ItemFramework.QualityItemInstance? item =
                MdmaBatchRegistry.AsQuality(__result);
            if (item == null ||
                !MdmaProductIds.TryGetForm(
                    item.ID,
                    out MdmaProductForm form))
            {
                return;
            }

            if (form == MdmaProductForm.Crystals &&
                MdmaBatchRegistry.AsProduct(item) != null)
            {
                item =
                    new S1ItemFramework.QualityItemInstance(
                        item.Definition,
                        item.Quantity,
                        item.Quality);
                __result = item;
            }

            if (TryReadJsonPayload(itemString, out string payload) &&
                MdmaBatchCodec.TryDecode(payload, out MdmaBatchProfile? profile) &&
                profile != null)
            {
                try
                {
                    MdmaBatchRegistry.Attach(item, profile);
                    return;
                }
                catch (ArgumentException)
                {
                    MelonLogger.Warning(
                        $"Ignored mismatched MDMA batch data for '{item.ID}'.");
                }
            }

            MdmaBatchRegistry.GetOrCreate(item);
        }
    }

    [HarmonyPatch(
        typeof(S1Product.ProductItemInstance),
        nameof(S1Product.ProductItemInstance.Write))]
    private static class ProductWritePatch
    {
        private static void Postfix(
            S1Product.ProductItemInstance __instance,
            FishWriter writer)
        {
            if (!IsTablets(__instance.ID))
                return;

            writer.WriteString(
                MdmaBatchCodec.Encode(
                    MdmaBatchRegistry.GetOrCreate(__instance)));
        }
    }

    [HarmonyPatch(
        typeof(S1Product.ProductItemInstance),
        nameof(S1Product.ProductItemInstance.Read))]
    private static class ProductReadPatch
    {
        private static void Postfix(
            S1Product.ProductItemInstance __instance,
            FishReader reader)
        {
            if (!IsTablets(__instance.ID))
                return;

            string payload = reader.ReadString();
            if (MdmaBatchCodec.TryDecode(
                    payload,
                    out MdmaBatchProfile? profile) &&
                profile != null)
            {
                try
                {
                    MdmaBatchRegistry.Attach(__instance, profile);
                    return;
                }
                catch (ArgumentException)
                {
                    MelonLogger.Warning(
                        $"Ignored mismatched network MDMA batch data for '{__instance.ID}'.");
                }
            }

            MdmaBatchRegistry.GetOrCreate(__instance);
        }
    }

    [HarmonyPatch(
        typeof(S1ItemFramework.QualityItemInstance),
        nameof(S1ItemFramework.QualityItemInstance.CanStackWith))]
    private static class CrystalCanStackWithPatch
    {
        private static void Postfix(
            S1ItemFramework.QualityItemInstance __instance,
            S1ItemFramework.ItemInstance other,
            ref bool __result)
        {
            if (!__result || !IsCrystals(__instance.ID))
                return;

            S1ItemFramework.QualityItemInstance? otherQuality =
                MdmaBatchRegistry.AsQuality(other);
            __result =
                otherQuality != null &&
                IsCrystals(otherQuality.ID) &&
                MdmaBatchRegistry.GetOrCreate(__instance)
                    .Equals(MdmaBatchRegistry.GetOrCreate(otherQuality));
        }
    }

    [HarmonyPatch(
        typeof(S1ItemFramework.QualityItemInstance),
        nameof(S1ItemFramework.QualityItemInstance.GetCopy))]
    private static class CrystalGetCopyPatch
    {
        private static void Postfix(
            S1ItemFramework.QualityItemInstance __instance,
            S1ItemFramework.ItemInstance __result)
        {
            if (!IsCrystals(__instance.ID))
                return;

            S1ItemFramework.QualityItemInstance? copy =
                MdmaBatchRegistry.AsQuality(__result);
            if (copy != null)
                MdmaBatchRegistry.Copy(__instance, copy);
        }
    }

    [HarmonyPatch(
        typeof(S1ItemFramework.QualityItemInstance),
        nameof(S1ItemFramework.QualityItemInstance.GetItemData))]
    private static class CrystalGetItemDataPatch
    {
        private static void Postfix(
            S1ItemFramework.QualityItemInstance __instance,
            S1PersistenceData.ItemData __result)
        {
            if (__result != null && IsCrystals(__instance.ID))
            {
                MdmaBatchRegistry.AssociateSaveData(
                    __result,
                    MdmaBatchRegistry.GetOrCreate(__instance));
            }
        }
    }

    [HarmonyPatch(
        typeof(S1ItemFramework.QualityItemInstance),
        nameof(S1ItemFramework.QualityItemInstance.Write))]
    private static class CrystalWritePatch
    {
        private static void Postfix(
            S1ItemFramework.QualityItemInstance __instance,
            FishWriter writer)
        {
            if (!IsCrystals(__instance.ID))
                return;

            writer.WriteString(
                MdmaBatchCodec.Encode(
                    MdmaBatchRegistry.GetOrCreate(__instance)));
        }
    }

    [HarmonyPatch(
        typeof(S1ItemFramework.QualityItemInstance),
        nameof(S1ItemFramework.QualityItemInstance.Read))]
    private static class CrystalReadPatch
    {
        private static void Postfix(
            S1ItemFramework.QualityItemInstance __instance,
            FishReader reader)
        {
            if (!IsCrystals(__instance.ID))
                return;

            string payload = reader.ReadString();
            if (MdmaBatchCodec.TryDecode(
                    payload,
                    out MdmaBatchProfile? profile) &&
                profile != null)
            {
                try
                {
                    MdmaBatchRegistry.Attach(__instance, profile);
                    return;
                }
                catch (ArgumentException)
                {
                    MelonLogger.Warning(
                        $"Ignored mismatched network MDMA batch data for '{__instance.ID}'.");
                }
            }

            MdmaBatchRegistry.GetOrCreate(__instance);
        }
    }

    private static bool IsTablets(string? itemId) =>
        string.Equals(
            itemId,
            MdmaProductIds.Tablets,
            StringComparison.OrdinalIgnoreCase);

    private static bool IsCrystals(string? itemId) =>
        string.Equals(
            itemId,
            MdmaProductIds.Crystals,
            StringComparison.OrdinalIgnoreCase);

    private static bool HasJsonProperties(string json, int closingBrace)
    {
        for (int index = 0; index < closingBrace; index++)
        {
            if (json[index] == ':')
                return true;
        }

        return false;
    }

    private static bool TryReadJsonPayload(
        string? json,
        out string payload)
    {
        payload = string.Empty;
        if (string.IsNullOrEmpty(json))
            return false;

        int start = json.IndexOf(JsonNeedle, StringComparison.Ordinal);
        if (start < 0)
            return false;

        start += JsonNeedle.Length;
        int end = json.IndexOf('"', start);
        if (end < 0)
            return false;

        payload = json.Substring(start, end - start);
        return payload.Length > 0;
    }
}
