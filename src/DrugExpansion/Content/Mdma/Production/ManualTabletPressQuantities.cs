namespace DrugExpansion.Content.Mdma.Production;

internal static class ManualTabletPressQuantities
{
    internal const int ChemistryCrystalYield = 10;

    internal const int CrystalsPerCycle = 1;

    internal const int TabletsPerCycle = 1;
}

internal sealed class ManualTabletPressCompletionGate
{
    private bool _armed;

    internal void Arm()
    {
        _armed = true;
    }

    internal bool TryCommit()
    {
        if (!_armed)
            return false;

        _armed = false;
        return true;
    }
}
