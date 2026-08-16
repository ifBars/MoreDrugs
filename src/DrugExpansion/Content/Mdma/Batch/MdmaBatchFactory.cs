namespace DrugExpansion.Content.Mdma.Batch;

internal static class MdmaBatchFactory
{
    internal static MdmaBatchProfile CreateReaction(
        int ingredientQuality,
        double processControl,
        double contaminationPressure,
        Func<Guid>? batchIdFactory = null)
    {
        if (ingredientQuality is < 0 or > 4)
            throw new ArgumentOutOfRangeException(nameof(ingredientQuality));
        ValidateUnitInterval(processControl, nameof(processControl));
        ValidateUnitInterval(contaminationPressure, nameof(contaminationPressure));

        int purity = ClampScore(
            42d +
            ingredientQuality * 11d +
            processControl * 18d -
            contaminationPressure * 12d);
        int consistency = ClampScore(
            30d +
            ingredientQuality * 8d +
            processControl * 38d -
            contaminationPressure * 8d);
        int contamination = ClampScore(
            34d -
            ingredientQuality * 5d -
            processControl * 16d +
            contaminationPressure * 32d);

        Guid batchId = (batchIdFactory ?? Guid.NewGuid)();
        return new MdmaBatchProfile(
            batchId.ToString("N"),
            MdmaProductForm.Crystals,
            purity,
            consistency,
            contamination);
    }

    internal static MdmaBatchProfile CreateFallback(MdmaProductForm form)
    {
        const string fallbackBatchId = "00000000000000000000000000000000";
        return form == MdmaProductForm.Crystals
            ? new MdmaBatchProfile(
                fallbackBatchId,
                form,
                purity: 55,
                consistency: 50,
                contamination: 20)
            : new MdmaBatchProfile(
                fallbackBatchId,
                form,
                purity: 55,
                consistency: 50,
                contamination: 20,
                tabletColor: MdmaTabletColor.Pink,
                tabletImprint: MdmaTabletImprint.Heart);
    }

    private static int ClampScore(double value) =>
        Math.Clamp(
            (int)Math.Round(value, MidpointRounding.AwayFromZero),
            0,
            100);

    private static void ValidateUnitInterval(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value is < 0d or > 1d)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Value must be finite and between 0 and 1.");
        }
    }
}
