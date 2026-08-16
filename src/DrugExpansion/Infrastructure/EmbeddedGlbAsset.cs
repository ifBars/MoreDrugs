using System.Reflection;
using S1MAPI.Gltf;
using S1MAPI.Utils;
using UnityEngine;

namespace DrugExpansion.Infrastructure;

internal sealed class EmbeddedGlbAsset : IDisposable
{
    private readonly string _resourceName;
    private readonly string _sourceName;
    private GameObject? _sourceRoot;
    private GameObject? _source;

    internal EmbeddedGlbAsset(string resourceName, string sourceName)
    {
        _resourceName = resourceName;
        _sourceName = sourceName;
    }

    internal GameObject GetOrLoad()
    {
        if (_source != null)
            return _source;

        byte[] bytes =
            EmbeddedResourceLoader.LoadBytes(
                _resourceName,
                typeof(EmbeddedGlbAsset).Assembly) ??
            throw new InvalidOperationException(
                $"Embedded GLB resource '{_resourceName}' was not found.");

        _sourceRoot = new GameObject($"{_sourceName}_Sources");
        _sourceRoot.transform.position = new Vector3(0f, -20000f, 0f);
        UnityEngine.Object.DontDestroyOnLoad(_sourceRoot);

        _source = GltfLoader.LoadGlb(bytes) ??
            throw new InvalidOperationException(
                $"MAPI could not load embedded GLB resource '{_resourceName}'.");
        _source.name = _sourceName;
        _source.transform.SetParent(_sourceRoot.transform, false);
        _source.SetActive(true);

        foreach (MeshFilter filter in _source.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh != null &&
                (mesh.tangents == null || mesh.tangents.Length != mesh.vertexCount))
            {
                mesh.RecalculateTangents();
            }
        }

        return _source;
    }

    public void Dispose()
    {
        if (_sourceRoot != null)
            UnityEngine.Object.Destroy(_sourceRoot);

        _source = null;
        _sourceRoot = null;
    }
}

