#if IL2CPPMELON
using S1 = Il2CppScheduleOne;
using S1Building = Il2CppScheduleOne.Building;
using S1EntityFramework = Il2CppScheduleOne.EntityFramework;
using S1FunctionalProductList =
    Il2CppSystem.Collections.Generic.List<
        Il2CppScheduleOne.Product.FunctionalProduct>;
using S1ItemFramework = Il2CppScheduleOne.ItemFramework;
using S1Management = Il2CppScheduleOne.Management;
using S1ObjectScripts = Il2CppScheduleOne.ObjectScripts;
using S1PlayerTasks = Il2CppScheduleOne.PlayerTasks;
using S1Product = Il2CppScheduleOne.Product;
using S1Tiles = Il2CppScheduleOne.Tiles;
using S1UIManagement = Il2CppScheduleOne.UI.Management;
using S1UIStations = Il2CppScheduleOne.UI.Stations;
using TmpText = Il2CppTMPro.TextMeshProUGUI;
#elif MONOMELON
using S1 = ScheduleOne;
using S1Building = ScheduleOne.Building;
using S1EntityFramework = ScheduleOne.EntityFramework;
using S1FunctionalProductList =
    System.Collections.Generic.List<ScheduleOne.Product.FunctionalProduct>;
using S1ItemFramework = ScheduleOne.ItemFramework;
using S1Management = ScheduleOne.Management;
using S1ObjectScripts = ScheduleOne.ObjectScripts;
using S1PlayerTasks = ScheduleOne.PlayerTasks;
using S1Product = ScheduleOne.Product;
using S1Tiles = ScheduleOne.Tiles;
using S1UIManagement = ScheduleOne.UI.Management;
using S1UIStations = ScheduleOne.UI.Stations;
using TmpText = TMPro.TextMeshProUGUI;
#endif

using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MelonLoader;
using DrugExpansion.Content.Mdma.Batch;
using UnityEngine;

namespace DrugExpansion.Content.Mdma.Production;

/// <summary>
/// Adapts the native Brick Press lifecycle for the DrugExpansion tablet press.
/// Inventory and slot replication remain native and authoritative; the authored
/// model and ejected rigidbodies are local presentation.
/// </summary>
internal static class ManualTabletPressRuntime
{
    private static readonly Dictionary<
        int,
        ManualTabletPressInstance> Instances = new();

    private static readonly ConditionalWeakTable<
        S1ObjectScripts.BrickPress,
        PreparedNativeLoad> PreparedNativeLoads = new();

    private static readonly Dictionary<
        int,
        CircularHandleInteraction> CircularHandleInteractions = new();

    private static readonly ConditionalWeakTable<
        S1PlayerTasks.UseBrickPress,
        S1ObjectScripts.BrickPress> ActiveTabletPressTasks = new();

    private static readonly Dictionary<
        int,
        ManualTabletPressCompletionGate> CompletionGates = new();

    private static readonly MethodInfo? BeginNativePressMethod =
        AccessTools.Method(
            typeof(S1PlayerTasks.UseBrickPress),
            "BeginPress");

    private static readonly MethodInfo? SetManagementPromptMethod =
        AccessTools.Method(
            typeof(S1UIManagement.ManagementWorldspaceCanvas),
            "SetCrosshairPromptMessage");

#if MONOMELON
    private static readonly FieldInfo? UseBrickPressPressField =
        AccessTools.Field(
            typeof(S1PlayerTasks.UseBrickPress),
            "press");
#endif

    private static readonly MethodInfo? MoveNativeHandleMethod =
        AccessTools.Method(
            typeof(S1ObjectScripts.BrickPressHandle),
            "Move");

    private static readonly PropertyInfo? CurrentInstructionProperty =
        AccessTools.Property(
            typeof(S1PlayerTasks.Task),
            nameof(S1PlayerTasks.Task.CurrentInstruction));

    private static readonly PropertyInfo? CurrentHandlePositionProperty =
        AccessTools.Property(
            typeof(S1ObjectScripts.BrickPressHandle),
            nameof(S1ObjectScripts.BrickPressHandle.CurrentPosition));

#if MONOMELON
    private static readonly FieldInfo? CurrentHandlePositionField =
        AccessTools.Field(
            typeof(S1ObjectScripts.BrickPressHandle),
            "<CurrentPosition>k__BackingField");
#endif

    private static ManualTabletPressAsset? _pressAsset;
    private static Func<GameObject>? _pillSourceFactory;
    private static Func<GameObject>? _crystalSourceFactory;
    private static Action? _onSuccessfulPress;
    private static MelonLogger.Instance? _logger;

    internal static void Configure(
        ManualTabletPressAsset pressAsset,
        Func<GameObject> pillSourceFactory,
        Func<GameObject> crystalSourceFactory,
        Action onSuccessfulPress,
        MelonLogger.Instance logger)
    {
        _pressAsset = pressAsset ??
            throw new ArgumentNullException(nameof(pressAsset));
        _pillSourceFactory = pillSourceFactory ??
            throw new ArgumentNullException(nameof(pillSourceFactory));
        _crystalSourceFactory = crystalSourceFactory ??
            throw new ArgumentNullException(nameof(crystalSourceFactory));
        _onSuccessfulPress = onSuccessfulPress ??
            throw new ArgumentNullException(nameof(onSuccessfulPress));
        _logger = logger ??
            throw new ArgumentNullException(nameof(logger));
    }

    internal static void Reset()
    {
        foreach (ManualTabletPressInstance instance in Instances.Values)
            instance.Dispose();

        Instances.Clear();
        CircularHandleInteractions.Clear();
        CompletionGates.Clear();
        _pressAsset = null;
        _pillSourceFactory = null;
        _crystalSourceFactory = null;
        _onSuccessfulPress = null;
        _logger = null;
    }

    internal static bool IsTabletPress(S1ObjectScripts.BrickPress? press) =>
        press != null &&
        string.Equals(
            press.ItemInstance?.ID,
            MdmaModule.TabletPressItemId,
            StringComparison.OrdinalIgnoreCase);

    internal static bool IsTabletPressDefinition(
        S1ItemFramework.BuildableItemDefinition? definition) =>
        definition != null &&
        string.Equals(
            definition.ID,
            MdmaModule.TabletPressItemId,
            StringComparison.OrdinalIgnoreCase);

