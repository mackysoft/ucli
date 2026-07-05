using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.ContractReading;

public sealed class IpcEditStepContractReaderSourceSelectionTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_WhenSceneSelectFromIsValid_ReturnsParsedContract ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "kind": "edit",
              "id": "edit-query",
              "on": {
                "scene": "Assets/Scenes/Main.unity"
              },
              "select": {
                "from": {
                  "op": "__SCENE_QUERY_OP__",
                  "args": {
                    "pathPrefix": "Root/Enemies",
                    "componentType": "Game.EnemySpawner, Assembly-CSharp"
                  }
                },
                "cardinality": "all"
              },
              "actions": [
                {
                  "kind": "delete"
                }
              ],
              "commit": "context"
            }
            """
                .Replace("__SCENE_QUERY_OP__", UcliPrimitiveOperationNames.SceneQuery, StringComparison.Ordinal));

        var result = IpcEditStepContractReader.TryRead(document.RootElement, out var contract, out var errorMessage);

        Assert.True(result, errorMessage);
        Assert.Equal("edit-query", contract.Id);
        Assert.Equal(IpcEditStepContract.ContextKind.Scene, contract.Context.Kind);
        Assert.Equal("Assets/Scenes/Main.unity", contract.Context.Path);
        Assert.Equal(IpcEditStepContract.SelectionKind.From, contract.Selection.Kind);
        Assert.Equal(IpcEditStepContract.CardinalityKind.All, contract.Selection.Cardinality);
        Assert.Equal(UcliPrimitiveOperationNames.SceneQuery, contract.Selection.SourceOperation);
        Assert.True(contract.Selection.SourceArgs.TryGetProperty("pathPrefix", out var pathPrefix));
        Assert.Equal("Root/Enemies", pathPrefix.GetString());
        Assert.True(contract.Selection.SourceArgs.TryGetProperty("componentType", out var componentType));
        Assert.Equal("Game.EnemySpawner, Assembly-CSharp", componentType.GetString());
        var action = Assert.Single(contract.Actions);
        Assert.Equal(IpcEditStepContract.ActionKind.Delete, action.Kind);
        Assert.Equal(IpcEditStepContract.CommitKind.Context, contract.Commit);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_WhenSceneSelectFromUsesFirst_ReturnsParsedContract ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "kind": "edit",
              "id": "edit-first-query",
              "on": {
                "scene": "Assets/Scenes/Main.unity"
              },
              "select": {
                "from": {
                  "op": "__SCENE_QUERY_OP__",
                  "args": {
                    "pathPrefix": "Root"
                  }
                },
                "cardinality": "first"
              },
              "actions": [
                {
                  "kind": "delete"
                }
              ],
              "commit": "none"
            }
            """
                .Replace("__SCENE_QUERY_OP__", UcliPrimitiveOperationNames.SceneQuery, StringComparison.Ordinal));

        var result = IpcEditStepContractReader.TryRead(document.RootElement, out var contract, out var errorMessage);

        Assert.True(result, errorMessage);
        Assert.Equal(IpcEditStepContract.SelectionKind.From, contract.Selection.Kind);
        Assert.Equal(IpcEditStepContract.CardinalityKind.First, contract.Selection.Cardinality);
        Assert.Equal(UcliPrimitiveOperationNames.SceneQuery, contract.Selection.SourceOperation);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_WhenSelectFromIsUsedOutsideSceneContext_ReturnsFalse ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "kind": "edit",
              "id": "edit-prefab-query",
              "on": {
                "prefab": "Assets/Prefabs/Enemy.prefab"
              },
              "select": {
                "from": {
                  "op": "__SCENE_QUERY_OP__",
                  "args": {
                    "pathPrefix": "Root"
                  }
                },
                "cardinality": "first"
              },
              "actions": [
                {
                  "kind": "delete"
                }
              ],
              "commit": "context"
            }
            """
                .Replace("__SCENE_QUERY_OP__", UcliPrimitiveOperationNames.SceneQuery, StringComparison.Ordinal));

        var result = IpcEditStepContractReader.TryRead(document.RootElement, out _, out var errorMessage);

        Assert.False(result);
        Assert.Equal("Edit step property 'step.select.from' is supported only for scene context.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_WhenSelectFromContainsUnknownProperty_ReturnsFalse ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "kind": "edit",
              "id": "edit-query",
              "on": {
                "scene": "Assets/Scenes/Main.unity"
              },
              "select": {
                "from": {
                  "op": "__SCENE_QUERY_OP__",
                  "args": {
                    "pathPrefix": "Root"
                  },
                  "extra": true
                },
                "cardinality": "all"
              },
              "actions": [
                {
                  "kind": "delete"
                }
              ],
              "commit": "context"
            }
            """
                .Replace("__SCENE_QUERY_OP__", UcliPrimitiveOperationNames.SceneQuery, StringComparison.Ordinal));

        var result = IpcEditStepContractReader.TryRead(document.RootElement, out _, out var errorMessage);

        Assert.False(result);
        Assert.Equal("Edit step property 'step.select.from' contains an unknown property: extra.", errorMessage);
    }
}
