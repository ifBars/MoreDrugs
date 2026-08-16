#if IL2CPPMELON
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using S1 = Il2CppScheduleOne;
using S1AvatarEquipping = Il2CppScheduleOne.AvatarFramework.Equipping;
using S1Equipping = Il2CppScheduleOne.Equipping;
using S1ItemFramework = Il2CppScheduleOne.ItemFramework;
using S1StationFramework = Il2CppScheduleOne.StationFramework;
using S1Storage = Il2CppScheduleOne.Storage;
using S1Trash = Il2CppScheduleOne.Trash;
#elif MONOMELON
using S1 = ScheduleOne;
using S1AvatarEquipping = ScheduleOne.AvatarFramework.Equipping;
using S1Equipping = ScheduleOne.Equipping;
using S1ItemFramework = ScheduleOne.ItemFramework;
using S1StationFramework = ScheduleOne.StationFramework;
using S1Storage = ScheduleOne.Storage;
using S1Trash = ScheduleOne.Trash;
#endif

using MelonLoader;
using DrugExpansion.Infrastructure;
using S1API.Console;
using S1API.Items;
using S1API.Items.Quality;
using S1API.Items.Storable;
using S1API.Products;
using S1API.Rendering;
using UnityEngine;

namespace DrugExpansion.Content.Mdma.Precursors;

internal sealed class MdmaPrecursorCatalog : IDisposable
{
    private const string SafroleResource =
        "DrugExpansion.Assets.Models.safrole.glb";
    private const string MethylamineResource =
        "DrugExpansion.Assets.Models.methylamine.glb";
    private static readonly Color ClearPrecursorLiquidColor =
        new(0.94f, 0.97f, 1f, 0.45f);

    private readonly MelonLogger.Instance _logger;
    private readonly EmbeddedGlbAsset _safrole =
        new(SafroleResource, "DrugExpansion_Safrole");
    private readonly EmbeddedGlbAsset _methylamine =
        new(MethylamineResource, "DrugExpansion_Methylamine");
    private readonly DiscoDaveyAccessoryCatalog _daveyAccessories;
    private readonly Dictionary<
        string,
        S1API.Items.Quality.QualityItemDefinition> _safroleDefinitions =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _configuredPresentations =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _generatedIcons =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, S1Trash.TrashItem> _trashPrefabs =
        new(StringComparer.OrdinalIgnoreCase);

    private S1API.Items.Storable.StorableItemDefinition?
        _methylamineDefinition;

    internal MdmaPrecursorCatalog(MelonLogger.Instance logger)
    {
        _logger = logger;
        _daveyAccessories =
            new DiscoDaveyAccessoryCatalog(logger);
    }

    internal void RegisterContent()
    {
        _daveyAccessories.Register();

        if (_safroleDefinitions.Count > 0 &&
            _methylamineDefinition != null)
        {
            return;
        }

        RegisterSafrole(
            MdmaPrecursorIds.SafroleLow,
            "Low-Grade Safrole",
            "A chemical ingredient used in a chemistry station.",
            Quality.Standard,
            MdmaEconomyPolicy.SafroleLowPrice,
            "SafroleLabel_Low");
        RegisterSafrole(
            MdmaPrecursorIds.SafroleStandard,
            "Safrole",
            "A chemical ingredient used in a chemistry station.",
            Quality.Premium,
            MdmaEconomyPolicy.SafroleStandardPrice,
            "SafroleLabel_Standard");
        RegisterSafrole(
            MdmaPrecursorIds.SafroleHigh,
            "High-Grade Safrole",
            "A chemical ingredient used in a chemistry station.",
            Quality.Heavenly,
            MdmaEconomyPolicy.SafroleHighPrice,
            "SafroleLabel_High");

        _methylamineDefinition ??=
            S1API.Items.Storable.ItemCreator
                .CloneFrom("acid")
                .WithBasicInfo(
                    MdmaPrecursorIds.Methylamine,
                    "Methylamine",
                    "A common chemical ingredient used in a chemistry station.",
                    ItemCategory.Ingredient)
                .WithStackLimit(20)
                .WithPricing(
                    MdmaEconomyPolicy.MethylaminePrice,
                    0.25f)
                .WithLegalStatus(LegalStatus.Illegal)
                .Build();

        TryConfigurePresentation(
            MdmaPrecursorIds.Methylamine,
            _methylamine.GetOrLoad(),
            labelVariant: null,
            ClearPrecursorLiquidColor);

        ConsoleItemAliases.Register(
            "safrole",
            MdmaPrecursorIds.SafroleStandard);
        ConsoleItemAliases.Register(
            "safrolelow",
            MdmaPrecursorIds.SafroleLow);
        ConsoleItemAliases.Register(
            "safrolehigh",
            MdmaPrecursorIds.SafroleHigh);
        ConsoleItemAliases.Register(
            "methylamine",
            MdmaPrecursorIds.Methylamine);

        _logger.Msg(
            "Registered three Safrole quality variants and Methylamine for Disco Davey's precursor supply.");
    }

