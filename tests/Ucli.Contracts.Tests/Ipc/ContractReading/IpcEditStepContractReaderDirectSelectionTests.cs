using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.ContractReading;

public sealed class IpcEditStepContractReaderDirectSelectionTests
{
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
}
