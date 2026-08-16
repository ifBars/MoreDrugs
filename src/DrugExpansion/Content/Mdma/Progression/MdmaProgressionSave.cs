using DrugExpansion.Content.Mdma;
using S1API.Internal.Abstraction;
using S1API.Products;
using S1API.Saveables;

namespace DrugExpansion.Content.Mdma.Progression;

public sealed class MdmaProgressionSave : Saveable
{
    [SaveableField("mdma_progression")]
    private MdmaProgressionData? _data;

    private static MdmaProgressionSave? _current;

    internal static event Action<MdmaProgressionData>? Loaded;

    public MdmaProgressionSave()
    {
        _current = this;
    }

    internal static bool IsLoaded { get; private set; }

    internal static MdmaProgressionData? Current =>
        IsLoaded ? _current?._data : null;

    internal static void ResetForIncomingSave()
    {
        IsLoaded = false;
        if (_current != null)
            _current._data = null;
    }

    internal static bool MarkFirstTabletPressed()
    {
        if (!IsLoaded || _current == null)
            return false;

        _current._data ??= new MdmaProgressionData();
        _current._data.SchemaVersion =
            MdmaProgressionPolicy.CurrentSchemaVersion;
        if (_current._data.HasPressedFirstTablet)
            return false;

        _current._data.HasPressedFirstTablet = true;
        return true;
    }

    internal static void ReplayLoadedState(
        Action<MdmaProgressionData> apply)
    {
        if (Current != null)
            apply(Current);
    }

    internal static void EnsureInitialized()
    {
        if (IsLoaded)
            return;

        _current ??= new MdmaProgressionSave();
        _current.InitializeState(hadProgressionRecord: false);
    }

    protected override void OnCreated()
    {
        InitializeState(hadProgressionRecord: false);
    }

    protected override void OnLoaded()
    {
        bool hadProgressionRecord = _data != null;
        InitializeState(hadProgressionRecord);
    }

    private void InitializeState(bool hadProgressionRecord)
    {
        bool discovered = ProductManager.DiscoveredProducts.Any(
            product => string.Equals(
                product.ID,
                MdmaProductIds.Tablets,
                StringComparison.OrdinalIgnoreCase));
        bool listed = ProductManager.ListedProducts.Any(
            product => string.Equals(
                product.ID,
                MdmaProductIds.Tablets,
                StringComparison.OrdinalIgnoreCase));

        _data = MdmaProgressionPolicy.Initialize(
            _data,
            hadProgressionRecord,
            discovered,
            listed);

        IsLoaded = true;
        Loaded?.Invoke(_data);
    }
}