    internal void CompleteLoad()
    {
        GameObject safroleSource = _safrole.GetOrLoad();
        foreach (SafroleSpec spec in SafroleSpecs)
        {
            TryConfigurePresentation(
                spec.Id,
                safroleSource,
                spec.LabelVariant,
                ClearPrecursorLiquidColor);
            TryGenerateIcon(spec.Id, safroleSource, spec.LabelVariant);
        }

        GameObject methylamineSource = _methylamine.GetOrLoad();
        TryConfigurePresentation(
            MdmaPrecursorIds.Methylamine,
            methylamineSource,
            labelVariant: null,
            ClearPrecursorLiquidColor);
        TryGenerateIcon(
            MdmaPrecursorIds.Methylamine,
            methylamineSource,
            labelVariant: null);

        TryRegisterTrashPrefabs();
    }

    public void Dispose()
    {
        _daveyAccessories.Dispose();
        _methylamine.Dispose();
        _safrole.Dispose();
    }

    private void RegisterSafrole(
        string id,
        string name,
        string description,
        Quality quality,
        float price,
        string labelVariant)
    {
        if (_safroleDefinitions.ContainsKey(id))
            return;

        S1API.Items.Quality.QualityItemDefinition definition =
            S1API.Items.Quality.QualityItemCreator
                .CloneFrom("pseudo")
                .WithBasicInfo(
                    id,
                    name,
                    description,
                    ItemCategory.Ingredient)
                .WithStackLimit(20)
                .WithPricing(price, 0.25f)
                .WithLegalStatus(LegalStatus.Illegal)
                .WithDefaultQuality(quality)
                .Build();
        _safroleDefinitions.Add(id, definition);

        TryConfigurePresentation(
            id,
            _safrole.GetOrLoad(),
            labelVariant,
            ClearPrecursorLiquidColor);
    }

    private void TryConfigurePresentation(
        string itemId,
        GameObject visualSource,
        string? labelVariant,
        Color liquidColor)
    {
        if (_configuredPresentations.Contains(itemId))
            return;

        try
        {
            S1ItemFramework.StorableItemDefinition definition =
                GetNativeStorableDefinition(itemId) ??
                throw new InvalidOperationException(
                    $"The registered precursor '{itemId}' is unavailable.");
            S1ItemFramework.StorableItemDefinition template =
                GetNativeStorableDefinition("acid") ??
                throw new InvalidOperationException(
                    "The native Acid presentation scaffold is unavailable.");
            if (template.StoredItem == null ||
                template.Equippable == null ||
                template.StationItem == null)
            {
                throw new InvalidOperationException(
                    "The native Acid scaffold is missing a stored, held, or station representation.");
            }

            definition.StoredItem = CloneStoredItem(
                template.StoredItem,
                visualSource,
                itemId,
                labelVariant);
            definition.Equippable = CloneEquippable(
                template.Equippable,
                visualSource,
                itemId,
                labelVariant);
            definition.StationItem = CloneStationItem(
                template.StationItem,
                visualSource,
                itemId,
                labelVariant,
                liquidColor);

            _configuredPresentations.Add(itemId);
        }
        catch (Exception exception)
        {
            _logger.Warning(
                $"Precursor presentation for '{itemId}' will retry after load: {exception.Message}");
        }
    }

    private static S1Storage.StoredItem CloneStoredItem(
        S1Storage.StoredItem template,
        GameObject source,
        string itemId,
        string? labelVariant)
    {
        S1Storage.StoredItem stored =
            UnityEngine.Object.Instantiate(template);
        PreparePrefab(stored.gameObject, $"{itemId}_Stored");
        ReplaceNativeVisual(
            stored.gameObject,
            source,
            labelVariant,
            Vector3.zero,
            Vector3.zero,
            Vector3.one);
        return stored;
    }

