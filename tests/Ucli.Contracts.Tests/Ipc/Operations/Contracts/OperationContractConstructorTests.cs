using System.Reflection;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations.Contracts;

public sealed class OperationContractConstructorTests
{
    public static TheoryData<Type> ValueObjectBoundaryContractTypes => new()
    {
        typeof(GameObjectReferenceArgs),
        typeof(SceneGameObjectReferenceArgs),
        typeof(ComponentReferenceArgs),
        typeof(AssetReferenceArgs),
        typeof(AssetCreateArgs),
        typeof(ComponentTypeArgs),
        typeof(ComponentEnsureArgs),
        typeof(GoCreateArgs),
        typeof(PrefabCreateArgs),
        typeof(PrefabPathArgs),
        typeof(SceneQueryArgs),
        typeof(SceneQueryMatch),
        typeof(ScenePathArgs),
        typeof(SerializedObjectSetItemArgs),
        typeof(IpcResolveOperationResult),
        typeof(AssetsFindArgs),
        typeof(SceneTreeArgs),
    };

    [Theory]
    [MemberData(nameof(ValueObjectBoundaryContractTypes))]
    [Trait("Size", "Small")]
    public void ValueObjectBoundaryContract_PublicConstruction_ExposesOnlyJsonConstructor (Type contractType)
    {
        var constructor = Assert.Single(contractType.GetConstructors());

        Assert.NotNull(constructor.GetCustomAttribute<JsonConstructorAttribute>());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SceneQueryArgs_PathPrefix_UsesUnityHierarchyPathContract ()
    {
        var pathPrefix = new UnityHierarchyPath("Root/Child");

        var args = new SceneQueryArgs(
            new SceneAssetPath("Assets/Scenes/Main.unity"),
            pathPrefix,
            componentType: null);

        Assert.Same(pathPrefix, args.PathPrefix);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AssetsFindArgs_PathPrefix_UsesUnityAssetPathPrefixContract ()
    {
        var pathPrefix = new UnityAssetPathPrefix("Assets/Prefabs");

        var args = new AssetsFindArgs(
            type: null,
            pathPrefix,
            nameContains: null,
            limit: null,
            cursor: null);

        Assert.Same(pathPrefix, args.PathPrefix);
    }
}