    internal static void Attach(S1ObjectScripts.BrickPress press)
    {
        int pressKey = press.GetInstanceID();
        if (!IsTabletPress(press) || Instances.ContainsKey(pressKey))
            return;

        if (_pressAsset == null ||
            _pillSourceFactory == null ||
            _crystalSourceFactory == null ||
            _logger == null)
        {
            MelonLogger.Warning(
                "Skipped Manual Tablet Press visuals because runtime assets are not configured.");
            return;
        }

        try
        {
            Instances.Add(
                pressKey,
                new ManualTabletPressInstance(
                    press,
                    _pressAsset,
                    _pillSourceFactory,
                    _crystalSourceFactory));
        }
        catch (Exception exception)
        {
            _logger.Error(
                $"Failed to attach the Manual Tablet Press runtime: {exception}");
        }
    }

    internal static void AttachGhost(
        S1EntityFramework.GridItem ghost,
        S1ItemFramework.BuildableItemDefinition definition)
    {
        if (!IsTabletPressDefinition(definition) || _pressAsset == null)
            return;

        if (ghost.transform.Find("DrugExpansion_ManualTabletPress") != null)
            return;

        try
        {
            HideNativeRenderers(ghost.gameObject);
            ManualTabletPressRig rig = _pressAsset.CreateInstance(ghost.transform);
            DisableReferenceAnimation(rig.Root);
            DisableReferenceProcessVisuals(rig.Root);
        }
        catch (Exception exception)
        {
            _logger?.Error(
                $"Failed to create the Manual Tablet Press placement ghost: {exception}");
        }
    }

    internal static void Tick(S1ObjectScripts.BrickPress press)
    {
        if (Instances.TryGetValue(
                press.GetInstanceID(),
                out ManualTabletPressInstance? instance))
            instance.Tick();
    }

    internal static void RefreshManagementIcon(Sprite? icon)
    {
        if (icon == null)
            return;

        foreach (ManualTabletPressInstance instance in Instances.Values)
            instance.SetManagementIcon(icon);
    }

    internal static void Detach(S1ObjectScripts.BrickPress press)
    {
        if (press == null)
            return;

        int pressKey = press.GetInstanceID();
        if (Instances.TryGetValue(
                pressKey,
                out ManualTabletPressInstance? instance))
        {
            Instances.Remove(pressKey);
            instance.Dispose();
        }

        CompletionGates.Remove(pressKey);
        PreparedNativeLoads.Remove(press);
        if (press.Handle != null)
            CircularHandleInteractions.Remove(
                press.Handle.GetInstanceID());
    }

