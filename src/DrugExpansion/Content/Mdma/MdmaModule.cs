#if IL2CPPMELON
using Il2CppInterop.Runtime.InteropTypes;
using S1 = Il2CppScheduleOne;
using S1AvatarEquipping = Il2CppScheduleOne.AvatarFramework.Equipping;
using S1Equipping = Il2CppScheduleOne.Equipping;
using S1ItemFramework = Il2CppScheduleOne.ItemFramework;
using S1Product = Il2CppScheduleOne.Product;
using S1StationFramework = Il2CppScheduleOne.StationFramework;
using S1Storage = Il2CppScheduleOne.Storage;
#elif MONOMELON
using S1 = ScheduleOne;
using S1AvatarEquipping = ScheduleOne.AvatarFramework.Equipping;
using S1Equipping = ScheduleOne.Equipping;
using S1ItemFramework = ScheduleOne.ItemFramework;
using S1Product = ScheduleOne.Product;
using S1StationFramework = ScheduleOne.StationFramework;
using S1Storage = ScheduleOne.Storage;
#endif

using MelonLoader;
using DrugExpansion.Content.Mdma.Batch;
using DrugExpansion.Content.Mdma.Precursors;
using DrugExpansion.Content.Mdma.Progression;
using DrugExpansion.Content.Mdma.Production;
using DrugExpansion.Infrastructure;
using S1API.Console;
using S1API.Items;
using S1API.Items.Buildable;
using S1API.Leveling;
using S1API.Products;
using S1API.Properties;
using S1API.Rendering;
using S1API.Shops;
using S1API.Stations;
using S1API.Utils;
using UnityEngine;

namespace DrugExpansion.Content.Mdma;

internal sealed class MdmaModule : IDrugContentModule, IMixingCapability
{
    internal const string ProductKindId = "ifbars.moredrugs:mdma";
    internal const string ProductId = MdmaProductIds.Tablets;
    internal const string RecipeId = "ifbars.moredrugs:mdma-synthesis";
    internal const string ProviderData = "mdma";
    internal const string TabletPressItemId =
        "ifbars.moredrugs:stations/manual-tablet-press";
    private const string BrickPackagingId = "brick";

    private static readonly FullRank ContentUnlockRank =
        new(Rank.Baron, 1);

    private const string HeartPillResource =
        "DrugExpansion.Assets.Models.heartpill.glb";
    private const string MdmaCrystalsResource =
        "DrugExpansion.Assets.Models.mdma_crystals.glb";
    private const string TabletPressIconResource =
        "DrugExpansion.Assets.Icons.manual_tablet_press.png";
    private const string CrystalAvatarPath =
        "DrugExpansion/Items/MDMACrystals/Held";

    private readonly MelonLogger.Instance _logger;
    private readonly EmbeddedGlbAsset _heartPill =
        new EmbeddedGlbAsset(HeartPillResource, "DrugExpansion_HeartPill");
    private readonly EmbeddedGlbAsset _mdmaCrystals =
        new EmbeddedGlbAsset(MdmaCrystalsResource, "DrugExpansion_MdmaCrystals");
    private readonly ManualTabletPressAsset _tabletPressAsset = new();
    private readonly MdmaPrecursorCatalog _precursors;

    private ProductKind? _productKind;
    private ProductPresentationProfile? _presentationProfile;
    private ProductPackagingContentProfile? _baggieProfile;
    private ProductPackagingContentProfile? _jarProfile;
    private ProductPackagingContentProfile? _brickProfile;
    private CustomProductDefinition? _definition;
    private S1API.Items.Quality.QualityItemDefinition? _crystalDefinition;
    private S1API.Items.Buildable.BuildableItemDefinition? _tabletPressDefinition;
    private ChemistryStationRecipe? _recipe;
    private ProductKindMetadata? _metadata;
    private ProductMixingProfile? _mixingProfile;
    private GameObject? _consumptionSource;
    private Sprite? _tabletPressIcon;
    private bool _tabletPressAddedToShop;
    private bool _tabletPressIconGenerated;
    private bool _crystalIconGenerated;
    private bool _crystalPresentationConfigured;
    private bool _progressionSubscribed;
    private bool _daveyAvailabilitySubscribed;
    private MdmaProgressionData? _lastAppliedProgression;
    private bool _discoveryAppliedForCurrentProgression;
    private bool _legacyWarningLoggedForCurrentProgression;

