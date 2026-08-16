using System.Globalization;
using System.Text;

namespace DrugExpansion.Content.Mdma.Batch;

internal static class MdmaBatchCodec
{
    private const int CurrentVersion = 1;
    private const int FieldCount = 10;
    private const int MaximumPayloadLength = 256;

    internal static string Encode(MdmaBatchProfile profile)
    {
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));

        string payload = string.Join(
            "|",
            CurrentVersion.ToString(CultureInfo.InvariantCulture),
            profile.BatchId,
            ((int)profile.Form).ToString(CultureInfo.InvariantCulture),
            profile.Purity.ToString(CultureInfo.InvariantCulture),
            profile.Consistency.ToString(CultureInfo.InvariantCulture),
            profile.Contamination.ToString(CultureInfo.InvariantCulture),
            ((int)profile.TestStatus).ToString(CultureInfo.InvariantCulture),
            ((int)profile.TabletColor).ToString(CultureInfo.InvariantCulture),
            ((int)profile.TabletImprint).ToString(CultureInfo.InvariantCulture),
            EncodeText(profile.BrandName));

        if (payload.Length > MaximumPayloadLength)
            throw new InvalidOperationException("Encoded MDMA batch payload exceeds its bound.");

        return payload;
    }

    internal static bool TryDecode(string? payload, out MdmaBatchProfile? profile)
    {
        profile = null;
        if (string.IsNullOrEmpty(payload) || payload.Length > MaximumPayloadLength)
            return false;

        string[] fields = payload.Split('|');
        if (fields.Length != FieldCount ||
            !TryParseInt(fields[0], out int version) ||
            version != CurrentVersion ||
            !TryParseEnum(fields[2], out MdmaProductForm form) ||
            !TryParseScore(fields[3], out int purity) ||
            !TryParseScore(fields[4], out int consistency) ||
            !TryParseScore(fields[5], out int contamination) ||
            !TryParseEnum(fields[6], out MdmaTestStatus testStatus) ||
            !TryParseEnum(fields[7], out MdmaTabletColor tabletColor) ||
            !TryParseEnum(fields[8], out MdmaTabletImprint tabletImprint) ||
            !TryDecodeText(fields[9], out string brandName))
        {
            return false;
        }

        try
        {
            profile = new MdmaBatchProfile(
                fields[1],
                form,
                purity,
                consistency,
                contamination,
                testStatus,
                tabletColor,
                tabletImprint,
                brandName);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string EncodeText(string value)
    {
        if (value.Length == 0)
            return string.Empty;

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static bool TryDecodeText(string value, out string result)
    {
        result = string.Empty;
        if (value.Length == 0)
            return true;

        try
        {
            string base64 = value.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight((base64.Length + 3) / 4 * 4, '=');
            result = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(base64));
            return true;
        }
        catch (Exception exception) when (
            exception is FormatException or DecoderFallbackException)
        {
            return false;
        }
    }

    private static bool TryParseScore(string value, out int result) =>
        TryParseInt(value, out result) && result is >= 0 and <= 100;

    private static bool TryParseInt(string value, out int result) =>
        int.TryParse(
            value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out result);

    private static bool TryParseEnum<T>(string value, out T result)
        where T : struct, Enum
    {
        result = default;
        if (!TryParseInt(value, out int numeric) ||
            !Enum.IsDefined(typeof(T), numeric))
        {
            return false;
        }

        result = (T)Enum.ToObject(typeof(T), numeric);
        return true;
    }
}
