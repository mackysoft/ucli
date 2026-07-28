using System.Text.Json.Serialization.Metadata;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary>Configures tagged unions owned by individual operation argument contracts.</summary>
internal static class UcliOperationJsonPolymorphismConfigurator
{
    public static bool TryConfigure (JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type == typeof(AssetSchemaArgs))
        {
            typeInfo.PolymorphismOptions = IpcJsonPolymorphismOptions.Create(
                new JsonDerivedType(
                    typeof(AssetSchemaByTypeArgs),
                    Vocabulary.GetText(AssetSchemaSelectionKind.Type)),
                new JsonDerivedType(
                    typeof(AssetSchemaByTargetArgs),
                    Vocabulary.GetText(AssetSchemaSelectionKind.Target)));
            return true;
        }

        if (typeInfo.Type != typeof(GoCreateArgs))
        {
            return false;
        }

        typeInfo.PolymorphismOptions = IpcJsonPolymorphismOptions.Create(
            new JsonDerivedType(
                typeof(GoCreateInSceneArgs),
                Vocabulary.GetText(GoCreatePlacementKind.Scene)),
            new JsonDerivedType(
                typeof(GoCreateUnderParentArgs),
                Vocabulary.GetText(GoCreatePlacementKind.Parent)));
        return true;
    }
}
