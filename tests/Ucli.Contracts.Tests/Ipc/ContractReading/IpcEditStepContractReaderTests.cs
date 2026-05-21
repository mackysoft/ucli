using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.ContractReading;

public sealed class IpcEditStepContractReaderTests
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
    public void TryRead_WhenProjectAssetSelectionIsValid_ReturnsParsedContract ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "kind": "edit",
              "id": "edit-project",
              "on": {
                "project": true
              },
              "select": {
                "projectAsset": {
                  "path": "ProjectSettings/TagManager.asset"
                },
                "cardinality": "one"
              },
              "actions": [
                {
                  "kind": "set",
                  "values": {
                    "tags": []
                  }
                }
              ],
              "commit": "project"
            }
            """);

        var result = IpcEditStepContractReader.TryRead(document.RootElement, out var contract, out var errorMessage);

        Assert.True(result, errorMessage);
        Assert.Equal(IpcEditStepContract.ContextKind.Project, contract.Context.Kind);
        Assert.Null(contract.Context.Path);
        Assert.Equal(IpcEditStepContract.SelectionKind.Direct, contract.Selection.Kind);
        Assert.Equal(IpcEditStepContract.CardinalityKind.One, contract.Selection.Cardinality);
        Assert.Equal("ProjectSettings/TagManager.asset", contract.Selection.ProjectAssetPath);
        Assert.False(contract.Selection.Self);
        var action = Assert.Single(contract.Actions);
        Assert.Equal(IpcEditStepContract.ActionKind.Set, action.Kind);
        Assert.Equal(JsonValueKind.Object, action.Values.ValueKind);
        Assert.Equal(IpcEditStepContract.CommitKind.Project, contract.Commit);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_WhenDirectSelectionUsesFirst_ReturnsFalse ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "kind": "edit",
              "id": "edit-direct-first",
              "on": {
                "scene": "Assets/Scenes/Main.unity"
              },
              "select": {
                "gameObject": "Root",
                "cardinality": "first"
              },
              "actions": [
                {
                  "kind": "delete"
                }
              ],
              "commit": "none"
            }
            """);

        var result = IpcEditStepContractReader.TryRead(document.RootElement, out _, out var errorMessage);

        Assert.False(result);
        Assert.Equal("Edit step property 'step.select.cardinality' value 'first' is supported only for candidate-source selections.", errorMessage);
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

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_WhenSetActionValuesIsEmpty_ReturnsFalse ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "kind": "edit",
              "id": "edit-project",
              "on": {
                "project": true
              },
              "select": {
                "projectAsset": {
                  "path": "ProjectSettings/TagManager.asset"
                },
                "cardinality": "one"
              },
              "actions": [
                {
                  "kind": "set",
                  "values": {}
                }
              ],
              "commit": "project"
            }
            """);

        var result = IpcEditStepContractReader.TryRead(document.RootElement, out _, out var errorMessage);

        Assert.False(result);
        Assert.Equal("Edit step property 'step.actions[0].values' must contain at least one assignment.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_WhenPrefabOverrideActionsAreValid_ReturnsParsedContract ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "kind": "edit",
              "id": "edit-prefab-overrides",
              "on": {
                "scene": "Assets/Scenes/Main.unity"
              },
              "select": {
                "gameObject": "Root",
                "component": "UnityEngine.BoxCollider, UnityEngine.PhysicsModule",
                "cardinality": "one"
              },
              "actions": [
                {
                  "kind": "applyPrefabOverrides",
                  "targetAssetPath": "Assets/Prefabs/Root.prefab",
                  "propertyPaths": [
                    "m_IsTrigger"
                  ]
                },
                {
                  "kind": "revertPrefabOverrides",
                  "target": "$collider",
                  "targetAssetPath": "Assets/Prefabs/Root.prefab"
                }
              ],
              "commit": "none"
            }
            """);

        var result = IpcEditStepContractReader.TryRead(document.RootElement, out var contract, out var errorMessage);

        Assert.True(result, errorMessage);
        Assert.Equal(2, contract.Actions.Count);
        Assert.Equal(IpcEditStepContract.ActionKind.ApplyPrefabOverrides, contract.Actions[0].Kind);
        Assert.Equal("Assets/Prefabs/Root.prefab", contract.Actions[0].TargetAssetPath);
        Assert.Equal(["m_IsTrigger"], contract.Actions[0].PropertyPaths);
        Assert.Equal(IpcEditStepContract.ActionKind.RevertPrefabOverrides, contract.Actions[1].Kind);
        Assert.Equal("$collider", contract.Actions[1].Target);
        Assert.Null(contract.Actions[1].PropertyPaths);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_WhenCreatePrefabOmitsTarget_ReturnsParsedContract ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "kind": "edit",
              "id": "create-prefab",
              "on": {
                "scene": "Assets/Scenes/Main.unity"
              },
              "select": {
                "gameObject": "Root",
                "cardinality": "one"
              },
              "actions": [
                {
                  "kind": "createPrefab",
                  "path": "Assets/Prefabs/Root.prefab"
                }
              ],
              "commit": "none"
            }
            """);

        var result = IpcEditStepContractReader.TryRead(document.RootElement, out var contract, out var errorMessage);

        Assert.True(result, errorMessage);
        Assert.Single(contract.Actions);
        Assert.Equal(IpcEditStepContract.ActionKind.CreatePrefab, contract.Actions[0].Kind);
        Assert.Null(contract.Actions[0].Target);
        Assert.Equal("Assets/Prefabs/Root.prefab", contract.Actions[0].Path);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("""[]""", "Edit step property 'step.actions[0].propertyPaths' must contain at least one path when specified.")]
    [InlineData("""["m_IsTrigger","m_IsTrigger"]""", "Edit step property 'step.actions[0].propertyPaths' contains duplicate path: m_IsTrigger.")]
    public void TryRead_WhenPrefabOverridePropertyPathsAreInvalid_ReturnsFalse (
        string propertyPathsJson,
        string expectedMessage)
    {
        using var document = JsonDocument.Parse(
            $$"""
            {
              "kind": "edit",
              "id": "edit-prefab-overrides",
              "on": {
                "scene": "Assets/Scenes/Main.unity"
              },
              "select": {
                "gameObject": "Root",
                "component": "UnityEngine.BoxCollider, UnityEngine.PhysicsModule",
                "cardinality": "one"
              },
              "actions": [
                {
                  "kind": "applyPrefabOverrides",
                  "targetAssetPath": "Assets/Prefabs/Root.prefab",
                  "propertyPaths": {{propertyPathsJson}}
                }
              ],
              "commit": "none"
            }
            """);

        var result = IpcEditStepContractReader.TryRead(document.RootElement, out _, out var errorMessage);

        Assert.False(result);
        Assert.Equal(expectedMessage, errorMessage);
    }
}
