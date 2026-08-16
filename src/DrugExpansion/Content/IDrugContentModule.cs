using S1API.Products;

namespace DrugExpansion.Content;

/// <summary>
/// One independently registered custom drug kind and its products.
/// </summary>
/// <remarks>
/// A module owns its durable IDs, persistence, visuals, packaging, production, and
/// optional mixing strategy. The catalog does not derive capabilities from a vanilla
/// drug enum, so a future entirely custom kind can opt into mixing on its own terms.
/// </remarks>
internal interface IDrugContentModule : IDisposable
{
    string ProviderDataKey { get; }

    void RegisterContent();

    void CompleteLoad();

    CustomProductDefinitionBuilder? Restore(CustomProductSaveDescriptor descriptor);
}

