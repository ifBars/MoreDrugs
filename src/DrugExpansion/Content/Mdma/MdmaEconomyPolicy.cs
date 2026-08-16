using DrugExpansion.Content.Mdma.Production;

namespace DrugExpansion.Content.Mdma;

internal static class MdmaEconomyPolicy
{
    internal const float ProductPrice = 190f;

    internal const float SafroleLowPrice = 80f;

    internal const float SafroleStandardPrice = 120f;

    internal const float SafroleHighPrice = 165f;

    internal const float MethylaminePrice = 95f;

    internal const float AcidPriceReference = 45f;

    internal const float SupplierStartingOrderLimit = 400f;

    internal const float SupplierMaximumOrderLimit = 2_000f;

    internal const float TabletPressPrice = 5_000f;

    internal static float BatchRevenue =>
        ProductPrice *
        ManualTabletPressQuantities.ChemistryCrystalYield *
        ManualTabletPressQuantities.TabletsPerCycle;

    internal static float BatchInputCost(float safrolePrice) =>
        safrolePrice + MethylaminePrice + AcidPriceReference;

    internal static float BatchGrossProfit(float safrolePrice) =>
        BatchRevenue - BatchInputCost(safrolePrice);
}
