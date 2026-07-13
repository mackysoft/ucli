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
}
