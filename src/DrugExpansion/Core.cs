using MelonLoader;
using DrugExpansion.Content;
using DrugExpansion.Content.Mdma;
using DrugExpansion.Content.Mdma.Progression;
using S1API.Lifecycle;
using S1API.Products;

[assembly: MelonInfo(typeof(DrugExpansion.Core), DrugExpansion.ModInfo.Name, DrugExpansion.ModInfo.Version, DrugExpansion.ModInfo.Author)]
[assembly: MelonGame(DrugExpansion.ModInfo.GameDeveloper, DrugExpansion.ModInfo.GameName)]

namespace DrugExpansion;

public sealed class Core : MelonMod
{
    private DrugCatalog? _catalog;

    public override void OnInitializeMelon()
    {
        _catalog = new DrugCatalog(LoggerInstance, new IDrugContentModule[]
        {
            new MdmaModule(LoggerInstance),
        });

        CustomProductSaveProviderRegistry.Register(_catalog);
        GameLifecycle.OnPreLoad += OnPreLoad;
        GameLifecycle.OnLoadComplete += OnLoadComplete;
        LoggerInstance.Msg($"{ModInfo.Name} {ModInfo.Version} initialized.");
    }

    public override void OnApplicationQuit()
    {
        GameLifecycle.OnPreLoad -= OnPreLoad;
        GameLifecycle.OnLoadComplete -= OnLoadComplete;
        _catalog?.Dispose();
        _catalog = null;
    }

    private void OnPreLoad()
    {
        MdmaProgressionSave.ResetForIncomingSave();
        _catalog?.RegisterContent();
    }

    private void OnLoadComplete()
    {
        _catalog?.CompleteLoad();
    }
}