    internal static bool TryGetSufficientCrystals(
        S1ObjectScripts.BrickPress press,
        out S1ItemFramework.QualityItemInstance? crystals)
    {
        crystals = null;
        int quantity = 0;

        foreach (S1ItemFramework.ItemSlot slot in press.InputSlots)
        {
            S1ItemFramework.QualityItemInstance? candidate =
                MdmaBatchRegistry.AsQuality(slot.ItemInstance);
            if (candidate == null ||
                !string.Equals(
                    candidate.ID,
                    MdmaProductIds.Crystals,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (crystals == null)
            {
                crystals = candidate;
            }
            else if (!crystals.CanStackWith(candidate, checkQuantities: false))
            {
                continue;
            }

            quantity += slot.Quantity;
            if (quantity >= ManualTabletPressQuantities.CrystalsPerCycle)
                return true;
        }

        crystals = null;
        return false;
    }

    internal static S1ObjectScripts.PackagingStation.EState GetState(
        S1ObjectScripts.BrickPress press)
    {
        if (!TryGetSufficientCrystals(
                press,
                out S1ItemFramework.QualityItemInstance? crystals) ||
            crystals == null)
        {
            return S1ObjectScripts.PackagingStation.EState.InsufficentProduct;
        }

        S1ItemFramework.ItemInstance? output = press.OutputSlot.ItemInstance;
        if (output == null)
            return S1ObjectScripts.PackagingStation.EState.CanBegin;

        S1Product.ProductItemInstance? outputProduct =
            MdmaBatchRegistry.AsProduct(output);
        if (outputProduct == null ||
            !string.Equals(
                outputProduct.ID,
                MdmaProductIds.Tablets,
                StringComparison.OrdinalIgnoreCase))
        {
            return S1ObjectScripts.PackagingStation.EState.Mismatch;
        }

        MdmaBatchProfile expected =
            MdmaBatchRegistry.GetOrCreate(crystals).Press(
                MdmaTabletColor.Pink,
                MdmaTabletImprint.Heart,
                string.Empty);
        if (!MdmaBatchRegistry.GetOrCreate(outputProduct).Equals(expected))
            return S1ObjectScripts.PackagingStation.EState.Mismatch;

        return output.Quantity +
                   ManualTabletPressQuantities.TabletsPerCycle <=
               output.StackLimit
            ? S1ObjectScripts.PackagingStation.EState.CanBegin
            : S1ObjectScripts.PackagingStation.EState.OutputSlotFull;
    }

    internal static bool CompletePress(S1ObjectScripts.BrickPress press)
    {
        if (!IsTabletPress(press))
            return false;

        int pressKey = press.GetInstanceID();
        if (!CompletionGates.TryGetValue(
                pressKey,
                out ManualTabletPressCompletionGate? gate))
        {
            gate = new ManualTabletPressCompletionGate();
            CompletionGates.Add(pressKey, gate);
        }
        if (!gate.TryCommit())
            return true;

        if (!TryGetSufficientCrystals(
                press,
                out S1ItemFramework.QualityItemInstance? authoritativeCrystals) ||
            authoritativeCrystals == null ||
            GetState(press) != S1ObjectScripts.PackagingStation.EState.CanBegin)
        {
            _logger?.Warning(
                "Rejected a Manual Tablet Press completion because its replicated slots were no longer ready.");
            return true;
        }

        S1Product.ProductDefinition? tabletDefinition =
            GetNativeProductDefinition(MdmaProductIds.Tablets);
        if (tabletDefinition == null)
        {
            _logger?.Error(
                $"Cannot complete tablet pressing because '{MdmaProductIds.Tablets}' is not registered.");
            return true;
        }

        MdmaPressedTabletBatch conversion =
            MdmaTabletPressConversion.Convert(
                MdmaBatchRegistry.GetOrCreate(authoritativeCrystals),
                (int)authoritativeCrystals.Quality);
        var tablets = new S1Product.ProductItemInstance(
            tabletDefinition,
            ManualTabletPressQuantities.TabletsPerCycle,
            (S1ItemFramework.EQuality)conversion.Quality);
        MdmaBatchRegistry.Attach(tablets, conversion.Profile);

        press.OutputSlot.AddItem(tablets);
        ConsumeCrystals(press, authoritativeCrystals);
        _onSuccessfulPress?.Invoke();
        return true;
    }

    internal static void RefreshCanvas(S1UIStations.BrickPressCanvas canvas)
    {
        S1ObjectScripts.BrickPress? press = canvas.Press;
        if (!IsTabletPress(press) || press == null)
            return;

        SetCanvasTitle(canvas, "Manual Tablet Press");

        switch (GetState(press))
        {
            case S1ObjectScripts.PackagingStation.EState.CanBegin:
                canvas.InstructionLabel.enabled = false;
                canvas.BeginButton.interactable = true;
                return;
            case S1ObjectScripts.PackagingStation.EState.InsufficentProduct:
                canvas.InstructionLabel.text =
                    "Insert MDMA Crystals into input slots";
                break;
            case S1ObjectScripts.PackagingStation.EState.Mismatch:
                canvas.InstructionLabel.text =
                    "Output must contain matching MDMA tablets";
                break;
            default:
                canvas.InstructionLabel.text =
                    "Output slot needs room for 1x MDMA";
                break;
        }

        canvas.InstructionLabel.enabled = true;
        canvas.BeginButton.interactable = false;
    }

    private static void SetCanvasTitle(
        S1UIStations.BrickPressCanvas canvas,
        string title)
    {
        Transform? titleTransform =
            canvas.transform.Find("Container/Top/Title");
        TmpText? titleLabel =
            titleTransform?.GetComponent<TmpText>();
        if (titleLabel == null)
        {
            foreach (TmpText candidate in
                     canvas.GetComponentsInChildren<TmpText>(true))
            {
                if (string.Equals(
                        candidate.name,
                        "Title",
                        StringComparison.Ordinal))
                {
                    titleLabel = candidate;
                    break;
                }
            }
        }

        if (titleLabel != null)
            titleLabel.text = title;
    }

    private static void ConsumeCrystals(
        S1ObjectScripts.BrickPress press,
        S1ItemFramework.QualityItemInstance crystals)
    {
        int remaining = ManualTabletPressQuantities.CrystalsPerCycle;
        foreach (S1ItemFramework.ItemSlot slot in press.InputSlots)
        {
            if (remaining <= 0)
                break;

            S1ItemFramework.QualityItemInstance? candidate =
                MdmaBatchRegistry.AsQuality(slot.ItemInstance);
            if (candidate == null ||
                !candidate.CanStackWith(crystals, checkQuantities: false))
            {
                continue;
            }

            int consumed = Mathf.Min(remaining, slot.Quantity);
            slot.ChangeQuantity(-consumed);
            remaining -= consumed;
        }
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

    private static S1Product.ProductItemInstance? AsProduct(
        S1ItemFramework.ItemInstance? instance)
    {
#if IL2CPPMELON
        return instance?.TryCast<S1Product.ProductItemInstance>();
#else
        return instance as S1Product.ProductItemInstance;
#endif
    }

    private static void ArmCompletion(S1ObjectScripts.BrickPress press)
    {
        int pressKey = press.GetInstanceID();
        if (!CompletionGates.TryGetValue(
                pressKey,
                out ManualTabletPressCompletionGate? gate))
        {
            gate = new ManualTabletPressCompletionGate();
            CompletionGates.Add(pressKey, gate);
        }

        gate.Arm();
    }

    private static S1Product.ProductItemInstance? CreateTaskProductSurrogate()
    {
        S1Product.ProductDefinition? definition =
            GetNativeProductDefinition(MdmaProductIds.Tablets);
        return definition == null
            ? null
            : AsProduct(definition.GetDefaultInstance(1));
    }

    private static bool IsTabletPressInputSlot(
        S1ItemFramework.ItemSlot slot,
        out S1ObjectScripts.BrickPress? press)
    {
        press = AsBrickPress(slot.SlotOwner);
        if (!IsTabletPress(press) || press == null)
            return false;

        foreach (S1ItemFramework.ItemSlot inputSlot in press.InputSlots)
        {
            if (ReferenceEquals(inputSlot, slot) || inputSlot == slot)
                return true;
        }

        return false;
    }

#if IL2CPPMELON
    private static S1ObjectScripts.BrickPress? AsBrickPress(
        S1ItemFramework.IItemSlotOwner? owner) =>
        owner?.TryCast<S1ObjectScripts.BrickPress>();
#else
    private static S1ObjectScripts.BrickPress? AsBrickPress(
        S1ItemFramework.IItemSlotOwner? owner) =>
        owner as S1ObjectScripts.BrickPress;
#endif

#if IL2CPPMELON
    private static S1ObjectScripts.BrickPress? AsBrickPress(
        S1Management.IConfigurable? configurable) =>
        configurable?.TryCast<S1ObjectScripts.BrickPress>();
#else
    private static S1ObjectScripts.BrickPress? AsBrickPress(
        S1Management.IConfigurable? configurable) =>
        configurable as S1ObjectScripts.BrickPress;
#endif

    private static void HideNativeRenderers(GameObject root)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = false;
    }

    private static void DisableReferenceAnimation(GameObject root)
    {
        foreach (Component component in root.GetComponentsInChildren<Component>(true))
        {
            if (component is Behaviour behaviour &&
                string.Equals(
                    component.GetType().Name,
                    "Animator",
                    StringComparison.Ordinal))
            {
                behaviour.enabled = false;
            }
        }
    }

    private static void DisableReferenceProcessVisuals(GameObject root)
    {
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform.name.StartsWith(
                    "FinishedTablet_",
                    StringComparison.Ordinal) ||
                string.Equals(
                    transform.name,
                    "FreshTabletAssembly",
                    StringComparison.Ordinal) ||
                string.Equals(
                    transform.name,
                    "FeedPowderAssembly",
                    StringComparison.Ordinal) ||
                string.Equals(
                    transform.name,
                    "DieFillAssembly",
                    StringComparison.Ordinal))
            {
                transform.gameObject.SetActive(false);
            }
        }
    }

    private static void BeginAutoLoadedPress(
        S1PlayerTasks.UseBrickPress task,
        S1ObjectScripts.BrickPress press)
    {
        try
        {
            if (BeginNativePressMethod == null)
            {
                throw new MissingMethodException(
                    typeof(S1PlayerTasks.UseBrickPress).FullName,
                    "BeginPress");
            }

            ResetHandle(press.Handle);
            ArmCompletion(press);
            BeginNativePressMethod.Invoke(task, null);
            if (PreparedNativeLoads.TryGetValue(
                    press,
                    out PreparedNativeLoad? prepared))
            {
                prepared.Hide();
            }

            try
            {
                CurrentInstructionProperty?.SetValue(
                    task,
                    "Turn the wheel clockwise to press MDMA tablets");
            }
            catch (Exception exception)
            {
                _logger?.Warning(
                    "The Manual Tablet Press started, but its task instruction " +
                    $"could not be renamed: {exception.Message}");
            }
        }
        catch (Exception exception)
        {
            _logger?.Error(
                "Failed to start the Manual Tablet Press with its hopper " +
                $"preloaded; preserving the native loading task: {exception}");
        }
        finally
        {
            PreparedNativeLoads.Remove(press);
        }
    }

