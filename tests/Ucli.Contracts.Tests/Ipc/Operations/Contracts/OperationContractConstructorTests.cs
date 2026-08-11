using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations.Contracts;

public sealed class OperationContractConstructorTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingScriptsAssetKinds_SnapshotsCallerOwnedCollectionAndDoesNotExposeMutableArray (bool requestedScope)
    {
        var assetKinds = new[] { MissingScriptsAssetKind.Scene };

        IReadOnlyList<MissingScriptsAssetKind> observedAssetKinds = requestedScope
            ? new MissingScriptsRequestedScope(
                new[] { new UnityAssetPathPrefix("Assets") },
                assetKinds).AssetKinds
            : new MissingScriptsCheckArgs(
                new[] { new UnityAssetPathPrefix("Assets") },
                assetKinds).AssetKinds;
        assetKinds[0] = MissingScriptsAssetKind.Prefab;

        Assert.Equal(MissingScriptsAssetKind.Scene, Assert.Single(observedAssetKinds));
        Assert.IsNotType<MissingScriptsAssetKind[]>(observedAssetKinds);
        var mutableView = Assert.IsAssignableFrom<IList<MissingScriptsAssetKind>>(observedAssetKinds);
        Assert.Throws<NotSupportedException>(() => mutableView[0] = MissingScriptsAssetKind.Prefab);
        Assert.Equal(MissingScriptsAssetKind.Scene, Assert.Single(observedAssetKinds));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MissingScriptsCheckArgs_AssetKinds_WhenEmpty_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new MissingScriptsCheckArgs(
            new[] { new UnityAssetPathPrefix("Assets") },
            Array.Empty<MissingScriptsAssetKind>()));

        Assert.Equal("assetKinds", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MissingScriptsCheckArgs_AssetKinds_WhenDuplicated_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new MissingScriptsCheckArgs(
            new[] { new UnityAssetPathPrefix("Assets") },
            new[] { MissingScriptsAssetKind.Scene, MissingScriptsAssetKind.Scene }));

        Assert.Equal("assetKinds", exception.ParamName);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(true)]
    [InlineData(false)]
    public void MissingScriptsRequestedScope_WhenRootsOrAssetKindsAreEmpty_ThrowsArgumentException (bool rootsEmpty)
    {
        var exception = Assert.Throws<ArgumentException>(() => new MissingScriptsRequestedScope(
            rootsEmpty ? Array.Empty<UnityAssetPathPrefix>() : new[] { new UnityAssetPathPrefix("Assets") },
            rootsEmpty ? new[] { MissingScriptsAssetKind.Scene } : Array.Empty<MissingScriptsAssetKind>()));

        Assert.Equal(rootsEmpty ? "roots" : "assetKinds", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MissingScriptsRequestedScope_AssetKinds_WhenDuplicated_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new MissingScriptsRequestedScope(
            new[] { new UnityAssetPathPrefix("Assets") },
            new[] { MissingScriptsAssetKind.Prefab, MissingScriptsAssetKind.Prefab }));

        Assert.Equal("assetKinds", exception.ParamName);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(" Root")]
    [InlineData("Root ")]
    [InlineData("Root//Child")]
    [InlineData("/Root")]
    [InlineData("Root/")]
    public void UnityHierarchyPath_TryParse_WhenPathCannotBeRepresented_ReturnsFalse (string value)
    {
        Assert.False(UnityHierarchyPath.TryParse(value, out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnityHierarchyPath_TryParse_WhenPathContainsUnpairedSurrogate_ReturnsFalse ()
    {
        var value = new string(new[] { '\ud800' });

        Assert.False(UnityHierarchyPath.TryParse(value, out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MissingScriptSlot_ComponentIndex_WhenNegative_ThrowsArgumentOutOfRangeException ()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new MissingScriptSlot(
            new UnityAssetPath("Assets/checked.prefab"),
            new UnityHierarchyPath("Root"),
            componentIndex: -1));

        Assert.Equal("componentIndex", exception.ParamName);
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
