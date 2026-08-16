using System.Globalization;

namespace DrugExpansion.Content.Mdma.Batch;

internal sealed class MdmaBatchProfile : IEquatable<MdmaBatchProfile>
{
    internal const int MaximumBrandLength = 24;

    internal MdmaBatchProfile(
        string batchId,
        MdmaProductForm form,
        int purity,
        int consistency,
        int contamination,
        MdmaTestStatus testStatus = MdmaTestStatus.Untested,
        MdmaTabletColor tabletColor = MdmaTabletColor.None,
        MdmaTabletImprint tabletImprint = MdmaTabletImprint.None,
        string brandName = "")
    {
        BatchId = NormalizeBatchId(batchId);
        Form = ValidateEnum(form, nameof(form));
        Purity = ValidateScore(purity, nameof(purity));
        Consistency = ValidateScore(consistency, nameof(consistency));
        Contamination = ValidateScore(contamination, nameof(contamination));
        TestStatus = ValidateEnum(testStatus, nameof(testStatus));

        if (form == MdmaProductForm.Crystals)
        {
            if (tabletColor != MdmaTabletColor.None ||
                tabletImprint != MdmaTabletImprint.None ||
                !string.IsNullOrWhiteSpace(brandName))
            {
                throw new ArgumentException(
                    "Crystal batches cannot carry tablet presentation or branding.");
            }

            TabletColor = MdmaTabletColor.None;
            TabletImprint = MdmaTabletImprint.None;
            BrandName = string.Empty;
            return;
        }

        TabletColor = ValidateTabletSelection(tabletColor, nameof(tabletColor));
        TabletImprint = ValidateTabletSelection(tabletImprint, nameof(tabletImprint));
        BrandName = NormalizeBrandName(brandName);
    }

    internal string BatchId { get; }

    internal MdmaProductForm Form { get; }

    internal int Purity { get; }

    internal int Consistency { get; }

    internal int Contamination { get; }

    internal int Safety => 100 - Contamination;

    internal MdmaTestStatus TestStatus { get; }

    internal MdmaTabletColor TabletColor { get; }

    internal MdmaTabletImprint TabletImprint { get; }

    internal string BrandName { get; }

    internal MdmaBatchRisk Risk =>
        Contamination >= 40
            ? MdmaBatchRisk.Critical
            : Contamination >= 20
                ? MdmaBatchRisk.Elevated
                : MdmaBatchRisk.Controlled;

    internal int MarketScore =>
        (int)Math.Round(
            Purity * 0.45d +
            Consistency * 0.25d +
            Safety * 0.30d,
            MidpointRounding.AwayFromZero);

    internal MdmaBatchProfile WithTestStatus(MdmaTestStatus testStatus) =>
        new(
            BatchId,
            Form,
            Purity,
            Consistency,
            Contamination,
            testStatus,
            TabletColor,
            TabletImprint,
            BrandName);

    internal MdmaBatchProfile Press(
        MdmaTabletColor tabletColor,
        MdmaTabletImprint tabletImprint,
        string brandName) =>
        Form != MdmaProductForm.Crystals
            ? throw new InvalidOperationException("Only crystal batches can be pressed.")
            : new MdmaBatchProfile(
                BatchId,
                MdmaProductForm.Tablet,
                Purity,
                Consistency,
                Contamination,
                TestStatus,
                tabletColor,
                tabletImprint,
                brandName);

    public bool Equals(MdmaBatchProfile? other)
    {
        return other != null &&
               StringComparer.Ordinal.Equals(BatchId, other.BatchId) &&
               Form == other.Form &&
               Purity == other.Purity &&
               Consistency == other.Consistency &&
               Contamination == other.Contamination &&
               TestStatus == other.TestStatus &&
               TabletColor == other.TabletColor &&
               TabletImprint == other.TabletImprint &&
               StringComparer.Ordinal.Equals(BrandName, other.BrandName);
    }

    public override bool Equals(object? obj) => Equals(obj as MdmaBatchProfile);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(BatchId, StringComparer.Ordinal);
        hash.Add(Form);
        hash.Add(Purity);
        hash.Add(Consistency);
        hash.Add(Contamination);
        hash.Add(TestStatus);
        hash.Add(TabletColor);
        hash.Add(TabletImprint);
        hash.Add(BrandName, StringComparer.Ordinal);
        return hash.ToHashCode();
    }

    private static int ValidateScore(int value, string parameterName)
    {
        if (value is < 0 or > 100)
            throw new ArgumentOutOfRangeException(parameterName, value, "Score must be between 0 and 100.");

        return value;
    }

    private static T ValidateEnum<T>(T value, string parameterName)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(typeof(T), value))
            throw new ArgumentOutOfRangeException(parameterName, value, "Value is not defined.");

        return value;
    }

    private static T ValidateTabletSelection<T>(T value, string parameterName)
        where T : struct, Enum
    {
        ValidateEnum(value, parameterName);
        if (Convert.ToInt32(value, CultureInfo.InvariantCulture) == 0)
            throw new ArgumentOutOfRangeException(parameterName, value, "A tablet selection is required.");

        return value;
    }

    private static string NormalizeBatchId(string value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        string normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length != 32 || !normalized.All(IsLowerHex))
        {
            throw new ArgumentException(
                "Batch ID must contain exactly 32 lowercase hexadecimal characters.",
                nameof(value));
        }

        return normalized;
    }

    private static bool IsLowerHex(char value) =>
        value is >= '0' and <= '9' or >= 'a' and <= 'f';

    private static string NormalizeBrandName(string value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        string normalized = value.Trim();
        if (normalized.Length > MaximumBrandLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"Brand name cannot exceed {MaximumBrandLength} characters.");
        }

        if (normalized.Any(char.IsControl))
            throw new ArgumentException("Brand name cannot contain control characters.", nameof(value));

        return normalized;
    }
}
