#if IL2CPPMELON
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using S1AvatarFramework = Il2CppScheduleOne.AvatarFramework;
#elif MONOMELON
using S1AvatarFramework = ScheduleOne.AvatarFramework;
#endif

using MelonLoader;
using DrugExpansion.Infrastructure;
using S1API.Entities.Appearances.AccessoryFields;
using S1API.Rendering;
using UnityEngine;

namespace DrugExpansion.Content.Mdma.Precursors;

internal sealed class DiscoDaveyAccessoryCatalog : IDisposable
{
    internal const string RecoloredSneakersPath =
        "DrugExpansion/Avatar/Accessories/Feet/DiscoDaveySneakers";
    internal const string RaveJacketPath =
        "DrugExpansion/Avatar/Accessories/Chest/DiscoDaveyRaveJacket";
    internal const string FestivalSlingBagPath =
        "DrugExpansion/Avatar/Accessories/Chest/DiscoDaveyFestivalSlingBag";

    private const string ResourceName =
        "DrugExpansion.Assets.Models.disco_davey_accessories.glb";
    private const string SneakerTextureResourceName =
        "DrugExpansion.Assets.Textures.disco_davey_sneakers.png";
    private const string RaveJacketTextureResourceName =
        "DrugExpansion.Assets.Textures.disco_davey_rave_jacket.png";
    private const string BagMeshName = "FestivalSlingBag";
    private const int Spine2BoneIndexFallback = 3;
    private const float AvatarRigScaleCompensation = 0.01f;
    private const float AvatarHipHeight = 1.02f;
    private static readonly Color SneakerSoleColor =
        new Color32(105, 230, 24, 255);
    private static readonly string[] TexturePropertyNames =
        { "_MainTex", "_BaseMap", "_Albedo" };

    private readonly MelonLogger.Instance _logger;
    private readonly EmbeddedGlbAsset _asset =
        new(ResourceName, "DrugExpansion_DiscoDaveyAccessories");
    private readonly List<GameObject> _registeredPrefabs = new();
    private readonly List<Material> _ownedMaterials = new();
    private readonly List<Texture2D> _ownedTextures = new();

    internal DiscoDaveyAccessoryCatalog(MelonLogger.Instance logger)
    {
        _logger = logger;
    }

    internal static bool CustomSneakersAvailable { get; private set; }

    internal static bool CustomSlingBagAvailable { get; private set; }

    internal static bool CustomRaveJacketAvailable { get; private set; }

    internal bool AreRegistered { get; private set; }

    internal void Register()
    {
        if (CustomSneakersAvailable &&
            CustomRaveJacketAvailable &&
            CustomSlingBagAvailable)
        {
            return;
        }

        if (!CustomSneakersAvailable)
            CustomSneakersAvailable = TryRegisterSneakers();
        if (!CustomRaveJacketAvailable)
            CustomRaveJacketAvailable = TryRegisterRaveJacket();
        if (!CustomSlingBagAvailable)
            CustomSlingBagAvailable = TryRegisterSlingBag();
        AreRegistered =
            CustomSneakersAvailable ||
            CustomRaveJacketAvailable ||
            CustomSlingBagAvailable;

        if (AreRegistered)
        {
            _logger.Msg(
                "Registered Disco Davey's recolored native outfit and custom festival sling bag.");
        }
    }

    private bool TryRegisterSneakers()
    {
        try
        {
            GameObject shoes = CreateRecoloredNativeSneakers();
            if (!AccessoryFactory.RegisterAccessory(
                    RecoloredSneakersPath,
                    shoes))
            {
                UnityEngine.Object.Destroy(shoes);
                throw new InvalidOperationException(
                    "S1API rejected Disco Davey's sneaker resource path.");
            }

            _registeredPrefabs.Add(shoes);
            return true;
        }
        catch (Exception exception)
        {
            _logger.Warning(
                "Disco Davey's recolored native sneakers were unavailable; " +
                $"the ordinary native sneakers will be used: {exception.Message}");
            return false;
        }
    }