    private static S1Equipping.Equippable CloneEquippable(
        S1Equipping.Equippable template,
        GameObject source,
        string itemId,
        string? labelVariant)
    {
        HeldItemPose viewmodelPose = GetViewmodelPose();
        S1Equipping.Equippable equippable =
            UnityEngine.Object.Instantiate(template);
        PreparePrefab(equippable.gameObject, $"{itemId}_Equippable");
        ReplaceNativeVisual(
            equippable.gameObject,
            source,
            labelVariant,
            viewmodelPose.LocalPosition,
            viewmodelPose.LocalEulerAngles,
            Vector3.one * viewmodelPose.UniformScale);

        S1Equipping.Equippable_Viewmodel? viewmodel =
            AsViewmodel(equippable);
        if (viewmodel != null)
        {
            viewmodel.AvatarEquippable =
                CreateAvatarEquippable(
                    viewmodel.AvatarEquippable,
                    source,
                    itemId,
                    labelVariant);
        }

        return equippable;
    }

    private static S1AvatarEquipping.AvatarEquippable
        CreateAvatarEquippable(
            S1AvatarEquipping.AvatarEquippable? template,
            GameObject source,
            string itemId,
            string? labelVariant)
    {
        string assetPath = $"DrugExpansion/Items/{Sanitize(itemId)}/Held";
        HeldItemPose pose = GetHeldItemPose(itemId);
        var root = new GameObject($"{Sanitize(itemId)}_AvatarEquippable");
        PreparePrefab(root, root.name);
        ReplaceNativeVisual(
            root,
            source,
            labelVariant,
            pose.LocalPosition,
            pose.LocalEulerAngles,
            Vector3.one * pose.UniformScale);

        S1AvatarEquipping.AvatarEquippable avatar =
            root.AddComponent<S1AvatarEquipping.AvatarEquippable>();
        avatar.AlignmentPoint = root.transform;
        avatar.AssetPath = assetPath;
        if (template != null)
        {
            avatar.Suspiciousness = template.Suspiciousness;
            avatar.Hand = template.Hand;
            avatar.TriggerType = template.TriggerType;
            avatar.AnimationTrigger = template.AnimationTrigger;
        }

        if (!AvatarEquippableRegistry.RegisterAvatarEquippable(
                assetPath,
                root))
        {
            throw new InvalidOperationException(
                $"Could not register precursor avatar presentation '{assetPath}'.");
        }

        return avatar;
    }

    private static HeldItemPose GetViewmodelPose() =>
        new(
            Vector3.zero,
            new Vector3(0f, 180f, 0f),
            1.8f);

    private static HeldItemPose GetHeldItemPose(string itemId) =>
        string.Equals(
            itemId,
            MdmaPrecursorIds.Methylamine,
            StringComparison.OrdinalIgnoreCase)
            ? new HeldItemPose(
                new Vector3(0f, -0.16f, 0f),
                new Vector3(0f, 270f, 0f),
                2.25f)
            : new HeldItemPose(
                new Vector3(0f, -0.14f, 0f),
                new Vector3(0f, 270f, 0f),
                2.25f);

    private S1StationFramework.StationItem CloneStationItem(
        S1StationFramework.StationItem template,
        GameObject source,
        string itemId,
        string? labelVariant,
        Color liquidColor)
    {
        GameObject stagingRoot =
            new($"{Sanitize(itemId)}_StationStaging")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
        stagingRoot.SetActive(false);

        try
        {
            S1StationFramework.StationItem station =
                UnityEngine.Object.Instantiate(
                    template,
                    stagingRoot.transform,
                    false);

            S1StationFramework.PourableModule? pourable =
                station.GetModule<S1StationFramework.PourableModule>();
            if (pourable == null)
            {
                throw new InvalidOperationException(
                    "The cloned Acid station item has no PourableModule.");
            }

            if (pourable.Draggable == null ||
                pourable.LiquidContainer == null ||
                pourable.LiquidContainer.LiquidVolume == null ||
                pourable.PourPoint == null)
            {
                throw new InvalidOperationException(
                    "The cloned Acid station item is missing its native pourable references.");
            }

            GameObject visual = ReplaceNativeVisual(
                station.gameObject,
                source,
                labelVariant,
                Vector3.zero,
                Vector3.zero,
                Vector3.one,
                pourable.Draggable.transform,
                pourable.LiquidContainer.LiquidVolume.transform);
            ConfigureStationPourableVisual(visual, pourable, itemId);

            pourable.LiquidType = itemId;
            pourable.LiquidColor = liquidColor;
            pourable.PourParticlesColor = liquidColor;
            pourable.DefaultLiquid_L = pourable.LiquidCapacity_L;
            pourable.LiquidContainer.AdjustMurkiness = false;
            pourable.LiquidContainer.SetLiquidColor(
                liquidColor,
                setColorVariable: true,
                updateLigting: false);
            pourable.LiquidContainer.LiquidVolume.murkiness = 0.05f;
            pourable.LiquidContainer.LiquidVolume.emissionColor =
                Color.black;

            station.TrashPrefab = CloneTrashPrefab(
                station.TrashPrefab,
                source,
                itemId,
                labelVariant);

            PreparePrefab(station.gameObject, $"{itemId}_Station");
            return station;
        }
        finally
        {
            UnityEngine.Object.Destroy(stagingRoot);
        }
    }