    internal MdmaModule(MelonLogger.Instance logger)
    {
        _logger = logger;
        _precursors = new MdmaPrecursorCatalog(logger);
    }

    public string ProviderDataKey => ProviderData;

    public void RegisterContent()
    {
        _precursors.RegisterContent();
        EnsureProgressionSubscription();
        EnsureDaveyAvailabilitySubscription();

        if (_definition != null &&
            _crystalDefinition != null &&
            _tabletPressDefinition != null)
            return;

        ProductDefinition template =
            ItemManager.GetDefinition("cocaine") as ProductDefinition ??
            throw new InvalidOperationException(
                "The native 'cocaine' product scaffold is unavailable.");
        PackagingDefinition baggie =
            ItemManager.GetDefinition("baggie") as PackagingDefinition ??
            throw new InvalidOperationException(
                "The native 'baggie' packaging definition is unavailable.");
        PackagingDefinition jar =
            ItemManager.GetDefinition("jar") as PackagingDefinition ??
            throw new InvalidOperationException(
                "The native 'jar' packaging definition is unavailable.");
        PackagingDefinition brick =
            ItemManager.GetDefinition(BrickPackagingId) as PackagingDefinition ??
            throw new InvalidOperationException(
                "The native 'brick' packaging definition is unavailable.");

        _productKind ??=
            new ProductKindBuilder(ProductKindId)
                // Native product saves currently require a compatibility value. This is
                // metadata, not MDMA's logical identity or its mixing capability.
                .WithCompatibilityDrugType(DrugType.MDMA)
                .Build();

        GameObject pillSource = _heartPill.GetOrLoad();
        GameObject crystalSource = _mdmaCrystals.GetOrLoad();
        EnsurePresentationRegistered(template, pillSource);
        EnsurePackagingRegistered(pillSource);
        RegisterMixing(_productKind);

        _definition = CreateBuilder(template, baggie, jar, brick).Build();
        _crystalDefinition = CreateCrystalBuilder().Build();
        _tabletPressIcon ??=
            ImageUtils.LoadImageFromResource(
                typeof(MdmaModule).Assembly,
                TabletPressIconResource);
        S1API.Items.Buildable.BuildableItemDefinitionBuilder tabletPressBuilder =
            S1API.Items.Buildable.BuildableItemCreator.CloneFrom("brickpress")
                .WithBasicInfo(
                    TabletPressItemId,
                    "Manual Tablet Press",
                    "A hand-operated press for converting MDMA crystals into tablets.",
                    ItemCategory.Equipment)
                .WithBuildSound(S1API.Items.Buildable.BuildSoundType.Metal)
                .WithPricing(MdmaEconomyPolicy.TabletPressPrice, 0.5f)
                .WithRequiredRank(ContentUnlockRank);
        if (_tabletPressIcon != null)
        {
            tabletPressBuilder.WithIcon(_tabletPressIcon);
            _tabletPressIconGenerated = true;
        }
        _tabletPressDefinition = tabletPressBuilder.Build();

        ConsoleItemAliases.Register("mdma", MdmaProductIds.Tablets);
        ConsoleItemAliases.Register("mdmacrystals", MdmaProductIds.Crystals);
        ConsoleItemAliases.Register("tabletpress", TabletPressItemId);
        ManualTabletPressRuntime.Configure(
            _tabletPressAsset,
            () => _heartPill.GetOrLoad(),
            () => _mdmaCrystals.GetOrLoad(),
            HandleSuccessfulTabletPress,
            _logger);

        _recipe ??=
            ChemistryStationRecipes.CreateAndRegister(builder => builder
                .WithRecipeId(RecipeId)
                // StationRecipeEntry appends the yield without a separator. The word
                // joiner preserves the preceding display space through S1API's trim.
                .WithTitle("MDMA Crystals \u2060")
                .WithInitialAvailability(
                    isDiscovered: false,
                    isUnlocked: false)
                .WithCookTimeMinutes(240)
                .WithTemperature(220f, 20f)
                .WithFinalLiquidColor(
                    new Color(0.88f, 0.85f, 0.67f, 0.78f))
                .WithIngredientOptions(
                    MdmaPrecursorIds.SafroleOptions,
                    1)
                .WithIngredient(MdmaPrecursorIds.Methylamine, 1)
                .WithIngredient("acid", 1)
                .WithProduct(
                    MdmaProductIds.Crystals,
                    ManualTabletPressQuantities.ChemistryCrystalYield));
        TryConfigureCrystalPresentation(crystalSource);

        _logger.Msg(
            $"Registered MDMA crystal intermediate '{MdmaProductIds.Crystals}', " +
            $"product '{MdmaProductIds.Tablets}', tablet press '{TabletPressItemId}', " +
            $"and chemistry recipe '{RecipeId}'.");
    }

