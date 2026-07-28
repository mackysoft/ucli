using System.Text.Json.Serialization.Metadata;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary>Configures tagged unions for operation arguments that reference Unity objects.</summary>
internal static class UcliReferenceJsonPolymorphismConfigurator
{
    public static bool TryConfigure (JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type == typeof(UnityObjectReferenceArgs))
        {
            ConfigureUnityObjectReferences(typeInfo);
            return true;
        }

        if (typeInfo.Type == typeof(AssetReferenceArgs))
        {
            ConfigureAssetReferences(typeInfo);
            return true;
        }

        if (typeInfo.Type == typeof(GameObjectReferenceArgs))
        {
            ConfigureGameObjectReferences(typeInfo);
            return true;
        }

        if (typeInfo.Type == typeof(SceneGameObjectReferenceArgs))
        {
            ConfigureSceneGameObjectReferences(typeInfo);
            return true;
        }

        if (typeInfo.Type == typeof(ComponentReferenceArgs))
        {
            ConfigureComponentReferences(typeInfo);
            return true;
        }

        if (typeInfo.Type != typeof(ResolveSelectorArgs))
        {
            return false;
        }

        ConfigureResolveSelectors(typeInfo);
        return true;
    }

    private static void ConfigureUnityObjectReferences (JsonTypeInfo typeInfo)
    {
        typeInfo.PolymorphismOptions = CreateOptions(
            (typeof(UcliAliasReferenceArgs), UcliReferenceKind.Alias),
            (typeof(GlobalObjectIdReferenceArgs), UcliReferenceKind.GlobalObjectId),
            (typeof(AssetGuidReferenceArgs), UcliReferenceKind.AssetGuid),
            (typeof(AssetPathReferenceArgs), UcliReferenceKind.AssetPath),
            (typeof(ProjectAssetPathReferenceArgs), UcliReferenceKind.ProjectAssetPath),
            (typeof(SceneHierarchyReferenceArgs), UcliReferenceKind.SceneHierarchy),
            (typeof(PrefabHierarchyReferenceArgs), UcliReferenceKind.PrefabHierarchy),
            (typeof(SceneComponentReferenceArgs), UcliReferenceKind.SceneComponent),
            (typeof(PrefabComponentReferenceArgs), UcliReferenceKind.PrefabComponent));
    }

    private static void ConfigureAssetReferences (JsonTypeInfo typeInfo)
    {
        typeInfo.PolymorphismOptions = CreateOptions(
            (typeof(UcliAliasReferenceArgs), UcliReferenceKind.Alias),
            (typeof(GlobalObjectIdReferenceArgs), UcliReferenceKind.GlobalObjectId),
            (typeof(AssetGuidReferenceArgs), UcliReferenceKind.AssetGuid),
            (typeof(AssetPathReferenceArgs), UcliReferenceKind.AssetPath),
            (typeof(ProjectAssetPathReferenceArgs), UcliReferenceKind.ProjectAssetPath));
    }

    private static void ConfigureGameObjectReferences (JsonTypeInfo typeInfo)
    {
        typeInfo.PolymorphismOptions = CreateOptions(
            (typeof(UcliAliasReferenceArgs), UcliReferenceKind.Alias),
            (typeof(GlobalObjectIdReferenceArgs), UcliReferenceKind.GlobalObjectId),
            (typeof(SceneHierarchyReferenceArgs), UcliReferenceKind.SceneHierarchy),
            (typeof(PrefabHierarchyReferenceArgs), UcliReferenceKind.PrefabHierarchy));
    }

    private static void ConfigureSceneGameObjectReferences (JsonTypeInfo typeInfo)
    {
        typeInfo.PolymorphismOptions = CreateOptions(
            (typeof(UcliAliasReferenceArgs), UcliReferenceKind.Alias),
            (typeof(GlobalObjectIdReferenceArgs), UcliReferenceKind.GlobalObjectId),
            (typeof(SceneHierarchyReferenceArgs), UcliReferenceKind.SceneHierarchy));
    }

    private static void ConfigureComponentReferences (JsonTypeInfo typeInfo)
    {
        typeInfo.PolymorphismOptions = CreateOptions(
            (typeof(UcliAliasReferenceArgs), UcliReferenceKind.Alias),
            (typeof(GlobalObjectIdReferenceArgs), UcliReferenceKind.GlobalObjectId),
            (typeof(SceneComponentReferenceArgs), UcliReferenceKind.SceneComponent),
            (typeof(PrefabComponentReferenceArgs), UcliReferenceKind.PrefabComponent));
    }

    private static void ConfigureResolveSelectors (JsonTypeInfo typeInfo)
    {
        typeInfo.PolymorphismOptions = CreateOptions(
            (typeof(GlobalObjectIdReferenceArgs), UcliReferenceKind.GlobalObjectId),
            (typeof(AssetGuidReferenceArgs), UcliReferenceKind.AssetGuid),
            (typeof(AssetPathReferenceArgs), UcliReferenceKind.AssetPath),
            (typeof(ProjectAssetPathReferenceArgs), UcliReferenceKind.ProjectAssetPath),
            (typeof(SceneHierarchyReferenceArgs), UcliReferenceKind.SceneHierarchy),
            (typeof(PrefabHierarchyReferenceArgs), UcliReferenceKind.PrefabHierarchy),
            (typeof(SceneComponentReferenceArgs), UcliReferenceKind.SceneComponent),
            (typeof(PrefabComponentReferenceArgs), UcliReferenceKind.PrefabComponent));
    }

    private static JsonPolymorphismOptions CreateOptions (
        params (Type Type, UcliReferenceKind Kind)[] variants)
    {
        var options = IpcJsonPolymorphismOptions.Create();
        for (var i = 0; i < variants.Length; i++)
        {
            var variant = variants[i];
            options.DerivedTypes.Add(new JsonDerivedType(
                variant.Type,
                Vocabulary.GetText(variant.Kind)));
        }

        return options;
    }
}