    private S1Trash.TrashItem CloneTrashPrefab(
        S1Trash.TrashItem? template,
        GameObject source,
        string itemId,
        string? labelVariant)
    {
        if (template == null)
        {
            throw new InvalidOperationException(
                "The cloned Acid station item has no trash prefab.");
        }

        S1Trash.TrashItem trash =
            UnityEngine.Object.Instantiate(template);
        string trashId = $"{itemId}_empty";
        trash.ID = trashId;
        PreparePrefab(trash.gameObject, $"{Sanitize(itemId)}_Trash");

        GameObject visual = ReplaceNativeVisual(
            trash.gameObject,
            source,
            labelVariant,
            Vector3.zero,
            Vector3.zero,
            Vector3.one);
        ConfigureEmptyContainerVisual(visual, itemId);

        _trashPrefabs[trashId] = trash;
        return trash;
    }

    private static void ConfigureEmptyContainerVisual(
        GameObject visual,
        string itemId)
    {
        bool isMethylamine = string.Equals(
            itemId,
            MdmaPrecursorIds.Methylamine,
            StringComparison.OrdinalIgnoreCase);
        string prefix = isMethylamine
            ? "Methylamine"
            : "Safrole";

        RequireDescendant(visual, $"{prefix}Cap")
            .gameObject.SetActive(false);
        RequireDescendant(visual, $"{prefix}LiquidVolume")
            .gameObject.SetActive(false);
        RequireDescendant(visual, $"{prefix}PourPoint")
            .gameObject.SetActive(false);

        string liquidWindow = isMethylamine
            ? "MethylamineLiquidWindow"
            : "SafroleLiquid";
        RequireDescendant(visual, liquidWindow)
            .gameObject.SetActive(false);
    }

    private void TryRegisterTrashPrefabs()
    {
        S1Trash.TrashManager? manager = S1Trash.TrashManager.Instance;
        if (manager == null)
        {
            _logger.Warning(
                "Precursor trash prefabs will retry because TrashManager is unavailable.");
            return;
        }

        foreach (S1Trash.TrashItem trash in _trashPrefabs.Values)
            RegisterTrashPrefab(manager, trash);
    }