    public void CompleteLoad()
    {
        _precursors.CompleteLoad();

        if (_definition == null ||
            _crystalDefinition == null ||
            _tabletPressDefinition == null ||
            _productKind == null)
            return;

        MdmaProgressionSave.ReplayLoadedState(
            HandleProgressionLoaded);
        if (!DiscoDavey.EnsureAvailabilitySubscription())
        {
            _logger.Warning(
                "Disco Davey's runtime relationship was unavailable at load completion.");
        }
        ApplyRecipeAvailability(DiscoDavey.IsAvailable);
        TryConfigureCrystalPresentation(_mdmaCrystals.GetOrLoad());
        TryGenerateCrystalIcon();
        TryGenerateTabletPressIcon();

        if (_metadata == null)
        {
            Sprite icon = _definition.Icon;
            if (icon == null)
            {
                _logger.Warning(
                    "MDMA icon generation has not completed; Product Manager metadata will retry on the next load.");
            }
            else
            {
                _metadata =
                    new ProductKindMetadataBuilder(_productKind)
                        .WithDisplayName("MDMA")
                        .WithColor(new Color(0.95f, 0.3f, 0.65f))
                        .WithIcon(icon)
                        .WithSortOrder(100)
                        .WithSearchAliases("ecstasy", "molly", "pill")
                        .WithProductManagerVisibility()
                        .Build();
            }
        }

        // Registration and Product Manager presentation metadata must not
        // implicitly unlock or list MDMA. Progression content owns discovery.

        if (!_tabletPressAddedToShop)
        {
            int shopCount = ShopManager.AddToShops(
                _tabletPressDefinition,
                MdmaEconomyPolicy.TabletPressPrice,
                "Handy Hank's Hardware",
                "Dan's Hardware");
            _tabletPressAddedToShop = shopCount > 0;
            if (!_tabletPressAddedToShop)
            {
                _logger.Warning(
                    "The Manual Tablet Press could not be added to a hardware store yet; registration will retry after the next load.");
            }
        }
    }

    public CustomProductDefinitionBuilder? Restore(
        CustomProductSaveDescriptor descriptor)
    {
        bool isTablets = string.Equals(
            descriptor.ProductId,
            MdmaProductIds.Tablets,
            StringComparison.OrdinalIgnoreCase);
        if (!isTablets)
        {
            return null;
        }

        ProductDefinition template =
            ItemManager.GetDefinition("cocaine") as ProductDefinition ??
            throw new InvalidOperationException(
                "Cannot restore MDMA without the native cocaine product scaffold.");
        PackagingDefinition baggie =
            ItemManager.GetDefinition("baggie") as PackagingDefinition ??
            throw new InvalidOperationException(
                "Cannot restore MDMA without native baggie packaging.");
        PackagingDefinition jar =
            ItemManager.GetDefinition("jar") as PackagingDefinition ??
            throw new InvalidOperationException(
                "Cannot restore MDMA without native jar packaging.");
        PackagingDefinition brick =
            ItemManager.GetDefinition(BrickPackagingId) as PackagingDefinition ??
            throw new InvalidOperationException(
                "Cannot restore MDMA without native brick packaging.");

        _productKind ??=
            new ProductKindBuilder(ProductKindId)
                .WithCompatibilityDrugType(DrugType.MDMA)
                .Build();

        GameObject pillSource = _heartPill.GetOrLoad();
        EnsurePresentationRegistered(template, pillSource);
        EnsurePackagingRegistered(pillSource);
        return CreateBuilder(template, baggie, jar, brick);
    }

    public void Dispose()
    {
        if (_progressionSubscribed)
        {
            MdmaProgressionSave.Loaded -= HandleProgressionLoaded;
            _progressionSubscribed = false;
        }
        if (_daveyAvailabilitySubscribed)
        {
            DiscoDavey.AvailabilityChanged -= ApplyRecipeAvailability;
            _daveyAvailabilitySubscribed = false;
        }
        if (_consumptionSource != null)
            UnityEngine.Object.Destroy(_consumptionSource);

        _consumptionSource = null;
        ManualTabletPressRuntime.Reset();
        _tabletPressAsset.Dispose();
        _precursors.Dispose();
        _mdmaCrystals.Dispose();
        _heartPill.Dispose();
    }

