namespace DrugExpansion.Content.Mdma.Precursors;

internal static class MdmaPrecursorIds
{
    internal const string DiscoDavey =
        "disco_davey";

    // This ID backed the released supplier's generated shop and delivery state.
    // Keep it exclusively for the opt-in S1API supplier persistence bridge.
    internal const string DiscoDaveyPersistentId =
        "ifbars.moredrugs:npcs/disco-davey";

    internal const string SafroleLow =
        "ifbars.moredrugs:ingredients/safrole-low";

    internal const string SafroleStandard =
        "ifbars.moredrugs:ingredients/safrole";

    internal const string SafroleHigh =
        "ifbars.moredrugs:ingredients/safrole-high";

    internal const string Methylamine =
        "ifbars.moredrugs:ingredients/methylamine";

    internal static readonly string[] SafroleOptions =
    {
        SafroleLow,
        SafroleStandard,
        SafroleHigh,
    };

    internal static readonly string[] SupplierItems =
    {
        SafroleLow,
        SafroleStandard,
        SafroleHigh,
        Methylamine,
    };
}