    private static void RegisterTrashPrefab(
        S1Trash.TrashManager manager,
        S1Trash.TrashItem trash)
    {
#if IL2CPPMELON
        Il2CppReferenceArray<S1Trash.TrashItem>? prefabs =
            manager.TrashPrefabs;
        int count = prefabs?.Length ?? 0;
        for (int index = 0; index < count; index++)
        {
            if (!string.Equals(
                    prefabs![index]?.ID,
                    trash.ID,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            prefabs[index] = trash;
            return;
        }

        var expanded =
            new Il2CppReferenceArray<S1Trash.TrashItem>(count + 1);
        for (int index = 0; index < count; index++)
            expanded[index] = prefabs![index];
        expanded[count] = trash;
        manager.TrashPrefabs = expanded;
#else
        S1Trash.TrashItem[] prefabs =
            manager.TrashPrefabs ?? Array.Empty<S1Trash.TrashItem>();
        for (int index = 0; index < prefabs.Length; index++)
        {
            if (!string.Equals(
                    prefabs[index]?.ID,
                    trash.ID,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            prefabs[index] = trash;
            manager.TrashPrefabs = prefabs;
            return;
        }

        Array.Resize(ref prefabs, prefabs.Length + 1);
        prefabs[prefabs.Length - 1] = trash;
        manager.TrashPrefabs = prefabs;
#endif
    }

    private void TryGenerateIcon(
        string itemId,
        GameObject visualSource,
        string? labelVariant)
    {
        if (_generatedIcons.Contains(itemId))
            return;

        GameObject? iconRoot = null;
        try
        {
            iconRoot = new GameObject($"{Sanitize(itemId)}_IconSource");
            GameObject visual =
                UnityEngine.Object.Instantiate(visualSource);
            visual.name = $"{Sanitize(itemId)}_IconVisual";
            visual.transform.SetParent(iconRoot.transform, false);
            visual.transform.localEulerAngles = new Vector3(18f, 66f, 0f);
            visual.transform.localScale = Vector3.one;
            ConfigureLabelVariant(visual, labelVariant);
            visual.SetActive(true);

            Sprite? icon = IconFactory.GenerateIconSprite(
                iconRoot.transform,
                size: 512,
                bakeSkinnedMeshes: true,
                fitToCamera: true,
                cameraFill: 0.80f);
            if (icon == null)
                return;

            ItemDefinition definition =
                ItemManager.GetDefinition(itemId) ??
                throw new InvalidOperationException(
                    $"The precursor definition '{itemId}' is unavailable for icon assignment.");
            definition.Icon = icon;
            _generatedIcons.Add(itemId);
        }
        catch (Exception exception)
        {
            _logger.Warning(
                $"Precursor icon for '{itemId}' will retry after the next load: {exception.Message}");
        }
        finally
        {
            if (iconRoot != null)
                UnityEngine.Object.Destroy(iconRoot);
        }
    }

    private static GameObject ReplaceNativeVisual(
        GameObject scaffold,
        GameObject source,
        string? labelVariant,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        Vector3 localScale,
        Transform? visualParent = null,
        Transform? preservedRendererRoot = null)
    {
        foreach (MeshRenderer renderer in
                 scaffold.GetComponentsInChildren<MeshRenderer>(true))
        {
            renderer.enabled =
                IsWithin(renderer.transform, preservedRendererRoot);
        }
        foreach (SkinnedMeshRenderer renderer in
                 scaffold.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            renderer.enabled =
                IsWithin(renderer.transform, preservedRendererRoot);
        }

        GameObject visual = UnityEngine.Object.Instantiate(source);
        visual.name = $"{Sanitize(scaffold.name)}_Visual";
        visual.transform.SetParent(
            visualParent ?? scaffold.transform,
            false);
        visual.transform.localPosition = localPosition;
        visual.transform.localEulerAngles = localEulerAngles;
        visual.transform.localScale = localScale;
        ConfigureLabelVariant(visual, labelVariant);
        visual.SetActive(true);
        return visual;
    }

    private static void ConfigureStationPourableVisual(
        GameObject visual,
        S1StationFramework.PourableModule pourable,
        string itemId)
    {
        bool isMethylamine = string.Equals(
            itemId,
            MdmaPrecursorIds.Methylamine,
            StringComparison.OrdinalIgnoreCase);
        string prefix = isMethylamine
            ? "Methylamine"
            : "Safrole";
        Quaternion nativePourRotation =
            Quaternion.Inverse(pourable.Draggable.transform.rotation) *
            pourable.PourPoint.rotation;

        Transform cap = RequireDescendant(
            visual,
            $"{prefix}Cap");
        Transform volumeMarker = RequireDescendant(
            visual,
            $"{prefix}LiquidVolume");
        Transform pourPointMarker = RequireDescendant(
            visual,
            $"{prefix}PourPoint");

        MeshFilter? markerFilter =
            volumeMarker.GetComponent<MeshFilter>();
        Transform nativeLiquid =
            pourable.LiquidContainer.LiquidVolume.transform;
        MeshFilter? nativeFilter =
            nativeLiquid.GetComponent<MeshFilter>();
        MeshRenderer? nativeRenderer =
            nativeLiquid.GetComponent<MeshRenderer>();
        if (markerFilter == null ||
            markerFilter.sharedMesh == null ||
            nativeFilter == null ||
            nativeRenderer == null)
        {
            throw new InvalidOperationException(
                $"The {prefix} station visual cannot bind to the native liquid volume.");
        }

        nativeLiquid.SetParent(volumeMarker.parent, false);
        CopyLocalTransform(volumeMarker, nativeLiquid);
        nativeFilter.sharedMesh = markerFilter.sharedMesh;
        nativeLiquid.gameObject.SetActive(true);
        nativeRenderer.enabled = true;
        volumeMarker.gameObject.SetActive(false);

        pourable.PourPoint.SetParent(pourPointMarker.parent, false);
        CopyLocalTransform(pourPointMarker, pourable.PourPoint);
        if (!isMethylamine)
        {
            // The safrole marker locates the bottle mouth; the acid scaffold's
            // direction keeps pouring reachable from either station drag side.
            pourable.PourPoint.rotation =
                pourable.Draggable.transform.rotation *
                nativePourRotation;
        }

        foreach (ParticleSystem particles in pourable.PourParticles)
        {
            if (particles == null)
                continue;

            particles.transform.SetParent(
                pourable.PourPoint,
                true);
        }

        pourPointMarker.gameObject.SetActive(false);
        cap.gameObject.SetActive(false);

        if (!isMethylamine)
        {
            Transform neckRing = RequireDescendant(
                visual,
                "SafroleNeckRing");
            neckRing.gameObject.SetActive(false);

            Transform? decorativeLiquid =
                FindDescendant(visual, "SafroleLiquid");
            if (decorativeLiquid != null)
                decorativeLiquid.gameObject.SetActive(false);
        }
    }

    private static bool IsWithin(
        Transform transform,
        Transform? possibleParent) =>
        possibleParent != null &&
        (transform == possibleParent ||
         transform.IsChildOf(possibleParent));

    private static Transform RequireDescendant(
        GameObject root,
        string name) =>
        FindDescendant(root, name) ??
        throw new InvalidOperationException(
            $"The model '{root.name}' is missing station marker '{name}'.");

    private static Transform? FindDescendant(
        GameObject root,
        string name)
    {
        foreach (Transform transform in
                 root.GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(
                    transform.name,
                    name,
                    StringComparison.Ordinal))
            {
                return transform;
            }
        }

        return null;
    }

    private static void CopyLocalTransform(
        Transform source,
        Transform destination)
    {
        destination.localPosition = source.localPosition;
        destination.localRotation = source.localRotation;
        destination.localScale = source.localScale;
    }

    private static void ConfigureLabelVariant(
        GameObject root,
        string? activeVariant)
    {
        bool found = activeVariant == null;
        foreach (Transform transform in
                 root.GetComponentsInChildren<Transform>(true))
        {
            if (!transform.name.StartsWith(
                    "SafroleLabel_",
                    StringComparison.Ordinal))
            {
                continue;
            }

            bool active = string.Equals(
                transform.name,
                activeVariant,
                StringComparison.Ordinal);
            transform.gameObject.SetActive(active);
            found |= active;
        }

        if (!found)
        {
            throw new InvalidOperationException(
                $"The Safrole model is missing label variant '{activeVariant}'.");
        }
    }

    private static void PreparePrefab(GameObject root, string name)
    {
        root.name = name;
        RuntimePrefabCache.Store(root);
    }

    private static S1ItemFramework.StorableItemDefinition?
        GetNativeStorableDefinition(string itemId)
    {
#if IL2CPPMELON
        return S1.Registry.GetItem(itemId)
            ?.TryCast<S1ItemFramework.StorableItemDefinition>();
#else
        return S1.Registry.GetItem(itemId) as
            S1ItemFramework.StorableItemDefinition;
#endif
    }

    private static S1Equipping.Equippable_Viewmodel? AsViewmodel(
        S1Equipping.Equippable equippable)
    {
#if IL2CPPMELON
        return equippable.TryCast<S1Equipping.Equippable_Viewmodel>();
#else
        return equippable as S1Equipping.Equippable_Viewmodel;
#endif
    }

    private static string Sanitize(string value) =>
        value.Replace(':', '_').Replace('/', '_');

    private readonly struct HeldItemPose
    {
        internal HeldItemPose(
            Vector3 localPosition,
            Vector3 localEulerAngles,
            float uniformScale)
        {
            LocalPosition = localPosition;
            LocalEulerAngles = localEulerAngles;
            UniformScale = uniformScale;
        }

        internal Vector3 LocalPosition { get; }

        internal Vector3 LocalEulerAngles { get; }

        internal float UniformScale { get; }
    }

    private static IReadOnlyList<SafroleSpec> SafroleSpecs { get; } =
        new[]
        {
            new SafroleSpec(
                MdmaPrecursorIds.SafroleLow,
                "SafroleLabel_Low"),
            new SafroleSpec(
                MdmaPrecursorIds.SafroleStandard,
                "SafroleLabel_Standard"),
            new SafroleSpec(
                MdmaPrecursorIds.SafroleHigh,
                "SafroleLabel_High"),
        };

    private sealed class SafroleSpec
    {
        internal SafroleSpec(
            string id,
            string labelVariant)
        {
            Id = id;
            LabelVariant = labelVariant;
        }

        internal string Id { get; }

        internal string LabelVariant { get; }

    }
}