    public void RegisterMixing(ProductKind productKind)
    {
        _mixingProfile ??=
            new ProductMixingProfileBuilder(productKind)
                .WithMixerMap(ProductMixingMap.Cocaine)
                .WithPropertyColorMixing()
                .WithOutputFactoryCompatibility(
                    "ifbars.moredrugs:mixing/mdma",
                    version: 3)
                .WithOutputFactory(input =>
                    new ProductMixingOutputDefinition(
                        input.MixName,
                        input.SourceKind,
                        MdmaMixingPricing.CalculatePrice(
                            MdmaMixingPriceCapture.ConsumeLastResolvedEffectIds())))
                .Build();
    }

    private void EnsureProgressionSubscription()
    {
        if (_progressionSubscribed)
            return;

        MdmaProgressionSave.Loaded += HandleProgressionLoaded;
        _progressionSubscribed = true;
    }

    private void EnsureDaveyAvailabilitySubscription()
    {
        if (_daveyAvailabilitySubscribed)
            return;

        DiscoDavey.AvailabilityChanged += ApplyRecipeAvailability;
        _daveyAvailabilitySubscribed = true;
    }

    private void ApplyRecipeAvailability(bool available)
    {
        if (_recipe == null)
            return;

        _recipe.SetAvailability(
            isDiscovered: available,
            isUnlocked: available);
        _logger.Msg(
            $"MDMA chemistry recipe availability set to {available} " +
            $"from Disco Davey's unlock state.");
    }

    private void HandleSuccessfulTabletPress()
    {
        if (!MdmaProgressionSave.MarkFirstTabletPressed())
            return;

        if (_definition == null)
        {
            _logger.Warning(
                "The first MDMA tablet was pressed before its product definition was ready; discovery will retry from save data.");
            return;
        }

        try
        {
            _definition.Discover(listForSale: false);
            _lastAppliedProgression = MdmaProgressionSave.Current;
            _discoveryAppliedForCurrentProgression = true;
            _logger.Msg(
                "MDMA was discovered after the first successful tablet press; listing remains a player choice.");
        }
        catch (Exception exception)
        {
            _logger.Warning(
                $"MDMA discovery will retry from saved progression: {exception.Message}");
        }
    }

    private void HandleProgressionLoaded(MdmaProgressionData progression)
    {
        if (_definition == null)
            return;

        if (!ReferenceEquals(_lastAppliedProgression, progression))
        {
            _lastAppliedProgression = progression;
            _discoveryAppliedForCurrentProgression = false;
            _legacyWarningLoggedForCurrentProgression = false;
        }

        if (MdmaProgressionPolicy.ShouldDiscover(
                progression.HasPressedFirstTablet))
        {
            if (_discoveryAppliedForCurrentProgression)
                return;

            try
            {
                _definition.Discover(listForSale: false);
                _discoveryAppliedForCurrentProgression = true;
            }
            catch (Exception exception)
            {
                _logger.Warning(
                    $"Saved MDMA discovery could not be restored yet: {exception.Message}");
            }
        }
        else if (progression.LegacyDiscoveryStatePreserved &&
                 !_legacyWarningLoggedForCurrentProgression)
        {
            _logger.Warning(
                "This save predates MDMA progression tracking and already knew or listed MDMA. Its ambiguous legacy state was preserved instead of being destructively rewritten.");
            _legacyWarningLoggedForCurrentProgression = true;
        }
    }

    private CustomProductDefinitionBuilder CreateBuilder(
        ProductDefinition template,
        PackagingDefinition baggie,
        PackagingDefinition jar,
        PackagingDefinition brick)
    {
        ProductKind kind = _productKind ??
            throw new InvalidOperationException("MDMA product kind is not registered.");

        return CustomProductItemCreator
            .CreateBuilder(ProductId, kind)
            .WithName("MDMA")
            .WithDescription("Heart-shaped MDMA tablets.")
            .WithProductPrice(MdmaEconomyPolicy.ProductPrice)
            .WithProperties(Property.Energizing, Property.Focused)
            .WithLegalStatus(LegalStatus.Illegal)
            .WithBaseAddictiveness(0.35f)
            .WithDefaultQuality(Quality.Premium)
            .WithRepresentationsFrom(template)
            .WithValidPackaging(baggie, jar, brick)
            .WithEffectDurations(playerSeconds: 180, npcSeconds: 240)
            .WithNativeMixerMap(ProductMixingMap.Cocaine)
            .WithSaveProvider(
                DrugCatalog.SaveProviderId,
                DrugCatalog.SaveProviderVersion,
                ProviderData);
    }