    private static S1ObjectScripts.BrickPress? GetTaskPress(
        S1PlayerTasks.UseBrickPress task)
    {
#if IL2CPPMELON
        return task.press;
#else
        return UseBrickPressPressField?.GetValue(task) as
            S1ObjectScripts.BrickPress;
#endif
    }

    internal static void RegisterCircularHandle(
        S1ObjectScripts.BrickPressHandle handle,
        Transform wheelPivot)
    {
        int handleKey = handle.GetInstanceID();
        CircularHandleInteractions.Remove(handleKey);
        CircularHandleInteractions.Add(
            handleKey,
            new CircularHandleInteraction(wheelPivot));
    }

    private static void ResetHandle(S1ObjectScripts.BrickPressHandle handle)
    {
        handle.SetPosition(0f);
        try
        {
            if (CurrentHandlePositionProperty?.SetMethod != null)
                CurrentHandlePositionProperty.SetValue(handle, 0f);
#if MONOMELON
            else
                CurrentHandlePositionField?.SetValue(handle, 0f);
#endif
        }
        catch (Exception exception)
        {
            _logger?.Warning(
                "The Manual Tablet Press wheel target was reset, but its " +
                $"rendered position will return home normally: {exception.Message}");
        }

        if (CircularHandleInteractions.TryGetValue(
                handle.GetInstanceID(),
                out CircularHandleInteraction? interaction))
        {
            interaction.Reset();
        }
    }

    private sealed class CircularHandleInteraction
    {
        private const float MinimumPointerRadiusPixels = 12f;

        private readonly Transform _wheelPivot;

        private bool _dragging;
        private float _lastPointerAngle;
        private float _accumulatedDegrees;

        internal CircularHandleInteraction(Transform wheelPivot)
        {
            _wheelPivot = wheelPivot;
        }

        internal void BeginDrag(S1ObjectScripts.BrickPressHandle handle)
        {
            if (!TryGetPointerAngle(out _lastPointerAngle))
                return;

            _accumulatedDegrees =
                handle.CurrentPosition *
                ManualTabletPressWheel.RequiredDegrees;
            _dragging = true;
        }

        internal void EndDrag()
        {
            _dragging = false;
        }

        internal void Reset()
        {
            _dragging = false;
            _lastPointerAngle = 0f;
            _accumulatedDegrees = 0f;
        }

        internal bool Tick(S1ObjectScripts.BrickPressHandle handle)
        {
            if (S1.GameInput.GetCurrentInputDeviceIsGamepad() ||
                MoveNativeHandleMethod == null)
            {
                return false;
            }

            if (!handle.Locked)
            {
                if (_dragging &&
                    TryGetPointerAngle(out float pointerAngle))
                {
                    _accumulatedDegrees =
                        ManualTabletPressWheel.AdvanceClockwise(
                            _accumulatedDegrees,
                            _lastPointerAngle,
                            pointerAngle);
                    _lastPointerAngle = pointerAngle;
                    handle.SetPosition(
                        ManualTabletPressWheel.ToProgress(
                            _accumulatedDegrees));
                }
                else if (!_dragging)
                {
                    float target =
                        Mathf.MoveTowards(
                            handle.TargetPosition,
                            0f,
                            Time.deltaTime);
                    _accumulatedDegrees =
                        target *
                        ManualTabletPressWheel.RequiredDegrees;
                    handle.SetPosition(target);
                }
            }

            MoveNativeHandleMethod.Invoke(handle, null);
            return true;
        }

        private bool TryGetPointerAngle(out float angle)
        {
            angle = 0f;
            Camera camera = Camera.main;
            if (camera == null)
                return false;

            Vector3 wheelScreenPoint =
                camera.WorldToScreenPoint(_wheelPivot.position);
            if (wheelScreenPoint.z <= 0f)
                return false;

            Vector2 pointer = S1.GameInput.MousePosition;
            Vector2 offset =
                pointer -
                new Vector2(wheelScreenPoint.x, wheelScreenPoint.y);
            if (offset.sqrMagnitude <
                MinimumPointerRadiusPixels * MinimumPointerRadiusPixels)
            {
                return false;
            }

            angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
            return true;
        }
    }

    private sealed class PreparedNativeLoad
    {
        private readonly S1PlayerTasks.Draggable _container;
        private readonly S1FunctionalProductList _products;

        internal PreparedNativeLoad(
            S1PlayerTasks.Draggable container,
            S1FunctionalProductList products)
        {
            _container = container;
            _products = products;
        }

        internal void Hide()
        {
            if (_container != null)
                _container.gameObject.SetActive(false);

            foreach (S1Product.FunctionalProduct product in _products)
            {
                if (product != null)
                    product.gameObject.SetActive(false);
            }
        }
    }

    [HarmonyPatch(
        typeof(S1ObjectScripts.BrickPress),
        nameof(S1ObjectScripts.BrickPress.InitializeGridItem))]
    private static class InitializeGridItemPatch
    {
        private static void Postfix(S1ObjectScripts.BrickPress __instance) =>
            Attach(__instance);
    }

    [HarmonyPatch(typeof(S1ObjectScripts.BrickPress), "LateUpdate")]
    private static class LateUpdatePatch
    {
        private static void Postfix(S1ObjectScripts.BrickPress __instance) =>
            Tick(__instance);
    }

    [HarmonyPatch(typeof(S1ObjectScripts.BrickPress), "Destroy")]
    private static class DestroyPatch
    {
        private static void Prefix(S1ObjectScripts.BrickPress __instance)
        {
            if (IsTabletPress(__instance))
                Detach(__instance);
        }
    }

    [HarmonyPatch(
        typeof(S1ItemFramework.ItemSlot),
        nameof(S1ItemFramework.ItemSlot.DoesItemMatchHardFilters))]
    private static class TabletPressInputFilterPatch
    {
        private static bool Prefix(
            S1ItemFramework.ItemSlot __instance,
            S1ItemFramework.ItemInstance item,
            ref bool __result)
        {
            if (item == null ||
                !string.Equals(
                    item.ID,
                    MdmaProductIds.Crystals,
                    StringComparison.OrdinalIgnoreCase) ||
                !IsTabletPressInputSlot(__instance, out _))
            {
                return true;
            }

            __result = true;
            return false;
        }
    }

    [HarmonyPatch(
        typeof(S1ObjectScripts.BrickPress),
        nameof(S1ObjectScripts.BrickPress.HasSufficientProduct))]
    private static class HasSufficientProductPatch
    {
        private static bool Prefix(
            S1ObjectScripts.BrickPress __instance,
            ref S1Product.ProductItemInstance product,
            ref bool __result)
        {
            if (!IsTabletPress(__instance))
                return true;

            __result =
                TryGetSufficientCrystals(
                    __instance,
                    out S1ItemFramework.QualityItemInstance? _) &&
                (product = CreateTaskProductSurrogate()!) != null;
            return false;
        }
    }

