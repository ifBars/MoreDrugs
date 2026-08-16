namespace DrugExpansion.Content.Mdma;

internal static class MdmaProductIds
{
    internal const string Tablets = "ifbars.moredrugs:products/mdma";
    internal const string Crystals = "ifbars.moredrugs:products/mdma-crystals";

    internal static bool TryGetForm(
        string? productId,
        out Batch.MdmaProductForm form)
    {
        if (string.Equals(productId, Tablets, StringComparison.OrdinalIgnoreCase))
        {
            form = Batch.MdmaProductForm.Tablet;
            return true;
        }

        if (string.Equals(productId, Crystals, StringComparison.OrdinalIgnoreCase))
        {
            form = Batch.MdmaProductForm.Crystals;
            return true;
        }

        form = default;
        return false;
    }
}