    private bool TryRegisterSlingBag()
    {
        try
        {
            GameObject source = _asset.GetOrLoad();
            GameObject bag = CreateRigidAccessory(
                Chest.CollarJacket,
                FestivalSlingBagPath,
                "Disco Davey Festival Sling Bag",
                source,
                new[]
                {
                    new RigidMeshSpec(BagMeshName, Spine2BoneIndexFallback),
                },
                reduceFootSize: false);
            if (!AccessoryFactory.RegisterAccessory(
                    FestivalSlingBagPath,
                    bag))
            {
                UnityEngine.Object.Destroy(bag);
                throw new InvalidOperationException(
                    "S1API rejected Disco Davey's sling-bag resource path.");
            }

            _registeredPrefabs.Add(bag);
            return true;
        }
        catch (Exception exception)
        {
            _logger.Warning(
                "Disco Davey's festival sling bag was unavailable; " +
                $"the rest of his outfit will still be used: {exception.Message}");
            return false;
        }
    }

    private bool TryRegisterRaveJacket()
    {
        try
        {
            Texture2D overlay =
                TextureUtils.LoadTextureFromResource(
                    typeof(DiscoDaveyAccessoryCatalog).Assembly,
                    RaveJacketTextureResourceName,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp) ??
                throw new InvalidOperationException(
                    "The embedded Disco Davey rave-jacket overlay could not be loaded.");
            _ownedTextures.Add(overlay);
            GameObject jacket =
                AccessoryFactory.CloneAccessoryWithCustomTextures(
                    Chest.CollarJacket,
                    "Disco Davey Rave Jacket",
                    targetResourcePath: RaveJacketPath) ??
                throw new InvalidOperationException(
                    "The native CollarJacket accessory could not be cloned.");
            jacket.SetActive(false);
            ApplyTemplateOverlay(
                jacket,
                overlay,
                "DiscoDavey_RaveJacket");

            S1AvatarFramework.Accessory accessory =
                jacket.GetComponent<S1AvatarFramework.Accessory>() ??
                throw new InvalidOperationException(
                    "The native CollarJacket scaffold has no Accessory component.");
            accessory.Name = "Disco Davey Rave Jacket";
            accessory.AssetPath = RaveJacketPath;
            accessory.ColorAllMeshes = false;
            ClearAccessoryColorTargets(accessory);
            foreach (Renderer renderer in
                     jacket.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null)
                        continue;

                    ApplyMaterialColor(material, Color.white);
                }
            }

            jacket.SetActive(true);
            if (!AccessoryFactory.RegisterAccessory(
                    RaveJacketPath,
                    jacket))
            {
                UnityEngine.Object.Destroy(jacket);
                throw new InvalidOperationException(
                    "S1API rejected Disco Davey's rave-jacket resource path.");
            }

