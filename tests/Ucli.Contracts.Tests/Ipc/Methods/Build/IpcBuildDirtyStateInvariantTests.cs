using System.Text.Json;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Methods.Build;

public sealed class IpcBuildDirtyStateInvariantTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenItemsCollectionChanges_PreservesOriginalSnapshot ()
    {
        var originalItem = CreateItem("Assets/A.asset");
        var source = new[] { originalItem };
        var dirtyState = new IpcBuildDirtyState(
            Dirty: true,
            Coverage: IpcBuildDirtyStateCoverage.Full,
            Items: source);

        source[0] = CreateItem("Assets/B.asset");

        Assert.Same(originalItem, Assert.Single(dirtyState.Items));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenItemsContainNull_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new IpcBuildDirtyState(
            Dirty: true,
            Coverage: IpcBuildDirtyStateCoverage.Full,
            Items: [null!]));

        Assert.Equal("Items", exception.ParamName);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [Trait("Size", "Small")]
    public void Constructor_WhenDirtyDoesNotMatchItemPresence_ThrowsArgumentException (
        bool dirty,
        bool hasItem)
    {
        var items = hasItem
            ? new[] { CreateItem("Assets/A.asset") }
            : Array.Empty<IpcBuildDirtyStateItem>();

        var exception = Assert.Throws<ArgumentException>(() => new IpcBuildDirtyState(
            Dirty: dirty,
            Coverage: IpcBuildDirtyStateCoverage.Full,
            Items: items));

        Assert.Equal("Dirty", exception.ParamName);
    }

    [Theory]
    [InlineData("Assets/A.asset", "Assets/A.asset")]
    [InlineData("Assets/Z.asset", "Assets/A.asset")]
    [Trait("Size", "Small")]
    public void Constructor_WhenItemPathsAreNotUniqueAndAscending_ThrowsArgumentException (
        string firstPath,
        string secondPath)
    {
        var exception = Assert.Throws<ArgumentException>(() => new IpcBuildDirtyState(
            Dirty: true,
            Coverage: IpcBuildDirtyStateCoverage.Full,
            Items: [CreateItem(firstPath), CreateItem(secondPath)]));

        Assert.Equal("Items", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildDirtyStateItem_WhenPathIsNull_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new IpcBuildDirtyStateItem(
            IpcBuildDirtyStateItemKind.Asset,
            null!));

        Assert.Equal("Path", exception.ParamName);
    }

    [Theory]
    [InlineData(IpcBuildDirtyStateItemKind.Scene, "Assets/Scenes/Main.unity")]
    [InlineData(IpcBuildDirtyStateItemKind.Prefab, "Assets/Prefabs/Player.prefab")]
    [InlineData(IpcBuildDirtyStateItemKind.Asset, "Packages/com.example/package.json")]
    [InlineData(IpcBuildDirtyStateItemKind.ProjectSettings, "ProjectSettings/TagManager.asset")]
    [InlineData(IpcBuildDirtyStateItemKind.ProjectSettings, "ProjectSettings/Unexpected.unity")]
    [Trait("Size", "Small")]
    public void IpcBuildDirtyStateItem_WhenKnownKindMatchesPath_PreservesPair (
        IpcBuildDirtyStateItemKind kind,
        string pathValue)
    {
        var path = new ProjectMutationAuditPath(pathValue);

        var item = new IpcBuildDirtyStateItem(kind, path);

        Assert.Equal(kind, item.Kind);
        Assert.Same(path, item.Path);
    }

    [Theory]
    [InlineData(IpcBuildDirtyStateItemKind.Scene, "Assets/Data.asset")]
    [InlineData(IpcBuildDirtyStateItemKind.Prefab, "Assets/Scenes/Main.unity")]
    [InlineData(IpcBuildDirtyStateItemKind.Asset, "Assets/Prefabs/Player.prefab")]
    [InlineData(IpcBuildDirtyStateItemKind.ProjectSettings, "Assets/Data.asset")]
    [InlineData(IpcBuildDirtyStateItemKind.Scene, "ProjectSettings/Unexpected.unity")]
    [Trait("Size", "Small")]
    public void IpcBuildDirtyStateItem_WhenKnownKindContradictsPath_ThrowsArgumentException (
        IpcBuildDirtyStateItemKind kind,
        string pathValue)
    {
        var exception = Assert.Throws<ArgumentException>(() => new IpcBuildDirtyStateItem(
            kind,
            new ProjectMutationAuditPath(pathValue)));

        Assert.Equal("Kind", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildDirtyStateItem_WhenJsonKindContradictsPath_RejectsDeserialization ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "kind": "scene",
              "path": "ProjectSettings/TagManager.asset"
            }
            """);

        var succeeded = IpcPayloadCodec.TryDeserialize<IpcBuildDirtyStateItem>(
            document.RootElement,
            out var item,
            out var error);

        Assert.False(succeeded);
        Assert.Null(item);
        Assert.Equal(IpcPayloadReadErrorKind.DeserializeFailed, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildProjectMutationAuditItem_WhenPathIsNull_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new IpcBuildProjectMutationAuditItem(
            Path: null!,
            ChangeKind: IpcBuildProjectMutationChangeKind.Added,
            BeforeSha256: null,
            AfterSha256: Sha256Digest.Parse(new string('a', 64))));

        Assert.Equal("Path", exception.ParamName);
    }

    private static IpcBuildDirtyStateItem CreateItem (string path)
    {
        return new IpcBuildDirtyStateItem(
            IpcBuildDirtyStateItemKind.Asset,
            new ProjectMutationAuditPath(path));
    }
}
