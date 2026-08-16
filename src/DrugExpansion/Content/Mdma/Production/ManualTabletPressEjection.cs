namespace DrugExpansion.Content.Mdma.Production;

internal static class ManualTabletPressEjection
{
    internal static bool ShouldAnimate(
        int previousQuantity,
        int currentQuantity,
        bool stationInUse) =>
        previousQuantity >= 0 &&
        currentQuantity > previousQuantity &&
        stationInUse;

    internal static float Jitter(uint sequence, float amplitude) =>
        (Unit(sequence) * 2f - 1f) * amplitude;

    internal static float Unit(uint sequence)
    {
        uint value = sequence + 0x9E3779B9u;
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        value ^= value >> 16;
        return (value & 0x00FFFFFFu) / 16_777_215f;
    }
}
