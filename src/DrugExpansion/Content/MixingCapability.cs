using S1API.Products;

namespace DrugExpansion.Content;

/// <summary>
/// Optional capability implemented by a drug module that participates in mixing.
/// </summary>
/// <remarks>
/// Logical product identity and mixability are intentionally independent. Implementations
/// may use a native execution strategy internally, but must not require the custom kind to
/// masquerade as a built-in Schedule I product family.
/// </remarks>
internal interface IMixingCapability
{
    void RegisterMixing(ProductKind productKind);
}