    private static S1API.Items.Quality.QualityItemDefinitionBuilder
        CreateCrystalBuilder()
    {
        return S1API.Items.Quality.QualityItemCreator
            .CloneFrom("cocainebase")
            .WithBasicInfo(
                MdmaProductIds.Crystals,
                "MDMA Crystals",
                "An unpressed MDMA batch ready for the Manual Tablet Press.",
                ItemCategory.Ingredient)
            .WithStackLimit(20)
            .WithPricing(basePurchasePrice: 0f, resellMultiplier: 0f)
            .WithLegalStatus(LegalStatus.Illegal)
            .WithDefaultQuality(Quality.Premium);
    }

    private void EnsurePresentationRegistered(
        ProductDefinition template,
        GameObject pillSource)
    {
        Quaternion pillRotation =
            Quaternion.Euler(78f, 0f, -8f) *
            Quaternion.Euler(0f, 90f, 0f);
        ProductPresentationTransform pillPose =
            new ProductPresentationTransform(
                Vector3.zero,
                pillRotation.eulerAngles,
                Vector3.one * 0.06f);
        Quaternion generatedIconCameraRotation =
            Quaternion.Euler(0f, -135f, 0f);
        Quaternion mixResultCameraRotation =
            Quaternion.Euler(-135.71956f, 0f, -180f);
        // Preserve the authored icon-facing orientation in the native mix-result camera.
        Quaternion functionalPillRotation =
            mixResultCameraRotation *
            Quaternion.Inverse(generatedIconCameraRotation) *
            pillRotation;
        ProductPresentationTransform functionalPillPose =
            new ProductPresentationTransform(
                pillPose.LocalPosition,
                functionalPillRotation.eulerAngles,
                pillPose.LocalScale);
        ProductPresentationTransform heldPillPose =
            new ProductPresentationTransform(
                pillPose.LocalPosition,
                (
                    Quaternion.Euler(-90f, 0f, 0f) *
                    Quaternion.Euler(0f, -90f, 0f)
                ).eulerAngles,
                pillPose.LocalScale);

        _consumptionSource ??= CreateConsumptionSource(pillSource, pillPose);
        _presentationProfile ??=
            new ProductPresentationProfileBuilder()
                .WithLooseVisual(() => pillSource, pillPose)
                .WithHeldVisual(() => pillSource, heldPillPose)
                .WithFunctionalProductVisual(
                    () => pillSource,
                    functionalPillPose)
                .WithFunctionalProductConvexMeshColliders()
                .WithGeneratedIconFromLooseVisual(
                    size: 512,
                    fitToCamera: true,
                    cameraFill: 0.78f)
                .WithConsumptionPrefab(() => _consumptionSource)
                .Require(
                    ProductPresentationContext.Loose,
                    ProductPresentationContext.Stored,
                    ProductPresentationContext.Held,
                    ProductPresentationContext.Station,
                    ProductPresentationContext.FunctionalProduct,
                    ProductPresentationContext.Icon,
                    ProductPresentationContext.Consumption)
                .Build();

        ProductPresentationProfileRegistry.RegisterForProduct(
            ModInfo.OwnerId,
            MdmaProductIds.Tablets,
            _presentationProfile);
        ProductPresentationProfileRegistry.RegisterForProductKind(
            ModInfo.OwnerId,
            _productKind ??
                throw new InvalidOperationException(
                    "MDMA product kind is not registered."),
            _presentationProfile);
    }

