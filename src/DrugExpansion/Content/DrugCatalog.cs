using MelonLoader;
using S1API.Products;

namespace DrugExpansion.Content;

internal sealed class DrugCatalog : ICustomProductSaveProvider, IDisposable
{
    internal const string SaveProviderId = "ifbars.moredrugs:products";
    internal const int SaveProviderVersion = 2;

    private readonly MelonLogger.Instance _logger;
    private readonly IReadOnlyList<IDrugContentModule> _modules;
    private readonly IReadOnlyDictionary<string, IDrugContentModule> _modulesByProviderData;

    internal DrugCatalog(
        MelonLogger.Instance logger,
        IReadOnlyList<IDrugContentModule> modules)
    {
        _logger = logger;
        _modules = modules;
        _modulesByProviderData = modules.ToDictionary(
            module => module.ProviderDataKey,
            StringComparer.OrdinalIgnoreCase);
    }

    public string ProviderId => SaveProviderId;

    public int MaximumDescriptorVersion => SaveProviderVersion;

    internal void RegisterContent()
    {
        foreach (IDrugContentModule module in _modules)
        {
            try
            {
                module.RegisterContent();
            }
            catch (Exception exception)
            {
                _logger.Error(
                    $"Failed to register custom drug module '{module.ProviderDataKey}': {exception}");
            }
        }
    }

    internal void CompleteLoad()
    {
        foreach (IDrugContentModule module in _modules)
        {
            try
            {
                module.CompleteLoad();
            }
            catch (Exception exception)
            {
                _logger.Error(
                    $"Failed to finish custom drug module '{module.ProviderDataKey}': {exception}");
            }
        }
    }

    public CustomProductDefinitionBuilder? Restore(CustomProductSaveDescriptor descriptor)
    {
        if (descriptor.ProviderVersion is < 1 or > SaveProviderVersion)
            return null;

        return _modulesByProviderData.TryGetValue(
            descriptor.ProviderData,
            out IDrugContentModule? module)
            ? module.Restore(descriptor)
            : null;
    }

    public void Dispose()
    {
        foreach (IDrugContentModule module in _modules)
            module.Dispose();
    }
}
