using UnityEngine;

namespace DrugExpansion.Content.Mdma;

internal static class RuntimePrefabCache
{
    private static GameObject? _root;

    internal static void Store(GameObject prefab)
    {
        GameObject root = GetOrCreateRoot();
        prefab.hideFlags = HideFlags.HideAndDontSave;
        prefab.transform.SetParent(root.transform, false);
        prefab.transform.localPosition = Vector3.zero;

        // Keep activeSelf true so clones enter gameplay active. The inactive cache
        // parent prevents prefab-only physics and drag components from ticking.
        prefab.SetActive(true);
    }

    private static GameObject GetOrCreateRoot()
    {
        if (_root != null)
            return _root;

        _root = new GameObject("DrugExpansion_RuntimePrefabCache")
        {
            hideFlags = HideFlags.HideAndDontSave,
        };
        _root.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(_root);
        return _root;
    }
}
