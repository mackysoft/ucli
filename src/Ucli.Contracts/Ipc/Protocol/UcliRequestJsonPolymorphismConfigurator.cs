using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary>Configures the tagged unions that comprise the actual request serializer contract.</summary>
internal static class UcliRequestJsonPolymorphismConfigurator
{
    public static bool TryConfigure (JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type == typeof(UcliRequestStepJsonContract))
        {
            ConfigureRequestSteps(typeInfo);
            return true;
        }

        if (typeInfo.Type == typeof(UcliEditContextJsonContract))
        {
            ConfigureEditContexts(typeInfo);
            return true;
        }

        if (typeInfo.Type == typeof(UcliEditSelectionJsonContract))
        {
            ConfigureEditSelections(typeInfo);
            return true;
        }

        if (typeInfo.Type != typeof(UcliEditActionJsonContract))
        {
            return false;
        }

        ConfigureEditActions(typeInfo);
        return true;
    }

    private static void ConfigureRequestSteps (JsonTypeInfo typeInfo)
    {
        typeInfo.PolymorphismOptions = IpcJsonPolymorphismOptions.Create(
            new JsonDerivedType(
                typeof(UcliOperationRequestStepJsonContract),
                Vocabulary.GetText(IpcExecuteStepKind.Op)),
            new JsonDerivedType(
                typeof(UcliEditRequestStepJsonContract),
                Vocabulary.GetText(IpcExecuteStepKind.Edit)));
    }

    private static void ConfigureEditContexts (JsonTypeInfo typeInfo)
    {
        typeInfo.PolymorphismOptions = IpcJsonPolymorphismOptions.Create(
            new JsonDerivedType(
                typeof(UcliSceneEditContextJsonContract),
                Vocabulary.GetText(IpcEditStepContract.ContextKind.Scene)),
            new JsonDerivedType(
                typeof(UcliPrefabEditContextJsonContract),
                Vocabulary.GetText(IpcEditStepContract.ContextKind.Prefab)),
            new JsonDerivedType(
                typeof(UcliAssetEditContextJsonContract),
                Vocabulary.GetText(IpcEditStepContract.ContextKind.Asset)),
            new JsonDerivedType(
                typeof(UcliProjectEditContextJsonContract),
                Vocabulary.GetText(IpcEditStepContract.ContextKind.Project)));
    }

    private static void ConfigureEditSelections (JsonTypeInfo typeInfo)
    {
        typeInfo.PolymorphismOptions = IpcJsonPolymorphismOptions.Create(
            new JsonDerivedType(
                typeof(UcliGameObjectEditSelectionJsonContract),
                Vocabulary.GetText(UcliEditSelectionJsonKind.GameObject)),
            new JsonDerivedType(
                typeof(UcliSelfEditSelectionJsonContract),
                Vocabulary.GetText(UcliEditSelectionJsonKind.Self)),
            new JsonDerivedType(
                typeof(UcliProjectAssetEditSelectionJsonContract),
                Vocabulary.GetText(UcliEditSelectionJsonKind.ProjectAsset)),
            new JsonDerivedType(
                typeof(UcliFromEditSelectionJsonContract),
                Vocabulary.GetText(UcliEditSelectionJsonKind.From)));
    }

    private static void ConfigureEditActions (JsonTypeInfo typeInfo)
    {
        var options = IpcJsonPolymorphismOptions.Create();
        AddMutationActions(options);
        AddCreationAndPrefabActions(options);
        typeInfo.PolymorphismOptions = options;
    }

    private static void AddMutationActions (JsonPolymorphismOptions options)
    {
        options.DerivedTypes.Add(new JsonDerivedType(
            typeof(UcliSetEditActionJsonContract),
            Vocabulary.GetText(IpcEditStepContract.ActionKind.Set)));
        options.DerivedTypes.Add(new JsonDerivedType(
            typeof(UcliEnsureComponentEditActionJsonContract),
            Vocabulary.GetText(IpcEditStepContract.ActionKind.EnsureComponent)));
        options.DerivedTypes.Add(new JsonDerivedType(
            typeof(UcliDeleteEditActionJsonContract),
            Vocabulary.GetText(IpcEditStepContract.ActionKind.Delete)));
        options.DerivedTypes.Add(new JsonDerivedType(
            typeof(UcliReparentEditActionJsonContract),
            Vocabulary.GetText(IpcEditStepContract.ActionKind.Reparent)));
    }

    private static void AddCreationAndPrefabActions (JsonPolymorphismOptions options)
    {
        options.DerivedTypes.Add(new JsonDerivedType(
            typeof(UcliCreateObjectEditActionJsonContract),
            Vocabulary.GetText(IpcEditStepContract.ActionKind.CreateObject)));
        options.DerivedTypes.Add(new JsonDerivedType(
            typeof(UcliCreateAssetEditActionJsonContract),
            Vocabulary.GetText(IpcEditStepContract.ActionKind.CreateAsset)));
        options.DerivedTypes.Add(new JsonDerivedType(
            typeof(UcliCreatePrefabEditActionJsonContract),
            Vocabulary.GetText(IpcEditStepContract.ActionKind.CreatePrefab)));
        options.DerivedTypes.Add(new JsonDerivedType(
            typeof(UcliApplyPrefabOverridesEditActionJsonContract),
            Vocabulary.GetText(IpcEditStepContract.ActionKind.ApplyPrefabOverrides)));
        options.DerivedTypes.Add(new JsonDerivedType(
            typeof(UcliRevertPrefabOverridesEditActionJsonContract),
            Vocabulary.GetText(IpcEditStepContract.ActionKind.RevertPrefabOverrides)));
    }
}
