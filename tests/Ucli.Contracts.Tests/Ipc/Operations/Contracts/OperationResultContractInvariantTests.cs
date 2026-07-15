using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations;

public sealed class OperationResultContractInvariantTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void AssetsFindResult_WhenConstructed_OwnsMatchesSnapshot ()
    {
        var matches = new List<AssetsFindMatch>
        {
            CreateAssetMatch(),
        };
        var window = CreateCompleteWindow();

        var result = new AssetsFindResult(matches, window);
        matches.Clear();

        Assert.Single(result.Matches);
        Assert.Same(window, result.Window);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AssetsFindResult_WhenRequiredReferenceIsNull_RejectsValue ()
    {
        var window = CreateCompleteWindow();

        Assert.Throws<ArgumentNullException>(() => new AssetsFindResult(null!, window));
        Assert.Throws<ArgumentNullException>(() => new AssetsFindResult([], null!));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AssetsFindResult_WhenMatchesContainNull_RejectsValue ()
    {
        Assert.Throws<ArgumentException>(() => new AssetsFindResult([null!], CreateCompleteWindow()));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GameObjectDescriptionResult_WhenConstructed_OwnsComponentAndChildSnapshots ()
    {
        var components = new List<GameObjectComponentDescriptionResult>
        {
            new(new UnityComponentTypeId("UnityEngine.Transform")),
        };
        var children = new List<GameObjectDescriptionResult>
        {
            new("Child", null, [], []),
        };

        var result = new GameObjectDescriptionResult("", null, components, children);
        components.Clear();
        children.Clear();

        Assert.Equal("", result.Name);
        Assert.Single(result.Components);
        Assert.Single(result.Children);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GameObjectDescriptionResult_WhenRequiredReferenceIsNull_RejectsValue ()
    {
        Assert.Throws<ArgumentNullException>(() => new GameObjectDescriptionResult(null!, null, [], []));
        Assert.Throws<ArgumentNullException>(() => new GameObjectDescriptionResult("Root", null, null!, []));
        Assert.Throws<ArgumentNullException>(() => new GameObjectDescriptionResult("Root", null, [], null!));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GameObjectDescriptionResult_WhenCollectionContainsNull_RejectsValue ()
    {
        Assert.Throws<ArgumentException>(() => new GameObjectDescriptionResult("Root", null, [null!], []));
        Assert.Throws<ArgumentException>(() => new GameObjectDescriptionResult("Root", null, [], [null!]));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SceneQueryResult_WhenConstructed_OwnsMatchesSnapshot ()
    {
        var scene = new SceneAssetPath("Assets/Scenes/Main.unity");
        var matches = new List<SceneQueryMatch>
        {
            CreateGameObjectMatch(),
        };

        var result = new SceneQueryResult(scene, matches);
        matches.Clear();

        Assert.Same(scene, result.Scene);
        Assert.Single(result.Matches);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SceneQueryResult_WhenRequiredReferenceIsNull_RejectsValue ()
    {
        var scene = new SceneAssetPath("Assets/Scenes/Main.unity");

        Assert.Throws<ArgumentNullException>(() => new SceneQueryResult(null!, []));
        Assert.Throws<ArgumentNullException>(() => new SceneQueryResult(scene, null!));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SceneQueryResult_WhenMatchesContainNull_RejectsValue ()
    {
        var scene = new SceneAssetPath("Assets/Scenes/Main.unity");

        Assert.Throws<ArgumentException>(() => new SceneQueryResult(scene, [null!]));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GameObjectComponentDescriptionResult_WhenSerialized_PreservesStringWireValue ()
    {
        var result = new GameObjectComponentDescriptionResult(
            new UnityComponentTypeId("UnityEngine.Transform"));

        var element = IpcPayloadCodec.SerializeToElement(result);

        Assert.Equal("UnityEngine.Transform", element.GetProperty("typeName").GetString());
        Assert.True(IpcPayloadCodec.TryDeserialize(
            element,
            out GameObjectComponentDescriptionResult deserialized,
            out _));
        Assert.Equal("UnityEngine.Transform", deserialized.TypeName!.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GameObjectDescriptionResultSchema_WhenGlobalObjectIdIsUnavailable_AllowsNull ()
    {
        var schemaJson = UcliOperationJsonSchemaGenerator.CreateResultSchemaJson(
            typeof(GameObjectDescriptionResult));
        using var document = JsonDocument.Parse(schemaJson!);

        var typeValues = document.RootElement
            .GetProperty("properties")
            .GetProperty("globalObjectId")
            .GetProperty("type")
            .EnumerateArray()
            .Select(static value => value.GetString())
            .ToArray();

        Assert.Equal(2, typeValues.Length);
        Assert.Equal("string", typeValues[0]);
        Assert.Equal("null", typeValues[1]);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData((UcliOperationReferenceTargetKind)0)]
    [InlineData(UcliOperationReferenceTargetKind.Asset)]
    [InlineData((UcliOperationReferenceTargetKind)999)]
    public void SceneQueryMatch_WhenKindIsNotAQueryableSceneTarget_RejectsValue (
        UcliOperationReferenceTargetKind kind)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SceneQueryMatch(
            kind,
            new UnityHierarchyPath("Root"),
            componentType: null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SceneQueryMatch_WhenTargetAndComponentTypeConflict_RejectsValue ()
    {
        Assert.Throws<ArgumentNullException>(() => new SceneQueryMatch(
            UcliOperationReferenceTargetKind.Component,
            new UnityHierarchyPath("Root"),
            componentType: null));
        Assert.Throws<ArgumentException>(() => new SceneQueryMatch(
            UcliOperationReferenceTargetKind.GameObject,
            new UnityHierarchyPath("Root"),
            new UnityComponentTypeId("UnityEngine.Transform")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SceneQueryMatch_WhenHierarchyPathIsNull_RejectsValue ()
    {
        Assert.Throws<ArgumentNullException>(() => new SceneQueryMatch(
            UcliOperationReferenceTargetKind.GameObject,
            hierarchyPath: null!,
            componentType: null));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UcliOperationReferenceTargetKind.GameObject, "gameObject")]
    [InlineData(UcliOperationReferenceTargetKind.Component, "component")]
    public void SceneQueryMatch_WhenSerialized_UsesExistingTargetKindLiteral (
        UcliOperationReferenceTargetKind kind,
        string expectedLiteral)
    {
        var match = new SceneQueryMatch(
            kind,
            new UnityHierarchyPath("Root"),
            kind == UcliOperationReferenceTargetKind.Component
                ? new UnityComponentTypeId("UnityEngine.Transform")
                : null);

        var element = IpcPayloadCodec.SerializeToElement(match);

        Assert.Equal(expectedLiteral, element.GetProperty("kind").GetString());
        Assert.True(IpcPayloadCodec.TryDeserialize(element, out SceneQueryMatch deserialized, out _));
        Assert.Equal(kind, deserialized.Kind);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(typeof(AssetsFindResult), nameof(AssetsFindResult.Matches))]
    [InlineData(typeof(AssetsFindResult), nameof(AssetsFindResult.Window))]
    [InlineData(typeof(GameObjectDescriptionResult), nameof(GameObjectDescriptionResult.Name))]
    [InlineData(typeof(GameObjectDescriptionResult), nameof(GameObjectDescriptionResult.GlobalObjectId))]
    [InlineData(typeof(GameObjectDescriptionResult), nameof(GameObjectDescriptionResult.Components))]
    [InlineData(typeof(GameObjectDescriptionResult), nameof(GameObjectDescriptionResult.Children))]
    [InlineData(typeof(GameObjectComponentDescriptionResult), nameof(GameObjectComponentDescriptionResult.TypeName))]
    [InlineData(typeof(SceneQueryMatch), nameof(SceneQueryMatch.Kind))]
    [InlineData(typeof(SceneQueryMatch), nameof(SceneQueryMatch.HierarchyPath))]
    [InlineData(typeof(SceneQueryMatch), nameof(SceneQueryMatch.ComponentType))]
    [InlineData(typeof(SceneQueryResult), nameof(SceneQueryResult.Scene))]
    [InlineData(typeof(SceneQueryResult), nameof(SceneQueryResult.Matches))]
    public void ResultContractProperty_DoesNotExposeStateMutation (
        Type contractType,
        string propertyName)
    {
        Assert.Null(contractType.GetProperty(propertyName)!.SetMethod);
    }

    private static AssetsFindMatch CreateAssetMatch ()
    {
        return new AssetsFindMatch(
            new UnityAssetPath("Assets/Data/Item.asset"),
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Item",
            new UnityTypeId("UnityEngine.ScriptableObject, UnityEngine.CoreModule"));
    }

    private static SceneQueryMatch CreateGameObjectMatch ()
    {
        return new SceneQueryMatch(
            UcliOperationReferenceTargetKind.GameObject,
            new UnityHierarchyPath("Root"),
            componentType: null);
    }

    private static BoundedWindow CreateCompleteWindow ()
    {
        return new BoundedWindow(
            limit: 1,
            cursor: null,
            nextCursor: null,
            isComplete: true,
            totalCount: 1);
    }
}