    [HarmonyPatch(
        typeof(S1ObjectScripts.BrickPress),
        nameof(S1ObjectScripts.BrickPress.GetState))]
    private static class GetStatePatch
    {
        private static bool Prefix(
            S1ObjectScripts.BrickPress __instance,
            ref S1ObjectScripts.PackagingStation.EState __result)
        {
            if (!IsTabletPress(__instance))
                return true;

            __result = GetState(__instance);
            return false;
        }
    }

    [HarmonyPatch(
        typeof(S1ObjectScripts.BrickPress),
        nameof(S1ObjectScripts.BrickPress.CompletePress))]
    private static class CompletePressPatch
    {
        private static bool Prefix(
            S1ObjectScripts.BrickPress __instance,
            S1Product.ProductItemInstance product) =>
            !CompletePress(__instance);
    }

    [HarmonyPatch(
        typeof(S1ObjectScripts.BrickPress),
        nameof(S1ObjectScripts.BrickPress.PlayPressAnim))]
    private static class PlayPressAnimPatch
    {
        private static void Prefix(S1ObjectScripts.BrickPress __instance)
        {
            if (IsTabletPress(__instance))
                ArmCompletion(__instance);
        }
    }

    [HarmonyPatch(
        typeof(S1ObjectScripts.BrickPress),
        nameof(S1ObjectScripts.BrickPress.CreateFunctionalContainer))]
    private static class CreateFunctionalContainerPatch
    {
        private static void Postfix(
            S1ObjectScripts.BrickPress __instance,
            S1PlayerTasks.Draggable __result,
            S1FunctionalProductList products)
        {
            if (!IsTabletPress(__instance) ||
                __result == null ||
                products == null)
            {
                return;
            }

            PreparedNativeLoads.Remove(__instance);
            PreparedNativeLoads.Add(
                __instance,
                new PreparedNativeLoad(__result, products));
        }
    }

    [HarmonyPatch(
        typeof(S1PlayerTasks.UseBrickPress),
        "CheckMould")]
    private static class UseBrickPressCheckMouldPatch
    {
        private static bool Prefix(S1PlayerTasks.UseBrickPress __instance)
        {
            S1ObjectScripts.BrickPress? press = GetTaskPress(__instance);
            if (!IsTabletPress(press) || press == null)
                return true;

            if (!ActiveTabletPressTasks.TryGetValue(__instance, out _))
            {
                ActiveTabletPressTasks.Remove(__instance);
                ActiveTabletPressTasks.Add(__instance, press);
                BeginAutoLoadedPress(__instance, press);
            }

            return false;
        }
    }

    [HarmonyPatch(
        typeof(S1PlayerTasks.UseBrickPress),
        nameof(S1PlayerTasks.UseBrickPress.StopTask))]
    private static class UseBrickPressStopTaskPatch
    {
        private static void Postfix(S1PlayerTasks.UseBrickPress __instance)
        {
            if (!ActiveTabletPressTasks.TryGetValue(
                    __instance,
                    out S1ObjectScripts.BrickPress? press))
            {
                return;
            }

            ResetHandle(press.Handle);
            ActiveTabletPressTasks.Remove(__instance);
        }
    }

    [HarmonyPatch(
        typeof(S1ObjectScripts.BrickPressHandle),
        nameof(S1ObjectScripts.BrickPressHandle.ClickStart))]
    private static class HandleClickStartPatch
    {
        private static void Postfix(
            S1ObjectScripts.BrickPressHandle __instance)
        {
            if (CircularHandleInteractions.TryGetValue(
                    __instance.GetInstanceID(),
                    out CircularHandleInteraction? interaction))
            {
                interaction.BeginDrag(__instance);
            }
        }
    }

    [HarmonyPatch(
        typeof(S1ObjectScripts.BrickPressHandle),
        nameof(S1ObjectScripts.BrickPressHandle.ClickEnd))]
    private static class HandleClickEndPatch
    {
        private static void Postfix(
            S1ObjectScripts.BrickPressHandle __instance)
        {
            if (CircularHandleInteractions.TryGetValue(
                    __instance.GetInstanceID(),
                    out CircularHandleInteraction? interaction))
            {
                interaction.EndDrag();
            }
        }
    }

    [HarmonyPatch(typeof(S1ObjectScripts.BrickPressHandle), "LateUpdate")]
    private static class HandleLateUpdatePatch
    {
        private static bool Prefix(
            S1ObjectScripts.BrickPressHandle __instance)
        {
            return !CircularHandleInteractions.TryGetValue(
                       __instance.GetInstanceID(),
                       out CircularHandleInteraction? interaction) ||
                   !interaction.Tick(__instance);
        }
    }

    [HarmonyPatch(typeof(S1UIStations.BrickPressCanvas), "UpdateUI")]
    private static class CanvasUpdatePatch
    {
        private static void Postfix(S1UIStations.BrickPressCanvas __instance) =>
            RefreshCanvas(__instance);
    }

    [HarmonyPatch(
        typeof(S1UIManagement.ManagementWorldspaceCanvas),
        "UpdateInputPrompt")]
    private static class ManagementPromptPatch
    {
        private static void Postfix(
            S1UIManagement.ManagementWorldspaceCanvas __instance)
        {
            if (SetManagementPromptMethod == null)
                return;

            int count = 0;
            S1Management.IConfigurable? hovered = __instance.HoveredConfigurable;
            if (hovered != null &&
                !__instance.SelectedConfigurables.Contains(hovered))
            {
                if (!IsTabletPress(AsBrickPress(hovered)))
                    return;
                count++;
            }

            foreach (S1Management.IConfigurable configurable in
                     __instance.SelectedConfigurables)
            {
                if (!IsTabletPress(AsBrickPress(configurable)))
                    return;
                count++;
            }

            if (count == 0)
                return;

            string prompt = count == 1
                ? "Manage Manual Tablet Press"
                : $"Manage {count}x Manual Tablet Press";
            SetManagementPromptMethod.Invoke(__instance, new object[] { prompt });
        }
    }

    [HarmonyPatch(typeof(S1Building.BuildStart_Grid), "CreateGhostModel")]
    private static class CreateGhostModelPatch
    {
        private static void Postfix(
            S1ItemFramework.BuildableItemDefinition itemDefinition,
            S1EntityFramework.GridItem __result)
        {
            if (__result != null)
                AttachGhost(__result, itemDefinition);
        }
    }
}

