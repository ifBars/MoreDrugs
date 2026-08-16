namespace DrugExpansion.Content.Mdma.Batch;

internal static class MdmaTabletPressConversion
{
    internal const int MinimumQuality = 0;
    internal const int MaximumQuality = 4;

    internal static MdmaPressedTabletBatch Convert(
        MdmaBatchProfile crystals,
        int crystalQuality)
    {
        if (crystals == null)
            throw new ArgumentNullException(nameof(crystals));
        if (crystalQuality is < MinimumQuality or > MaximumQuality)
        {
            throw new ArgumentOutOfRangeException(
                nameof(crystalQuality),
                crystalQuality,
                "Crystal quality must be a defined native quality tier.");
        }

        return new MdmaPressedTabletBatch(
            crystals.Press(
                MdmaTabletColor.Pink,
                MdmaTabletImprint.Heart,
                string.Empty),
            crystalQuality);
    }
}

internal sealed class MdmaPressedTabletBatch
{
    internal MdmaPressedTabletBatch(
        MdmaBatchProfile profile,
        int quality)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        Quality = quality;
    }

    internal MdmaBatchProfile Profile { get; }

    internal int Quality { get; }
}
