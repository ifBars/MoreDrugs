namespace DrugExpansion.Content.Mdma.Progression;

internal static class MdmaProgressionPolicy
{
    internal const int CurrentSchemaVersion = 1;

    internal static MdmaProgressionData Initialize(
        MdmaProgressionData? data,
        bool hadProgressionRecord,
        bool isDiscovered,
        bool isListed)
    {
        data ??= new MdmaProgressionData();
        data.SchemaVersion = CurrentSchemaVersion;
        data.LegacyDiscoveryStatePreserved |= ShouldPreserveLegacyState(
            hadProgressionRecord,
            isDiscovered,
            isListed);
        return data;
    }

    internal static bool ShouldDiscover(bool hasPressedFirstTablet) =>
        hasPressedFirstTablet;

    internal static bool ShouldPreserveLegacyState(
        bool hasProgressionRecord,
        bool isDiscovered,
        bool isListed) =>
        !hasProgressionRecord && (isDiscovered || isListed);
}

public sealed class MdmaProgressionData
{
    public int SchemaVersion { get; set; } =
        MdmaProgressionPolicy.CurrentSchemaVersion;

    public bool HasPressedFirstTablet { get; set; }

    public bool LegacyDiscoveryStatePreserved { get; set; }
}