internal sealed class ManualTabletPressInstance
{
    private const int MaximumVisibleTablets = 6;
    private const float EjectionIntervalSeconds = 0.11f;
    private const float GuidedPathSeconds = 0.65f;
    private const float FeedShoeAlignmentOffset = 0.19f;

    private readonly S1ObjectScripts.BrickPress _press;
    private readonly Func<GameObject> _pillSourceFactory;
    private readonly ManualTabletPressRig _rig;
    private readonly GameObject _hopperCrystals;
    private readonly GameObject _shoeCrystal;
    private readonly GameObject _dieGranules;
    private readonly Quaternion _handleHomeRotation;
    private readonly Vector3 _ramRaised;
    private readonly Vector3 _ramLowered;
    private readonly Vector3 _feedHome;
    private readonly Vector3 _feedAtDie;
    private readonly Vector3 _ejectorHome;
    private readonly List<GameObject> _tablets = new();

    private int _observedOutputQuantity = -1;
    private object? _ejectionCoroutine;
    private bool _disposed;
    private bool _ejectionRunning;
    private int _queuedEjections;
    private uint _sequence;

    internal ManualTabletPressInstance(
        S1ObjectScripts.BrickPress press,
        ManualTabletPressAsset asset,
        Func<GameObject> pillSourceFactory,
        Func<GameObject> crystalSourceFactory)
    {
        _press = press;
        _pillSourceFactory = pillSourceFactory;

        HideNativeRenderers(press.gameObject);
        _rig = asset.CreateInstance(press.transform);
        DisableReferenceAnimation(_rig.Root);
        DisableReferenceProcessVisuals(_rig.Root);
        GameObject crystalSource = crystalSourceFactory();
        _hopperCrystals =
            CreateCrystalVisual(
                crystalSource,
                "DrugExpansion_HopperCrystals",
                _rig.Root.transform,
                Require(_rig.Root, "PowderHopperRim"),
                0.75f,
                -0.009f,
                "CrystalPile",
                "CrystalChunk_A",
                "CrystalChunk_B");
        _shoeCrystal =
            CreateCrystalVisual(
                crystalSource,
                "DrugExpansion_FeedCrystal",
                _rig.FeedShoeAssembly,
                Require(_rig.Root, "FeedPowder"),
                0.85f,
                0.010f,
                "CrystalPile");
        _dieGranules =
            CreateCrystalVisual(
                crystalSource,
                "DrugExpansion_DieGranules",
                _rig.Root.transform,
                Require(_rig.Root, "DiePowderFill"),
                0.52f,
                0.002f,
                "CrystalGranules");

        _handleHomeRotation = _rig.HandlePivot.localRotation;
        _ramRaised = _rig.RamAssembly.localPosition;
        _ramLowered =
            _ramRaised +
            _rig.RamAssembly.parent.InverseTransformVector(
                _rig.PressLowered.position -
                _rig.PressRaised.position);
        _feedHome = _rig.FeedShoeAssembly.localPosition;
        Transform feedParent = _rig.FeedShoeAssembly.parent;
        Vector3 feedPocketInParent =
            feedParent.InverseTransformPoint(
                Require(_rig.Root, "FeedPowder").position);
        Vector3 dieInFeedParent =
            feedParent.InverseTransformPoint(
                _rig.MouldDetector.position);
        float feedTravel =
            dieInFeedParent.x -
            feedPocketInParent.x;
        _feedAtDie = new Vector3(
            _feedHome.x +
            feedTravel +
            Mathf.Sign(feedTravel) * FeedShoeAlignmentOffset,
            _feedHome.y,
            _feedHome.z);
        _ejectorHome = _rig.EjectorAssembly.localPosition;

        ConfigureNativeInteraction();
        Sprite? managementIcon = press.ItemInstance?.Definition?.Icon;
        if (managementIcon != null)
            SetManagementIcon(managementIcon);
        AddTrayColliders();
        ApplyMechanics(0f);
    }

    internal void SetManagementIcon(Sprite icon) =>
        _press.typeIcon = icon;

    internal void Tick()
    {
        if (_disposed || _rig.Root == null)
            return;

        float progress = Mathf.Clamp01(_press.Handle.CurrentPosition);
        ApplyMechanics(progress);
        ObserveOutput();
    }

    private void ConfigureNativeInteraction()
    {
        Vector3 pouringFocus =
            _rig.MouldDetector.position +
            _rig.Root.transform.up * 0.16f;
        _rig.CameraPressing.LookAt(
            _rig.HandlePivot.position,
            _rig.Root.transform.up);
        _rig.CameraPouring.LookAt(
            pouringFocus,
            _rig.Root.transform.up);
        _rig.PlaneNormal.LookAt(
            _rig.CameraPressing.position,
            _rig.Root.transform.up);

        _press.CameraPosition = _rig.CameraPressing;
        _press.CameraPosition_Pouring = _rig.CameraPouring;
        _press.CameraPosition_Raising = _rig.CameraPressing;
        _rig.StandPoint.localRotation *= Quaternion.Euler(0f, 180f, 0f);
        _press.StandPoint = _rig.StandPoint;
        _press.ContainerSpawnPoint = _rig.ContainerSpawnPoint;

        _press.Handle.PlaneNormal = _rig.PlaneNormal;
        _press.Handle.RaisedTransform = _rig.HandleClickableAnchor;
        _press.Handle.LoweredTransform = _rig.HandleLowered;

        Transform clickable = _press.Handle.HandleClickable.transform;
        clickable.position = _rig.HandleClickableAnchor.position;
        clickable.rotation = _rig.HandleClickableAnchor.rotation;
        clickable.SetParent(_rig.HandlePivot, true);
        ManualTabletPressRuntime.RegisterCircularHandle(
            _press.Handle,
            _rig.HandlePivot);

        Transform mould = _press.MouldDetection.transform;
        mould.position = _rig.MouldDetector.position;
        mould.rotation = _rig.MouldDetector.rotation;
        _press.MouldDetection.size = new Vector3(0.28f, 0.18f, 0.28f);

        if (_press.OutputVisuals != null)
        {
            // StorageVisualizer subscribes QueueRefresh directly to the output
            // slot. Disabling the component does not stop that delegate, so the
            // native Brick Press would still create a second pill beside the
            // custom collection tray whenever the output quantity changed.
            _press.OutputVisuals.BlockRefreshes = true;
            _press.OutputVisuals.enabled = false;
        }
    }

