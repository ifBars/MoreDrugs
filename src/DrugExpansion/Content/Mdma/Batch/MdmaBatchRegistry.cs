#if IL2CPPMELON
using S1ItemFramework = Il2CppScheduleOne.ItemFramework;
using S1PersistenceData = Il2CppScheduleOne.Persistence.Datas;
using S1Product = Il2CppScheduleOne.Product;
#elif MONOMELON
using S1ItemFramework = ScheduleOne.ItemFramework;
using S1PersistenceData = ScheduleOne.Persistence.Datas;
using S1Product = ScheduleOne.Product;
#endif

using System.Runtime.CompilerServices;

namespace DrugExpansion.Content.Mdma.Batch;

internal static class MdmaBatchRegistry
{
    private static readonly ConditionalWeakTable<
        S1ItemFramework.QualityItemInstance,
        MdmaBatchProfile> Profiles = new();

    private static readonly ConditionalWeakTable<
        S1PersistenceData.SaveData,
        MdmaBatchProfile> SaveProfiles = new();

    internal static bool TryGet(
        S1ItemFramework.QualityItemInstance? instance,
        out MdmaBatchProfile? profile)
    {
        profile = null;
        return instance != null &&
               MdmaProductIds.TryGetForm(instance.ID, out _) &&
               Profiles.TryGetValue(instance, out profile);
    }

    internal static MdmaBatchProfile GetOrCreate(
        S1ItemFramework.QualityItemInstance instance)
    {
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));
        if (!MdmaProductIds.TryGetForm(instance.ID, out MdmaProductForm form))
        {
            throw new ArgumentException(
                $"Item '{instance.ID}' is not an MDMA product.",
                nameof(instance));
        }

        if (Profiles.TryGetValue(instance, out MdmaBatchProfile? profile))
            return profile;

        profile = MdmaBatchFactory.CreateFallback(form);
        Profiles.Add(instance, profile);
        return profile;
    }

    internal static void Attach(
        S1ItemFramework.QualityItemInstance instance,
        MdmaBatchProfile profile)
    {
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));
        if (!MdmaProductIds.TryGetForm(instance.ID, out MdmaProductForm form))
        {
            throw new ArgumentException(
                $"Item '{instance.ID}' is not an MDMA product.",
                nameof(instance));
        }
        if (profile.Form != form)
        {
            throw new ArgumentException(
                $"Batch form '{profile.Form}' does not match item '{instance.ID}'.",
                nameof(profile));
        }

        Profiles.Remove(instance);
        Profiles.Add(instance, profile);
    }

    internal static void Copy(
        S1ItemFramework.QualityItemInstance source,
        S1ItemFramework.QualityItemInstance destination)
    {
        if (!MdmaProductIds.TryGetForm(
                source.ID,
                out MdmaProductForm sourceForm) ||
            !MdmaProductIds.TryGetForm(
                destination.ID,
                out MdmaProductForm destinationForm) ||
            sourceForm != destinationForm)
            return;

        Attach(destination, GetOrCreate(source));
    }

    internal static void AssociateSaveData(
        S1PersistenceData.SaveData data,
        MdmaBatchProfile profile)
    {
        SaveProfiles.Remove(data);
        SaveProfiles.Add(data, profile);
    }

    internal static bool TryGetSaveProfile(
        S1PersistenceData.SaveData data,
        out MdmaBatchProfile? profile) =>
        SaveProfiles.TryGetValue(data, out profile);

    internal static S1Product.ProductItemInstance? AsProduct(
        S1ItemFramework.ItemInstance? instance)
    {
#if IL2CPPMELON
        return instance?.TryCast<S1Product.ProductItemInstance>();
#else
        return instance as S1Product.ProductItemInstance;
#endif
    }

    internal static S1ItemFramework.QualityItemInstance? AsQuality(
        S1ItemFramework.ItemInstance? instance)
    {
#if IL2CPPMELON
        return instance?.TryCast<S1ItemFramework.QualityItemInstance>();
#else
        return instance as S1ItemFramework.QualityItemInstance;
#endif
    }
}
