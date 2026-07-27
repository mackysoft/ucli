using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.ContractReading;

public sealed class UcliRequestJsonContractIntegrationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_MapsPublicOperationStepAndCreatesPrivatePositionIdentity ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "protocolVersion": 1,
              "steps": [
                {
                  "kind": "op",
                  "op": "ucli.resolve",
                  "args": {}
                }
              ]
            }
            """);

        var result = IpcExecuteArgumentsContractReader.TryRead(
            document.RootElement,
            out var request,
            out var error);

        Assert.True(result, error.Message);
        var step = Assert.Single(request.Steps);
        Assert.Equal(IpcExecuteStepKind.Op, step.Kind);
        Assert.Equal("0", step.Id.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_MapsEditDtoToExecutionModel ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "protocolVersion": 1,
              "steps": [
                {
                  "on": {
                    "path": "Assets/Scenes/Main.unity",
                    "kind": "scene"
                  },
                  "select": {
                    "path": "Root/Spawner",
                    "component": "Game.Spawner, Assembly-CSharp",
                    "cardinality": "one",
                    "kind": "gameObject"
                  },
                  "actions": [
                    {
                      "values": { "spawnInterval": 3.0 },
                      "kind": "set"
                    }
                  ],
                  "commit": "context",
                  "kind": "edit"
                }
              ]
            }
            """);

        var result = IpcExecuteArgumentsContractReader.TryRead(
            document.RootElement,
            out var request,
            out var error);

        Assert.True(result, error.Message);
        var edit = Assert.Single(request.Steps).EditContract;
        Assert.NotNull(edit);
        Assert.Equal(IpcEditStepContract.ContextKind.Scene, edit!.Context.Kind);
        Assert.Equal(IpcEditStepContract.SelectionKind.Direct, edit.Selection.Kind);
        Assert.Equal("Game.Spawner, Assembly-CSharp", edit.Selection.ComponentType);
        Assert.Equal(IpcEditStepContract.ActionKind.Set, Assert.Single(edit.Actions).Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_TreatsExplicitNullOptionalActionTargetAsUnspecified ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "protocolVersion": 1,
              "steps": [
                {
                  "kind": "edit",
                  "on": {
                    "kind": "project"
                  },
                  "select": {
                    "kind": "projectAsset",
                    "path": "ProjectSettings/TagManager.asset",
                    "cardinality": "one"
                  },
                  "actions": [
                    {
                      "kind": "delete",
                      "target": null
                    }
                  ],
                  "commit": "project"
                }
              ]
            }
            """);

        var result = IpcExecuteArgumentsContractReader.TryRead(
            document.RootElement,
            out var request,
            out var error);

        Assert.True(result, error.Message);
        var edit = Assert.Single(request.Steps).EditContract;
        Assert.NotNull(edit);
        Assert.Equal(IpcEditStepContract.ContextKind.Project, edit!.Context.Kind);
        Assert.Null(Assert.Single(edit.Actions).Target);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_RejectsSelectionThatIsIncompatibleWithContext ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "protocolVersion": 1,
              "steps": [
                {
                  "kind": "edit",
                  "on": {
                    "kind": "project"
                  },
                  "select": {
                    "kind": "gameObject",
                    "path": "Root",
                    "cardinality": "one"
                  },
                  "actions": [
                    { "kind": "delete" }
                  ],
                  "commit": "context"
                }
              ]
            }
            """);

        var result = IpcExecuteArgumentsContractReader.TryRead(
            document.RootElement,
            out _,
            out var error);

        Assert.False(result);
        Assert.Equal(0, error.StepIndex);
        Assert.Contains("not supported by the selected edit context", error.Message, StringComparison.Ordinal);
    }
}