    private void ApplyMechanics(float progress)
    {
        _rig.HandlePivot.localRotation =
            _handleHomeRotation *
            Quaternion.AngleAxis(
                360f *
                ManualTabletPressWheel.RequiredTurns *
                progress,
                Vector3.forward);
        _rig.RamAssembly.localPosition =
            Vector3.Lerp(_ramRaised, _ramLowered, progress);
        float ejectProgress =
            progress <= 0.82f
                ? 0f
                : Mathf.SmoothStep(0f, 1f, (progress - 0.82f) / 0.18f);
        _rig.EjectorAssembly.localPosition =
            _ejectorHome + Vector3.up * (0.10f * ejectProgress);

        bool hasCrystals =
            ManualTabletPressRuntime.TryGetSufficientCrystals(
                _press,
                out _);
        bool hasProcessMaterial =
            hasCrystals ||
            progress > 0.001f && progress < 0.82f;
        float feedProgress =
            progress <= 0.30f
                ? Mathf.SmoothStep(0f, 1f, progress / 0.30f)
                : progress >= 0.55f
                    ? Mathf.SmoothStep(1f, 0f, (progress - 0.55f) / 0.35f)
                    : 1f;
        _rig.FeedShoeAssembly.localPosition =
            Vector3.Lerp(_feedHome, _feedAtDie, feedProgress);

        bool crystalOnShoe =
            hasProcessMaterial && progress < 0.55f;
        bool powderInDie =
            hasProcessMaterial &&
            progress >= 0.55f &&
            progress < 0.82f;
        _hopperCrystals.SetActive(hasCrystals);
        _shoeCrystal.SetActive(crystalOnShoe);
        _dieGranules.SetActive(powderInDie);
    }

    private void ObserveOutput()
    {
        int outputQuantity = GetOutputQuantity();
        if (_observedOutputQuantity < 0)
        {
            _observedOutputQuantity = outputQuantity;
            RebuildSettledTablets(outputQuantity);
            return;
        }

        if (outputQuantity == _observedOutputQuantity)
            return;

        int delta = outputQuantity - _observedOutputQuantity;
        _observedOutputQuantity = outputQuantity;
        bool stationInUse =
            _press.PlayerUserObject != null ||
            _press.NPCUserObject != null;
        if (ManualTabletPressEjection.ShouldAnimate(
                outputQuantity - delta,
                outputQuantity,
                stationInUse))
        {
            QueueEjections(delta);
        }
        else
        {
            RebuildSettledTablets(outputQuantity);
        }
    }

    private int GetOutputQuantity()
    {
        S1ItemFramework.ItemInstance? output = _press.OutputSlot?.ItemInstance;
        return output != null &&
               string.Equals(
                   output.ID,
                   MdmaProductIds.Tablets,
                   StringComparison.OrdinalIgnoreCase)
            ? output.Quantity
            : 0;
    }

    private void QueueEjections(int count)
    {
        if (_disposed)
            return;

        _queuedEjections += Math.Min(count, MaximumVisibleTablets);
        if (_ejectionRunning)
            return;

        _ejectionRunning = true;
        _ejectionCoroutine = MelonCoroutines.Start(EjectQueuedTablets());
    }

    private IEnumerator EjectQueuedTablets()
    {
        try
        {
            while (!_disposed &&
                   _rig.Root != null &&
                   _queuedEjections > 0)
            {
                _queuedEjections--;
                if (_tablets.Count >= MaximumVisibleTablets)
                    DestroyTabletAt(0);

                yield return AnimateOneTablet(_sequence++);
                if (_queuedEjections > 0)
                    yield return new WaitForSeconds(EjectionIntervalSeconds);
            }
        }
        finally
        {
            _queuedEjections = 0;
            _ejectionRunning = false;
            _ejectionCoroutine = null;
        }
    }

    private IEnumerator AnimateOneTablet(uint sequence)
    {
        if (_disposed || _rig.Root == null)
            yield break;

        GameObject tablet = CreateTabletVisual();
        _tablets.Add(tablet);

        Vector3 start =
            _rig.FreshTabletAssembly.position +
            _rig.EjectorAssembly.parent.TransformVector(Vector3.up * 0.10f);
        Vector3 end =
            _rig.OutputPoint.position +
            _press.transform.up * 0.12f +
            _press.transform.right *
                ManualTabletPressEjection.Jitter(sequence, 0.025f) +
            _press.transform.forward *
                ManualTabletPressEjection.Jitter(sequence + 11u, 0.05f);
        Vector3 control =
            Vector3.Lerp(start, end, 0.48f) +
            _press.transform.up * 0.18f;

        Quaternion startRotation =
            _press.transform.rotation * Quaternion.Euler(0f, 0f, -10f);
        Quaternion endRotation =
            _press.transform.rotation *
            Quaternion.Euler(
                78f,
                ManualTabletPressEjection.Unit(sequence + 23u) * 120f - 60f,
                -8f);

        float elapsed = 0f;
        while (!_disposed &&
               elapsed < GuidedPathSeconds &&
               tablet != null &&
               _rig.Root != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / GuidedPathSeconds);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            tablet.transform.position =
                QuadraticBezier(start, control, end, eased);
            tablet.transform.rotation =
                Quaternion.Slerp(startRotation, endRotation, eased);
            yield return null;
        }

        if (tablet == null)
            yield break;

