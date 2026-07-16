using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations.Contracts;

public sealed class OperationContractConstructorTests
{
    public static TheoryData<Type> ConstructorOwnedContractTypes => new()
    {
        typeof(AssetCreateArgs),
        typeof(AssetSaveArgs),
        typeof(AssetSchemaArgs),
        typeof(AssetSetArgs),
        typeof(AssetsFindArgs),
        typeof(SerializedObjectSetItemArgs),
        typeof(ComponentTypeArgs),
        typeof(ComponentEnsureArgs),
        typeof(ComponentSetArgs),
        typeof(CsEvalArgs),
        typeof(GoCreateArgs),
        typeof(GoDescribeArgs),
        typeof(GoReparentArgs),
        typeof(GoTargetArgs),
        typeof(PrefabCreateArgs),
        typeof(PrefabOverrideArgs),
        typeof(PrefabPathArgs),
        typeof(SceneQueryArgs),
        typeof(ScenePathArgs),
        typeof(SceneTreeArgs),
        typeof(AssetReferenceArgs),
        typeof(GameObjectReferenceArgs),
        typeof(SceneGameObjectReferenceArgs),
        typeof(ComponentReferenceArgs),
        typeof(ResolveSelectorArgs),
    };

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

    [Theory]
    [MemberData(nameof(ConstructorOwnedContractTypes))]
    [Trait("Size", "Small")]
    public void ValidatedOperationContract_PublicProperties_AreConstructorOwned (Type contractType)
    {
        Assert.All(
            contractType.GetProperties(BindingFlags.Instance | BindingFlags.Public),
            static property => Assert.Null(property.SetMethod));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RequiredOperationArgument_WhenRequiredReferenceIsNull_ThrowsArgumentNullException ()
    {
        foreach (var validArgs in CreateValidRequiredOperationArguments())
        {
            var contractType = validArgs.GetType();
            var constructor = Assert.Single(contractType.GetConstructors());
            var parameters = constructor.GetParameters();
            var arguments = parameters
                .Select(parameter => GetConstructorArgument(validArgs, parameter))
                .ToArray();

            for (var parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
            {
                var parameter = parameters[parameterIndex];
                if (parameter.ParameterType.IsValueType
                    || GetContractProperty(contractType, parameter).GetCustomAttribute<UcliRequiredAttribute>() is null)
                {
                    continue;
                }

                var invalidArguments = arguments.ToArray();
                invalidArguments[parameterIndex] = null;

                var exception = Assert.Throws<TargetInvocationException>(() => constructor.Invoke(invalidArguments));
                var argumentNullException = Assert.IsType<ArgumentNullException>(exception.InnerException);
                Assert.Equal(parameter.Name, argumentNullException.ParamName);
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Size", "Small")]
    public void SetArguments_Sets_SnapshotsCallerOwnedCollection (bool assetSet)
    {
        var original = CreateSetItem("m_Name");
        var replacement = CreateSetItem("m_Enabled");
        var callerOwnedSets = new[] { original };

        var sets = assetSet
            ? new AssetSetArgs(CreateAssetReference(), callerOwnedSets).Sets
            : new ComponentSetArgs(CreateComponentReference(), callerOwnedSets).Sets;
        callerOwnedSets[0] = replacement;

        Assert.Same(original, Assert.Single(sets));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Size", "Small")]
    public void SetArguments_Sets_WhenItemIsNull_ThrowsArgumentException (bool assetSet)
    {
        var sets = new SerializedObjectSetItemArgs[] { null! };

        var exception = assetSet
            ? Assert.Throws<ArgumentException>(() => new AssetSetArgs(CreateAssetReference(), sets))
            : Assert.Throws<ArgumentException>(() => new ComponentSetArgs(CreateComponentReference(), sets));

        Assert.Equal("sets", exception.ParamName);
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

    private static IReadOnlyList<object> CreateValidRequiredOperationArguments ()
    {
        var scenePath = new SceneAssetPath("Assets/Scenes/Main.unity");
        var setItem = CreateSetItem("m_Name");
        return new object[]
        {
            new AssetCreateArgs(
                new UnityTypeId("UnityEngine.Material"),
                new UnityAssetPath("Assets/Data/Material.asset")),
            new AssetSaveArgs(CreateAssetReference()),
            new AssetSetArgs(CreateAssetReference(), new[] { setItem }),
            setItem,
            new ComponentTypeArgs(new UnityComponentTypeId("UnityEngine.Transform")),
            new ComponentEnsureArgs(CreateGameObjectReference(), new UnityComponentTypeId("UnityEngine.Transform")),
            new ComponentSetArgs(CreateComponentReference(), new[] { setItem }),
            new CsEvalArgs("return null;"),
            new GoCreateArgs("Created", scenePath, parent: null),
            new GoDescribeArgs(CreateGameObjectReference(), depth: null),
            new GoReparentArgs(CreateGameObjectReference(), CreateGameObjectReference()),
            new GoTargetArgs(CreateGameObjectReference()),
            new PrefabCreateArgs(
                CreateSceneGameObjectReference(),
                new PrefabAssetPath("Assets/Prefabs/Created.prefab")),
            new PrefabOverrideArgs(
                CreateComponentReference(),
                new PrefabAssetPath("Assets/Prefabs/Target.prefab"),
                propertyPaths: null),
            new PrefabPathArgs(new PrefabAssetPath("Assets/Prefabs/Target.prefab")),
            new SceneQueryArgs(scenePath, pathPrefix: null, componentType: null),
            new ScenePathArgs(scenePath),
            new SceneTreeArgs(new UnityScenePath(scenePath.Value), depth: null, limit: null, cursor: null),
        };
    }

    private static object? GetConstructorArgument (
        object value,
        ParameterInfo parameter)
    {
        return GetContractProperty(value.GetType(), parameter).GetValue(value);
    }

    private static PropertyInfo GetContractProperty (
        Type contractType,
        ParameterInfo parameter)
    {
        return Assert.Single(
            contractType.GetProperties(BindingFlags.Instance | BindingFlags.Public),
            property => string.Equals(property.Name, parameter.Name, StringComparison.OrdinalIgnoreCase));
    }

    private static AssetReferenceArgs CreateAssetReference ()
    {
        return new AssetReferenceArgs(
            new UcliPlanAlias("asset"),
            globalObjectId: null,
            assetGuid: null,
            assetPath: null,
            projectAssetPath: null);
    }

    private static GameObjectReferenceArgs CreateGameObjectReference ()
    {
        return new GameObjectReferenceArgs(
            new UcliPlanAlias("gameObject"),
            globalObjectId: null,
            prefab: null,
            scene: null,
            hierarchyPath: null);
    }

    private static SceneGameObjectReferenceArgs CreateSceneGameObjectReference ()
    {
        return new SceneGameObjectReferenceArgs(
            new UcliPlanAlias("sceneGameObject"),
            globalObjectId: null,
            scene: null,
            hierarchyPath: null);
    }

    private static ComponentReferenceArgs CreateComponentReference ()
    {
        return new ComponentReferenceArgs(
            new UcliPlanAlias("component"),
            globalObjectId: null,
            scene: null,
            prefab: null,
            hierarchyPath: null,
            componentType: null);
    }

    private static SerializedObjectSetItemArgs CreateSetItem (string path)
    {
        return new SerializedObjectSetItemArgs(
            new SerializedPropertyPath(path),
            JsonSerializer.SerializeToElement("value"));
    }
}