    private void EnsurePackagingRegistered(GameObject pillSource)
    {
        _baggieProfile ??=
            new ProductPackagingContentProfileBuilder()
                .WithContent(() => pillSource)
                .AddPlacement(
                    new ProductPresentationTransform(
                        new Vector3(0f, -0.002f, 0f),
                        new Vector3(78f, -70f, -8f),
                        Vector3.one * 0.05f))
                .Build();
        _jarProfile ??=
            new ProductPackagingContentProfileBuilder()
                .WithContent(() => pillSource)
                .AddPlacements(
                    JarPlacement(-0.04f, 0.02f, 12f),
                    JarPlacement(0f, 0.02f, -28f),
                    JarPlacement(0.04f, 0.02f, 38f),
                    JarPlacement(-0.035f, 0.06f, -18f),
                    JarPlacement(0.035f, 0.06f, 22f),
                    JarPlacement(-0.04f, 0.095f, 48f),
                    JarPlacement(0f, 0.095f, -42f),
                    JarPlacement(0.04f, 0.095f, 8f),
                    JarPlacement(-0.02f, 0.13f, 65f),
                    JarPlacement(0.025f, 0.13f, -62f))
                .Build();
        Material brickMaterial = GetHeartPillMaterial(pillSource);
        _brickProfile ??=
            new ProductPackagingContentProfileBuilder()
                .WithNativeFilledVisualScaffold(
                    ProductPackagingVisualTemplate.Cocaine,
                    clone => ApplyMdmaBrickMaterial(clone, brickMaterial))
                .Build();

        ProductPackagingContentProfileRegistry.Register(
            ModInfo.OwnerId,
            ProductId,
            "baggie",
            _baggieProfile);
        ProductPackagingContentProfileRegistry.Register(
            ModInfo.OwnerId,
            ProductId,
            "jar",
            _jarProfile);
        ProductPackagingContentProfileRegistry.Register(
            ModInfo.OwnerId,
            ProductId,
            BrickPackagingId,
            _brickProfile);
        ProductPackagingContentProfileRegistry.RegisterForProductKind(
            ModInfo.OwnerId,
            ProductKindId,
            "baggie",
            _baggieProfile);
        ProductPackagingContentProfileRegistry.RegisterForProductKind(
            ModInfo.OwnerId,
            ProductKindId,
            "jar",
            _jarProfile);
        ProductPackagingContentProfileRegistry.RegisterForProductKind(
            ModInfo.OwnerId,
            ProductKindId,
            BrickPackagingId,
            _brickProfile);
    }