        Rigidbody body = tablet.AddComponent<Rigidbody>();
        body.mass = 0.02f;
        body.drag = 0.18f;
        body.angularDrag = 0.28f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.velocity =
            _press.transform.right *
                (0.08f +
                 ManualTabletPressEjection.Unit(sequence + 31u) * 0.08f) +
            _press.transform.forward *
                ManualTabletPressEjection.Jitter(sequence + 47u, 0.09f) -
            _press.transform.up * 0.04f;
        body.angularVelocity = new Vector3(
            ManualTabletPressEjection.Jitter(sequence + 59u, 2.4f),
            ManualTabletPressEjection.Jitter(sequence + 71u, 2.4f),
            ManualTabletPressEjection.Jitter(sequence + 83u, 2.4f));
    }

    private GameObject CreateTabletVisual()
    {
        GameObject source = _pillSourceFactory();
        GameObject tablet = UnityEngine.Object.Instantiate(source);
        tablet.name = "DrugExpansion_EjectedTablet";
        tablet.transform.SetParent(_rig.Root.transform, true);
        tablet.transform.localScale = Vector3.one * 0.045f;
        tablet.layer = LayerMask.NameToLayer("Ignore Raycast");
        tablet.SetActive(true);

        foreach (Collider collider in tablet.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
            UnityEngine.Object.Destroy(collider);
        }

        foreach (Collider tabletCollider in AddTabletColliders(tablet))
            IgnoreNativePressColliders(tabletCollider);
        return tablet;
    }

    internal void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _queuedEjections = 0;
        if (_ejectionCoroutine != null)
        {
            MelonCoroutines.Stop(_ejectionCoroutine);
            _ejectionCoroutine = null;
        }

        _ejectionRunning = false;
        DestroyAllTablets();
        if (_rig.Root != null)
            UnityEngine.Object.Destroy(_rig.Root);
    }

    private GameObject CreateCrystalVisual(
        GameObject source,
        string name,
        Transform visualParent,
        Transform anchor,
        float scale,
        float verticalOffset,
        params string[] visibleVariants)
    {
        var visible = new HashSet<string>(
            visibleVariants,
            StringComparer.Ordinal);
        GameObject visual = UnityEngine.Object.Instantiate(source);
        visual.name = name;
        visual.transform.SetParent(visualParent, false);
        visual.transform.position =
            anchor.position + _rig.Root.transform.up * verticalOffset;
        visual.transform.rotation = _rig.Root.transform.rotation;
        visual.transform.localScale = Vector3.one * scale;
        visual.layer = LayerMask.NameToLayer("Ignore Raycast");

        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (Transform transform in
                 visual.GetComponentsInChildren<Transform>(true))
        {
            if (!IsCrystalVariant(transform.name))
                continue;

            bool enabled = visible.Contains(transform.name);
            transform.gameObject.SetActive(enabled);
            if (enabled)
                found.Add(transform.name);
        }

        if (!found.SetEquals(visible))
        {
            UnityEngine.Object.Destroy(visual);
            throw new InvalidOperationException(
                $"The MDMA crystal model is missing variants: " +
                $"{string.Join(", ", visible.Except(found))}.");
        }

        foreach (Collider collider in
                 visual.GetComponentsInChildren<Collider>(true))
        {
            UnityEngine.Object.Destroy(collider);
        }

        visual.SetActive(false);
        return visual;
    }

    private void RebuildSettledTablets(int outputQuantity)
    {
        DestroyAllTablets();

        int count = Math.Min(outputQuantity, MaximumVisibleTablets);
        for (int index = 0; index < count; index++)
        {
            GameObject tablet = CreateTabletVisual();
            const int columns = 3;
            int row = index / columns;
            int column = index % columns;
            float x = (column - 1f) * 0.07f +
                      ManualTabletPressEjection.Jitter(
                          (uint)index + 3u,
                          0.012f);
            float z = (row - 0.5f) * 0.09f +
                      ManualTabletPressEjection.Jitter(
                          (uint)index + 17u,
                          0.012f);
            tablet.transform.position =
                _rig.OutputPoint.position +
                _press.transform.right * x +
                _press.transform.forward * z +
                _press.transform.up * (0.018f + row * 0.008f);
            tablet.transform.rotation =
                _press.transform.rotation *
                Quaternion.Euler(
                    78f,
                    ManualTabletPressEjection.Unit((uint)index + 29u) *
                        100f -
                    50f,
                    -8f);
            _tablets.Add(tablet);
        }
    }

    private void AddTrayColliders()
    {
        AddMeshColliderBox("CollectionTrayBed");
        AddMeshColliderBox("CollectionTrayOuterWall");
        AddMeshColliderBox("CollectionTrayFrontWall");
        AddMeshColliderBox("CollectionTrayRearWall");
        AddMeshColliderBox("CollectionTrayBridge");
    }

    private void AddMeshColliderBox(string nodeName)
    {
        Transform? node = Find(_rig.Root, nodeName);
        if (node == null || node.GetComponent<Collider>() != null)
            return;

        MeshFilter? filter = node.GetComponent<MeshFilter>();
        if (filter?.sharedMesh == null)
            return;

        BoxCollider collider = node.gameObject.AddComponent<BoxCollider>();
        collider.center = filter.sharedMesh.bounds.center;
        collider.size = filter.sharedMesh.bounds.size;
    }

    private void IgnoreNativePressColliders(Collider tabletCollider)
    {
        foreach (Collider collider in
                 _press.GetComponentsInChildren<Collider>(true))
        {
            if (collider == tabletCollider ||
                collider.transform.IsChildOf(_rig.Root.transform))
            {
                continue;
            }

            Physics.IgnoreCollision(tabletCollider, collider, true);
        }
    }

    private static IReadOnlyList<Collider> AddTabletColliders(
        GameObject tablet)
    {
        var colliders = new List<Collider>();
        foreach (MeshFilter filter in
                 tablet.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                continue;

            MeshCollider collider =
                filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            collider.convex = true;
            colliders.Add(collider);
        }

        return colliders;
    }

    private void DestroyAllTablets()
    {
        for (int index = _tablets.Count - 1; index >= 0; index--)
            DestroyTabletAt(index);
    }

    private void DestroyTabletAt(int index)
    {
        GameObject tablet = _tablets[index];
        _tablets.RemoveAt(index);
        if (tablet != null)
            UnityEngine.Object.Destroy(tablet);
    }

    private static Transform? Find(GameObject root, string name)
    {
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(transform.name, name, StringComparison.Ordinal))
                return transform;
        }

        return null;
    }

    private static Transform Require(GameObject root, string name) =>
        Find(root, name) ??
        throw new InvalidOperationException(
            $"The tablet press model is missing visual anchor '{name}'.");

    private static bool IsCrystalVariant(string name) =>
        string.Equals(name, "CrystalPile", StringComparison.Ordinal) ||
        string.Equals(name, "CrystalChunk_A", StringComparison.Ordinal) ||
        string.Equals(name, "CrystalChunk_B", StringComparison.Ordinal) ||
        string.Equals(name, "CrystalGranules", StringComparison.Ordinal);

    private static Vector3 QuadraticBezier(
        Vector3 start,
        Vector3 control,
        Vector3 end,
        float t)
    {
        float inverse = 1f - t;
        return inverse * inverse * start +
               2f * inverse * t * control +
               t * t * end;
    }

    private static void HideNativeRenderers(GameObject root)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = false;
    }

    private static void DisableReferenceAnimation(GameObject root)
    {
        foreach (Component component in root.GetComponentsInChildren<Component>(true))
        {
            if (component is Behaviour behaviour &&
                string.Equals(
                    component.GetType().Name,
                    "Animator",
                    StringComparison.Ordinal))
            {
                behaviour.enabled = false;
            }
        }
    }

    private static void DisableReferenceProcessVisuals(GameObject root)
    {
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform.name.StartsWith(
                    "FinishedTablet_",
                    StringComparison.Ordinal) ||
                string.Equals(
                    transform.name,
                    "FreshTabletAssembly",
                    StringComparison.Ordinal) ||
                string.Equals(
                    transform.name,
                    "FeedPowderAssembly",
                    StringComparison.Ordinal) ||
                string.Equals(
                    transform.name,
                    "DieFillAssembly",
                    StringComparison.Ordinal))
            {
                transform.gameObject.SetActive(false);
            }
        }
    }
}
