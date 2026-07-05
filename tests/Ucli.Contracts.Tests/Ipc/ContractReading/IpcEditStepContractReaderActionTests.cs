using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.ContractReading;

public sealed class IpcEditStepContractReaderActionTests
{
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