            _registeredPrefabs.Add(jacket);
            return true;
        }
        catch (Exception exception)
        {
            _logger.Warning(
                "Disco Davey's recolored rave jacket was unavailable; " +
                $"the ordinary native jacket will be used: {exception.Message}");
            return false;
        }
    }

    public void Dispose()
    {
        foreach (GameObject prefab in _registeredPrefabs)
        {
            if (prefab != null)
                UnityEngine.Object.Destroy(prefab);
        }

        _registeredPrefabs.Clear();
        foreach (Material material in _ownedMaterials)
        {
            if (material != null)
                UnityEngine.Object.Destroy(material);
        }

        _ownedMaterials.Clear();
        foreach (Texture2D texture in _ownedTextures)
        {
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        }

        _ownedTextures.Clear();
        AreRegistered = false;
        CustomSneakersAvailable = false;
        CustomRaveJacketAvailable = false;
        CustomSlingBagAvailable = false;
        _asset.Dispose();
    }

    private GameObject CreateRecoloredNativeSneakers()
    {
        Texture2D upperOverlay =
            TextureUtils.LoadTextureFromResource(
                typeof(DiscoDaveyAccessoryCatalog).Assembly,
                SneakerTextureResourceName,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp) ??
            throw new InvalidOperationException(
                "The embedded Disco Davey sneaker overlay could not be loaded.");
        _ownedTextures.Add(upperOverlay);
        GameObject clone =
            AccessoryFactory.CloneAccessoryWithCustomTextures(
                Feet.Sneakers,
                "Disco Davey Sneakers",
                targetResourcePath: RecoloredSneakersPath) ??
            throw new InvalidOperationException(
                "The native Sneakers accessory could not be cloned.");
        clone.SetActive(false);

        S1AvatarFramework.Accessory accessory =
            clone.GetComponent<S1AvatarFramework.Accessory>() ??
            throw new InvalidOperationException(
                "The native Sneakers scaffold has no Accessory component.");
        accessory.Name = "Disco Davey Sneakers";
        accessory.AssetPath = RecoloredSneakersPath;
        accessory.ReduceFootSize = true;
        accessory.FootSizeReduction = 0.4f;
        accessory.ShouldBlockHair = false;
        accessory.ColorAllMeshes = false;
        ClearAccessoryColorTargets(accessory);

        var composites = new Dictionary<Texture, Texture2D>();
        foreach (Renderer renderer in
                 clone.GetComponentsInChildren<Renderer>(true))
        {
            Material[] sourceMaterials = renderer.sharedMaterials;
            if (sourceMaterials.Length == 0)
                continue;

            int soleIndex = FindSoleSubmeshIndex(renderer);
            var recoloredMaterials =
                new Material[sourceMaterials.Length];
            for (int index = 0; index < sourceMaterials.Length; index++)
            {
                Material sourceMaterial = sourceMaterials[index];
                if (sourceMaterial == null)
                    continue;

                var recolored = new Material(sourceMaterial)
                {
                    name =
                        $"DiscoDavey_{renderer.name}_{index}",
                };
                if (index == soleIndex)
                {
                    ApplyMaterialColor(
                        recolored,
                        SneakerSoleColor);
                }
                else
                {
                    ApplySneakerUpperMaterial(
                        recolored,
                        upperOverlay,
                        composites);
                }
                recoloredMaterials[index] = recolored;
                _ownedMaterials.Add(recolored);
            }

            renderer.sharedMaterials = recoloredMaterials;
        }

        _ownedTextures.AddRange(composites.Values);
        clone.SetActive(true);
        return clone;
    }

    private static int FindSoleSubmeshIndex(Renderer renderer)
    {
        if (renderer is not SkinnedMeshRenderer skinned ||
            skinned.sharedMesh == null ||
            skinned.sharedMesh.subMeshCount < 2)
        {
            return -1;
        }

        Mesh mesh = skinned.sharedMesh;
        var vertices = mesh.vertices;
        int selectedIndex = -1;
        float lowestMeanHeight = float.PositiveInfinity;
        int materialCount = renderer.sharedMaterials.Length;
        int submeshCount = Math.Min(mesh.subMeshCount, materialCount);
        for (int submeshIndex = 0;
             submeshIndex < submeshCount;
             submeshIndex++)
        {
            var indices = mesh.GetIndices(submeshIndex);
            if (indices.Length == 0)
                continue;

            double heightSum = 0.0;
            for (int index = 0; index < indices.Length; index++)
                heightSum += vertices[indices[index]].y;

            float meanHeight =
                (float)(heightSum / indices.Length);
            if (meanHeight < lowestMeanHeight)
            {
                lowestMeanHeight = meanHeight;
                selectedIndex = submeshIndex;
            }
        }

        return selectedIndex;
    }

    private static void ApplyMaterialColor(
        Material material,
        Color color)
    {
        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private static void ApplySneakerUpperMaterial(
        Material material,
        Texture2D overlay,
        IDictionary<Texture, Texture2D> composites)
    {
        Texture sourceTexture =
            GetBaseColorTexture(material) ??
            throw new InvalidOperationException(
                $"Native sneaker material '{material.name}' has no base-color texture.");
        if (!composites.TryGetValue(
                sourceTexture,
                out Texture2D? composite) ||
            composite == null)
        {
            composite = CreateTemplateComposite(
                sourceTexture,
                overlay,
                $"DiscoDavey_Sneaker_{composites.Count}");
            composites.Add(sourceTexture, composite);
        }

        ApplyMaterialColor(material, Color.white);
        ApplyBaseColorTexture(material, composite);
    }

    private void ApplyTemplateOverlay(
        GameObject accessory,
        Texture2D overlay,
        string textureName)
    {
        var composites = new Dictionary<Texture, Texture2D>();
        foreach (Renderer renderer in
                 accessory.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null)
                    continue;

                Texture? sourceTexture = GetBaseColorTexture(material);
                if (sourceTexture == null)
                    continue;

                if (!composites.TryGetValue(
                        sourceTexture,
                        out Texture2D? composite) ||
                    composite == null)
                {
                    composite = CreateTemplateComposite(
                        sourceTexture,
                        overlay,
                        $"{textureName}_{composites.Count}");
                    composites.Add(sourceTexture, composite);
                    _ownedTextures.Add(composite);
                }

                ApplyMaterialColor(material, Color.white);
                ApplyBaseColorTexture(material, composite);
            }
        }

        if (composites.Count == 0)
        {
            throw new InvalidOperationException(
                $"The native accessory for '{textureName}' has no base-color texture.");
        }
    }

    private static Texture2D CreateTemplateComposite(
        Texture nativeTexture,
        Texture2D overlay,
        string textureName)
    {
        Texture2D readableNative =
            CreateReadableCopy(nativeTexture, textureName);
        Texture2D effectiveOverlay = overlay;
        bool destroyEffectiveOverlay = false;
        try
        {
            if (readableNative.width != overlay.width ||
                readableNative.height != overlay.height)
            {
                effectiveOverlay = CreateReadableCopy(
                    overlay,
                    $"{textureName}_Overlay",
                    readableNative.width,
                    readableNative.height);
                destroyEffectiveOverlay = true;
            }

            Color32[] nativePixels = readableNative.GetPixels32();
            Color32[] overlayPixels = effectiveOverlay.GetPixels32();
            for (int index = 0; index < nativePixels.Length; index++)
            {
                byte alpha = overlayPixels[index].a;
                if (alpha == 0)
                    continue;
                if (alpha == byte.MaxValue)
                {
                    nativePixels[index] = overlayPixels[index];
                    continue;
                }

                nativePixels[index] = Color32.Lerp(
                    nativePixels[index],
                    overlayPixels[index],
                    alpha / 255f);
            }

            var composite = new Texture2D(
                readableNative.width,
                readableNative.height,
                TextureFormat.RGBA32,
                mipChain: true)
            {
                name = textureName,
                filterMode = nativeTexture.filterMode,
                wrapMode = nativeTexture.wrapMode,
            };
            composite.SetPixels32(nativePixels);
            composite.Apply(
                updateMipmaps: true,
                makeNoLongerReadable: true);
            return composite;
        }
        finally
        {
            UnityEngine.Object.Destroy(readableNative);
            if (destroyEffectiveOverlay)
                UnityEngine.Object.Destroy(effectiveOverlay);
        }
    }

    private static Texture2D CreateReadableCopy(
        Texture source,
        string textureName,
        int? targetWidth = null,
        int? targetHeight = null)
    {
        int width = targetWidth ?? source.width;
        int height = targetHeight ?? source.height;
        RenderTexture temporary = RenderTexture.GetTemporary(
            width,
            height,
            depthBuffer: 0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Default);
        RenderTexture? previous = RenderTexture.active;
        try
        {
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;
            var readable = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                mipChain: false)
            {
                name = $"{textureName}_NativeTemplate",
            };
            readable.ReadPixels(
                new Rect(0f, 0f, width, height),
                0,
                0,
                recalculateMipMaps: false);
            readable.Apply(
                updateMipmaps: false,
                makeNoLongerReadable: false);
            return readable;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
        }
    }

    private static Texture? GetBaseColorTexture(Material material)
    {
        foreach (string propertyName in TexturePropertyNames)
        {
            if (!material.HasProperty(propertyName))
                continue;

            Texture texture = material.GetTexture(propertyName);
            if (texture != null)
                return texture;
        }

        return null;
    }

    private static void ApplyBaseColorTexture(
        Material material,
        Texture texture)
    {
        foreach (string propertyName in TexturePropertyNames)
        {
            if (material.HasProperty(propertyName))
                material.SetTexture(propertyName, texture);
        }
    }

    private static void ClearAccessoryColorTargets(
        S1AvatarFramework.Accessory accessory)
    {
#if IL2CPPMELON
        accessory.meshesToColor =
            new Il2CppReferenceArray<MeshRenderer>(0);
        accessory.skinnedMeshesToColor =
            new Il2CppReferenceArray<SkinnedMeshRenderer>(0);
#else
        accessory.meshesToColor = Array.Empty<MeshRenderer>();
        accessory.skinnedMeshesToColor =
            Array.Empty<SkinnedMeshRenderer>();
#endif
    }

    private static GameObject CreateRigidAccessory(
        string sourceResourcePath,
        string targetResourcePath,
        string name,
        GameObject visualSource,
        IReadOnlyList<RigidMeshSpec> meshes,
        bool reduceFootSize)
    {
        GameObject clone =
            AccessoryFactory.CloneAccessoryWithCustomTextures(
                sourceResourcePath,
                name,
                targetResourcePath: targetResourcePath) ??
            throw new InvalidOperationException(
                $"The native accessory scaffold '{sourceResourcePath}' could not be cloned.");
        clone.SetActive(false);

        S1AvatarFramework.Accessory accessory =
            clone.GetComponent<S1AvatarFramework.Accessory>() ??
            throw new InvalidOperationException(
                $"The accessory scaffold '{sourceResourcePath}' has no Accessory component.");
        SkinnedMeshRenderer templateRenderer =
            FindTemplateRenderer(clone);
        DisableNativeVisuals(clone);
        var customRenderers = new List<MeshRenderer>(meshes.Count);
        foreach (RigidMeshSpec spec in meshes)
        {
            MeshFilter sourceFilter =
                FindSourceMesh(visualSource, spec.MeshName);
            customRenderers.Add(
                CreateRigidRenderer(
                    clone.transform,
                    visualSource.transform,
                    sourceFilter));
        }

        accessory.Name = name;
        accessory.AssetPath = targetResourcePath;
        accessory.ReduceFootSize = reduceFootSize;
        accessory.FootSizeReduction = reduceFootSize ? 0.4f : 1f;
        accessory.ShouldBlockHair = false;
        accessory.ColorAllMeshes = false;
        SetAccessoryRendererArrays(accessory, customRenderers);
        clone.SetActive(true);
        return clone;
    }

    private static SkinnedMeshRenderer FindTemplateRenderer(
        GameObject scaffold)
    {
        SkinnedMeshRenderer[] renderers =
            scaffold.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        return renderers.FirstOrDefault(
                   renderer =>
                       renderer != null &&
                       renderer.sharedMesh != null &&
                       renderer.name.Contains(
                           "LOD0",
                           StringComparison.OrdinalIgnoreCase)) ??
               renderers.FirstOrDefault(
                   renderer =>
                       renderer != null &&
                       renderer.sharedMesh != null) ??
               throw new InvalidOperationException(
                   $"Accessory scaffold '{scaffold.name}' has no skinned renderer.");
    }

    private static MeshFilter FindSourceMesh(
        GameObject source,
        string meshName)
    {
        return source.GetComponentsInChildren<MeshFilter>(true)
                   .FirstOrDefault(
                       filter =>
                           filter != null &&
                           filter.sharedMesh != null &&
                           string.Equals(
                               filter.name,
                               meshName,
                               StringComparison.Ordinal)) ??
               throw new InvalidOperationException(
                $"The Disco Davey GLB is missing mesh '{meshName}'.");
    }

    private static void DisableNativeVisuals(GameObject scaffold)
    {
        foreach (Renderer renderer in
                 scaffold.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = false;
        }

        foreach (LODGroup group in
                 scaffold.GetComponentsInChildren<LODGroup>(true))
        {
            group.enabled = false;
        }
    }

    private static MeshRenderer CreateRigidRenderer(
        Transform accessoryRoot,
        Transform visualRoot,
        MeshFilter sourceFilter)
    {
        Mesh sourceMesh = sourceFilter.sharedMesh;
        Mesh mesh = UnityEngine.Object.Instantiate(sourceMesh);
        mesh.name = $"{sourceFilter.name}_Rigid";

        Matrix4x4 sourceToRoot =
            visualRoot.worldToLocalMatrix *
            sourceFilter.transform.localToWorldMatrix;
        Vector3[] vertices = mesh.vertices;
        for (int index = 0; index < vertices.Length; index++)
        {
            vertices[index] =
                sourceToRoot.MultiplyPoint3x4(vertices[index]);
        }

        Vector3[] normals = mesh.normals;
        if (normals.Length == vertices.Length)
        {
            Matrix4x4 normalMatrix = sourceToRoot.inverse.transpose;
            for (int index = 0; index < normals.Length; index++)
            {
                normals[index] =
                    normalMatrix.MultiplyVector(normals[index]).normalized;
            }

            mesh.normals = normals;
        }

        mesh.vertices = vertices;
        mesh.RecalculateBounds();

        var rendererRoot =
            new GameObject($"{sourceFilter.name}_Static");
        rendererRoot.transform.SetParent(accessoryRoot, false);
        rendererRoot.transform.localScale =
            Vector3.one * AvatarRigScaleCompensation;
        rendererRoot.transform.localPosition =
            Vector3.down *
            AvatarHipHeight *
            AvatarRigScaleCompensation;
        rendererRoot.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = rendererRoot.AddComponent<MeshRenderer>();
        renderer.sharedMaterials =
            sourceFilter.GetComponent<MeshRenderer>()?.sharedMaterials ??
            Array.Empty<Material>();
        return renderer;
    }

    private static void SetAccessoryRendererArrays(
        S1AvatarFramework.Accessory accessory,
        List<MeshRenderer> renderers)
    {
#if IL2CPPMELON
        accessory.meshesToColor =
            new Il2CppReferenceArray<MeshRenderer>(
                renderers.ToArray());
        accessory.skinnedMeshesToColor =
            new Il2CppReferenceArray<SkinnedMeshRenderer>(0);
        accessory.skinnedMeshesToBind =
            new Il2CppReferenceArray<SkinnedMeshRenderer>(0);
        accessory.shapeKeyMeshRends =
            new Il2CppReferenceArray<SkinnedMeshRenderer>(0);
#else
        accessory.meshesToColor = renderers.ToArray();
        accessory.skinnedMeshesToColor =
            Array.Empty<SkinnedMeshRenderer>();
        accessory.skinnedMeshesToBind =
            Array.Empty<SkinnedMeshRenderer>();
        accessory.shapeKeyMeshRends =
            Array.Empty<SkinnedMeshRenderer>();
#endif
    }

    private readonly struct RigidMeshSpec
    {
        internal RigidMeshSpec(
            string meshName,
            int boneIndex)
        {
            MeshName = meshName;
            BoneIndex = boneIndex;
        }

        internal string MeshName { get; }

        internal int BoneIndex { get; }
    }
}
