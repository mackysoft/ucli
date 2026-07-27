using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations.Contracts;

public sealed class OperationContractConstructorTests
{
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

        Assert.Equal("m_Name", Assert.Single(sets).Path.Value);
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

    private static AssetReferenceArgs CreateAssetReference ()
    {
        return new UcliAliasReferenceArgs(new UcliPlanAlias("asset"));
    }

    private static ComponentReferenceArgs CreateComponentReference ()
    {
        return new UcliAliasReferenceArgs(new UcliPlanAlias("component"));
    }

    private static SerializedObjectSetItemArgs CreateSetItem (string path)
    {
        return new SerializedObjectSetItemArgs(
            new SerializedPropertyPath(path),
            JsonSerializer.SerializeToElement("value"));
    }
}