    private static Material GetHeartPillMaterial(GameObject pillSource)
    {
        foreach (Renderer renderer in
                 pillSource.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material != null)
                    return material;
            }
        }

        throw new InvalidOperationException(
            "The MDMA heart-pill asset does not provide a material for brick visuals.");
    }

    private static void ApplyMdmaBrickMaterial(
        GameObject scaffold,
        Material material)
    {
        bool customized = false;
        foreach (Renderer renderer in
                 scaffold.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.name.StartsWith(
                    "Brick_LOD",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
                materials[i] = material;
            renderer.sharedMaterials = materials;
            customized = true;
        }

        if (!customized)
        {
            throw new InvalidOperationException(
                "The native cocaine brick scaffold did not contain Brick_LOD renderers.");
        }
    }

    private void TryConfigureCrystalPresentation(GameObject crystalSource)
    {
        if (_crystalPresentationConfigured)
            return;

        try
        {
            S1ItemFramework.QualityItemDefinition definition =
                GetNativeQualityDefinition(MdmaProductIds.Crystals) ??
                throw new InvalidOperationException(
                    "The registered MDMA crystal definition is unavailable.");
            S1ItemFramework.QualityItemDefinition template =
                GetNativeQualityDefinition("cocainebase") ??
                throw new InvalidOperationException(
                    "The native cocaine-base presentation scaffold is unavailable.");

            S1Storage.StoredItem stored =
                CloneCrystalStoredItem(template, crystalSource);
            S1Equipping.Equippable equippable =
                CloneCrystalEquippable(template, crystalSource);
            S1StationFramework.StationItem? station =
                template.StationItem == null
                    ? null
                    : CloneCrystalStationItem(
                        template.StationItem,
                        crystalSource);

            definition.StoredItem = stored;
            definition.Equippable = equippable;
            definition.StationItem = station;
            _crystalPresentationConfigured = true;
        }
        catch (Exception exception)
        {
            _logger.Warning(
                "MDMA crystal presentation could not be applied yet; " +
                $"native quality-item visuals will remain as a fallback: {exception.Message}");
        }
    }

    private static ProductPresentationTransform JarPlacement(
        float x,
        float y,
        float zRotation)
    {
        return new ProductPresentationTransform(
            new Vector3(x, y, 0f),
            new Vector3(78f, 0f, zRotation),
            Vector3.one * 0.04f);
    }

    private static S1Storage.StoredItem CloneCrystalStoredItem(
        S1ItemFramework.QualityItemDefinition template,
        GameObject crystalSource)
    {
        S1Storage.StoredItem stored =
            UnityEngine.Object.Instantiate(template.StoredItem);
        PrepareCrystalPrefab(stored.gameObject, "DrugExpansion_MDMA_Crystals_Stored");
        ReplaceCrystalVisual(
            stored.gameObject,
            crystalSource,
            new Vector3(0f, 0.018f, 0f),
            new Vector3(0f, 25f, 0f),
            Vector3.one * 0.62f);
        return stored;
    }

    private static S1StationFramework.StationItem CloneCrystalStationItem(
        S1StationFramework.StationItem template,
        GameObject crystalSource)
    {
        S1StationFramework.StationItem station =
            UnityEngine.Object.Instantiate(template);
        S1StationFramework.CookableModule? cookable =
            station.GetModule<S1StationFramework.CookableModule>();
        if (cookable != null)
            station.Modules.Remove(cookable);

        PrepareCrystalPrefab(
            station.gameObject,
            "DrugExpansion_MDMA_Crystals_Station");
        ReplaceCrystalVisual(
            station.gameObject,
            crystalSource,
            Vector3.zero,
            new Vector3(0f, 20f, 0f),
            Vector3.one * 0.62f);
        return station;
    }

    private static S1Equipping.Equippable CloneCrystalEquippable(
        S1ItemFramework.QualityItemDefinition template,
        GameObject crystalSource)
    {
        S1Equipping.Equippable equippable =
            UnityEngine.Object.Instantiate(template.Equippable);
        PrepareCrystalPrefab(
            equippable.gameObject,
            "DrugExpansion_MDMA_Crystals_Equippable");
        ReplaceCrystalVisual(
            equippable.gameObject,
            crystalSource,
            new Vector3(0f, 0f, 0.01f),
            new Vector3(-72f, 80f, 12f),
            Vector3.one * 0.75f);

        S1Equipping.Equippable_Viewmodel? viewmodel =
            AsViewmodel(equippable);
        if (viewmodel != null)
        {
            viewmodel.AvatarEquippable =
                CreateCrystalAvatarEquippable(
                    viewmodel.AvatarEquippable,
                    crystalSource);
        }

        return equippable;
    }

    private static S1AvatarEquipping.AvatarEquippable
        CreateCrystalAvatarEquippable(
            S1AvatarEquipping.AvatarEquippable? template,
            GameObject crystalSource)
    {
        var root =
            new GameObject("DrugExpansion_MDMA_Crystals_AvatarEquippable");
        PrepareCrystalPrefab(root, root.name);
        ReplaceCrystalVisual(
            root,
            crystalSource,
            Vector3.zero,
            new Vector3(-72f, 80f, 12f),
            Vector3.one * 0.75f);

        S1AvatarEquipping.AvatarEquippable avatar =
            root.AddComponent<S1AvatarEquipping.AvatarEquippable>();
        avatar.AlignmentPoint = root.transform;
        avatar.AssetPath = CrystalAvatarPath;
        if (template != null)
        {
            avatar.Suspiciousness = template.Suspiciousness;
            avatar.Hand = template.Hand;
            avatar.TriggerType = template.TriggerType;
            avatar.AnimationTrigger = template.AnimationTrigger;
        }

        if (!AvatarEquippableRegistry.RegisterAvatarEquippable(
                CrystalAvatarPath,
                root))
        {
            throw new InvalidOperationException(
                $"Could not register the crystal avatar presentation at '{CrystalAvatarPath}'.");
        }

        return avatar;
    }

    private static void PrepareCrystalPrefab(GameObject root, string name)
    {
        root.name = name;
        RuntimePrefabCache.Store(root);
    }

    private static void ReplaceCrystalVisual(
        GameObject scaffold,
        GameObject crystalSource,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        Vector3 localScale)
    {
        foreach (Renderer renderer in
                 scaffold.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = false;
        }

        foreach (Collider collider in
                 scaffold.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }

        GameObject visual = UnityEngine.Object.Instantiate(crystalSource);
        visual.name = "DrugExpansion_MDMA_Crystals_Visual";
        visual.transform.SetParent(scaffold.transform, false);
        visual.transform.localPosition = localPosition;
        visual.transform.localEulerAngles = localEulerAngles;
        visual.transform.localScale = localScale;
        visual.SetActive(true);
    }

    private void TryGenerateCrystalIcon()
    {
        if (_crystalIconGenerated || _crystalDefinition == null)
            return;

        GameObject? iconRoot = null;
        try
        {
            iconRoot = new GameObject("DrugExpansion_MDMA_Crystals_IconSource");
            GameObject visual =
                UnityEngine.Object.Instantiate(_mdmaCrystals.GetOrLoad());
            visual.transform.SetParent(iconRoot.transform, false);
            visual.transform.localEulerAngles = new Vector3(45f, 0f, 0f);
            visual.transform.localScale = Vector3.one * 1.15f;
            visual.SetActive(true);

            Sprite? icon = IconFactory.GenerateIconSprite(
                iconRoot.transform,
                size: 512,
                bakeSkinnedMeshes: true,
                fitToCamera: true,
                cameraFill: 1.04f);
            if (icon == null)
                return;

            _crystalDefinition.Icon = icon;
            _crystalIconGenerated = true;
        }
        catch (Exception exception)
        {
            _logger.Warning(
                $"MDMA crystal icon generation will retry after the next load: {exception.Message}");
        }
        finally
        {
            if (iconRoot != null)
                UnityEngine.Object.Destroy(iconRoot);
        }
    }

    private void TryGenerateTabletPressIcon()
    {
        if (_tabletPressIconGenerated || _tabletPressDefinition == null)
            return;

        GameObject? iconSource = null;
        try
        {
            iconSource = new GameObject("DrugExpansion_TabletPressIconSource");
            ManualTabletPressRig rig =
                _tabletPressAsset.CreateInstance(iconSource.transform);
            rig.FreshTabletAssembly.gameObject.SetActive(false);
            foreach (Transform child in
                     rig.Root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.StartsWith(
                        "FinishedTablet_",
                        StringComparison.Ordinal))
                {
                    child.gameObject.SetActive(false);
                }
            }

            Sprite? icon = IconFactory.GenerateIconSprite(
                rig.Root.transform,
                size: 512,
                bakeSkinnedMeshes: true,
                fitToCamera: true,
                cameraFill: 0.72f);
            if (icon == null)
            {
                _logger.Warning(
                    "Manual Tablet Press icon generation is not ready; the inherited Brick Press icon will be used until the next load.");
                return;
            }

            _tabletPressDefinition.Icon = icon;
            ManualTabletPressRuntime.RefreshManagementIcon(icon);
            _tabletPressIconGenerated = true;
        }
        catch (Exception exception)
        {
            _logger.Warning(
                $"Manual Tablet Press icon generation failed; the inherited Brick Press icon will be used: {exception.Message}");
        }
        finally
        {
            if (iconSource != null)
                UnityEngine.Object.Destroy(iconSource);
        }
    }

    private static GameObject CreateConsumptionSource(
        GameObject pillSource,
        ProductPresentationTransform pillPose)
    {
        S1Product.ProductDefinition nativeTemplate =
            GetNativeProductDefinition("cocaine") ??
            throw new InvalidOperationException(
                "Cannot create the MDMA consumption prefab without the native cocaine scaffold.");
        GameObject consumptionSource =
            UnityEngine.Object.Instantiate(
                nativeTemplate.ConsumeAnimation.gameObject);
        consumptionSource.name = "DrugExpansion_MDMA_Consumption";
        UnityEngine.Object.DontDestroyOnLoad(consumptionSource);
        consumptionSource.transform.position = new Vector3(0f, -20000f, 0f);

        GameObject consumePill = UnityEngine.Object.Instantiate(pillSource);
        consumePill.name = "DrugExpansion_MDMA_Consumption_Visual";
        consumePill.transform.SetParent(consumptionSource.transform, false);
        consumePill.transform.localPosition = pillPose.LocalPosition;
        consumePill.transform.localEulerAngles = pillPose.LocalEulerAngles;
        consumePill.transform.localScale = pillPose.LocalScale;
        consumePill.SetActive(true);
        consumptionSource.SetActive(true);
        return consumptionSource;
    }

    private static S1Product.ProductDefinition? GetNativeProductDefinition(
        string itemId)
    {
#if IL2CPPMELON
        return S1.Registry.GetItem(itemId)?.TryCast<S1Product.ProductDefinition>();
#else
        return S1.Registry.GetItem(itemId) as S1Product.ProductDefinition;
#endif
    }

    private static S1ItemFramework.QualityItemDefinition?
        GetNativeQualityDefinition(string itemId)
    {
#if IL2CPPMELON
        return S1.Registry.GetItem(itemId)
            ?.TryCast<S1ItemFramework.QualityItemDefinition>();
#else
        return S1.Registry.GetItem(itemId) as
            S1ItemFramework.QualityItemDefinition;
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
}
