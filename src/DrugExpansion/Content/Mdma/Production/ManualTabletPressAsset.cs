using DrugExpansion.Infrastructure;
using UnityEngine;

namespace DrugExpansion.Content.Mdma.Production;

/// <summary>
/// Loads the tablet-press visual and exposes its native-aligned mechanical anchors.
/// Station behavior remains authoritative; the GLB animation is reference motion only.
/// </summary>
internal sealed class ManualTabletPressAsset : IDisposable
{
    private const string ResourceName =
        "DrugExpansion.Assets.Models.manual_tablet_press.glb";

    private readonly EmbeddedGlbAsset _asset =
        new EmbeddedGlbAsset(ResourceName, "DrugExpansion_ManualTabletPress");

    internal ManualTabletPressRig CreateInstance(Transform parent)
    {
        if (parent == null)
            throw new ArgumentNullException(nameof(parent));

        GameObject source = _asset.GetOrLoad();
        GameObject instance = UnityEngine.Object.Instantiate(source);
        instance.name = "DrugExpansion_ManualTabletPress";
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        instance.SetActive(true);

        try
        {
            return ManualTabletPressRig.Resolve(instance);
        }
        catch
        {
            UnityEngine.Object.Destroy(instance);
            throw;
        }
    }

    public void Dispose()
    {
        _asset.Dispose();
    }
}

/// <summary>
/// Named visual assemblies and interaction anchors required by the tablet station.
/// Names deliberately parallel the native Brick Press contract without copying its
/// product conversion behavior.
/// </summary>
internal sealed class ManualTabletPressRig
{
    private ManualTabletPressRig(
        GameObject root,
        Transform handlePivot,
        Transform ramAssembly,
        Transform feedShoeAssembly,
        Transform feedPowderAssembly,
        Transform dieFillAssembly,
        Transform ejectorAssembly,
        Transform freshTabletAssembly,
        Transform handleClickableAnchor,
        Transform planeNormal,
        Transform handleRaised,
        Transform handleLowered,
        Transform pressRaised,
        Transform pressLowered,
        Transform mouldDetector,
        Transform cameraPouring,
        Transform cameraPressing,
        Transform standPoint,
        Transform containerSpawnPoint,
        Transform outputPoint)
    {
        Root = root;
        HandlePivot = handlePivot;
        RamAssembly = ramAssembly;
        FeedShoeAssembly = feedShoeAssembly;
        FeedPowderAssembly = feedPowderAssembly;
        DieFillAssembly = dieFillAssembly;
        EjectorAssembly = ejectorAssembly;
        FreshTabletAssembly = freshTabletAssembly;
        HandleClickableAnchor = handleClickableAnchor;
        PlaneNormal = planeNormal;
        HandleRaised = handleRaised;
        HandleLowered = handleLowered;
        PressRaised = pressRaised;
        PressLowered = pressLowered;
        MouldDetector = mouldDetector;
        CameraPouring = cameraPouring;
        CameraPressing = cameraPressing;
        StandPoint = standPoint;
        ContainerSpawnPoint = containerSpawnPoint;
        OutputPoint = outputPoint;
    }

    internal GameObject Root { get; }

    internal Transform HandlePivot { get; }

    internal Transform RamAssembly { get; }

    internal Transform FeedShoeAssembly { get; }

    internal Transform FeedPowderAssembly { get; }

    internal Transform DieFillAssembly { get; }

    internal Transform EjectorAssembly { get; }

    internal Transform FreshTabletAssembly { get; }

    internal Transform HandleClickableAnchor { get; }

    internal Transform PlaneNormal { get; }

    internal Transform HandleRaised { get; }

    internal Transform HandleLowered { get; }

    internal Transform PressRaised { get; }

    internal Transform PressLowered { get; }

    internal Transform MouldDetector { get; }

    internal Transform CameraPouring { get; }

    internal Transform CameraPressing { get; }

    internal Transform StandPoint { get; }

    internal Transform ContainerSpawnPoint { get; }

    internal Transform OutputPoint { get; }

    internal static ManualTabletPressRig Resolve(GameObject root)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));

        return new ManualTabletPressRig(
            root,
            FindRequired(root, "HandlePivot"),
            FindRequired(root, "RamAssembly"),
            FindRequired(root, "FeedShoeAssembly"),
            FindRequired(root, "FeedPowderAssembly"),
            FindRequired(root, "DieFillAssembly"),
            FindRequired(root, "EjectorAssembly"),
            FindRequired(root, "FreshTabletAssembly"),
            FindRequired(root, "HandleClickableAnchor"),
            FindRequired(root, "PlaneNormal"),
            FindRequired(root, "HandleRaised"),
            FindRequired(root, "HandleLowered"),
            FindRequired(root, "PressRaised"),
            FindRequired(root, "PressLowered"),
            FindRequired(root, "MouldDetector"),
            FindRequired(root, "CameraPouring"),
            FindRequired(root, "CameraPressing"),
            FindRequired(root, "StandPoint"),
            FindRequired(root, "ContainerSpawnPoint"),
            FindRequired(root, "OutputPoint"));
    }

    private static Transform FindRequired(GameObject root, string name)
    {
        foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(candidate.name, name, StringComparison.Ordinal))
                return candidate;
        }

        throw new InvalidOperationException(
            $"The tablet press model is missing required node '{name}'.");
    }
}
