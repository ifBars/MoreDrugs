namespace DrugExpansion.Content.Mdma.Production;

internal static class ManualTabletPressWheel
{
    internal const float RequiredTurns = 3f;

    internal const float RequiredDegrees = RequiredTurns * 360f;

    internal static float AdvanceClockwise(
        float accumulatedDegrees,
        float previousPointerAngle,
        float currentPointerAngle)
    {
        float delta = NormalizeDelta(currentPointerAngle - previousPointerAngle);
        return Math.Clamp(
            accumulatedDegrees - delta,
            0f,
            RequiredDegrees);
    }

    internal static float ToProgress(float accumulatedDegrees) =>
        Math.Clamp(accumulatedDegrees / RequiredDegrees, 0f, 1f);

    private static float NormalizeDelta(float degrees)
    {
        degrees %= 360f;
        if (degrees > 180f)
            degrees -= 360f;
        else if (degrees < -180f)
            degrees += 360f;

        return degrees;
    }
}
