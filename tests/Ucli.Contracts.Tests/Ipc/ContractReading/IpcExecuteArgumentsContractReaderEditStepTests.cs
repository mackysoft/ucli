using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.ContractReading;

public sealed class IpcExecuteArgumentsContractReaderEditStepTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_StrictExecute_ReadsEditStep ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "protocolVersion": 1,
              "steps": [
                {
                  "kind": "edit",
                  "id": "edit-1",
                  "on": {
                    "scene": "Assets/Scenes/Main.unity"
                  },
                  "select": {
                    "gameObject": "Root/Spawner",
                    "component": "Game.EnemySpawner, Assembly-CSharp",
                    "cardinality": "one"
                  },
                  "actions": [
                    {
                      "kind": "set",
                      "values": {
                        "spawnInterval": 3.0
                      }
                    }
                  ],
                  "commit": "context"
                }
              ]
            }
            """);

        var result = IpcExecuteArgumentsContractReader.TryRead(
            argumentsObject: document.RootElement,
            profile: IpcExecuteArgumentsContractReadProfile.StrictExecute,
            argumentsContract: out var parsedArguments,
            error: out var error);

        Assert.True(result);
        Assert.Equal(IpcExecuteArgumentsContractReadErrorKind.None, error.Kind);
        var step = Assert.Single(parsedArguments.Steps!);
        Assert.NotNull(step);
        Assert.Equal(IpcExecuteStepKind.Edit, step!.Kind);
        Assert.Equal("edit-1", step.Id);
        Assert.Null(step.OperationName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_StrictExecute_ReturnsStepSelectTypeMismatch_WhenEditSelectIsNotObject ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "protocolVersion": 1,
              "steps": [
                {
                  "kind": "edit",
                  "id": "edit-1",
                  "on": {
                    "scene": "Assets/Scenes/Main.unity"
                  },
                  "select": [],
                  "actions": [
                    {
                      "kind": "delete"
                    }
                  ],
                  "commit": "context"
                }
              ]
            }
            """);

        var result = IpcExecuteArgumentsContractReader.TryRead(
            argumentsObject: document.RootElement,
            profile: IpcExecuteArgumentsContractReadProfile.StrictExecute,
            argumentsContract: out _,
            error: out var error);

        Assert.False(result);
        Assert.Equal(IpcExecuteArgumentsContractReadErrorKind.StepSelectContractViolation, error.Kind);
        Assert.Equal(0, error.StepIndex);
        Assert.Equal("edit-1", error.StepId);
        Assert.Equal(StepPropertyReadErrorKind.TypeMismatch, error.StepPropertyReadErrorKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_StrictExecute_ReadsEditStepWithDirectSelection ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "protocolVersion": 1,
              "steps": [
                {
                  "kind": "edit",
                  "id": "edit-1",
                  "on": {
                    "scene": "Assets/Scenes/Main.unity"
                  },
                  "select": {
                    "gameObject": "Root/Spawner",
                    "cardinality": "one"
                  },
                  "actions": [
                    {
                      "kind": "delete"
                    }
                  ],
                  "commit": "context"
                }
              ]
            }
            """);

        var result = IpcExecuteArgumentsContractReader.TryRead(
            argumentsObject: document.RootElement,
            profile: IpcExecuteArgumentsContractReadProfile.StrictExecute,
            argumentsContract: out var parsedArguments,
            error: out var error);

        Assert.True(result);
        Assert.Equal(IpcExecuteArgumentsContractReadErrorKind.None, error.Kind);
        Assert.NotNull(parsedArguments.Steps);
        var step = Assert.Single(parsedArguments.Steps!);
        Assert.NotNull(step);
        Assert.Equal(IpcExecuteStepKind.Edit, step!.Kind);
        Assert.Equal("edit-1", step.Id);
        Assert.Null(step.OperationName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_StrictExecute_ReturnsStepEditContractViolation_WhenEditCommitLiteralIsUnsupported ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "protocolVersion": 1,
              "steps": [
                {
                  "kind": "edit",
                  "id": "edit-unsupported-commit",
                  "on": {
                    "scene": "Assets/Scenes/Main.unity"
                  },
                  "select": {
                    "gameObject": "Root",
                    "cardinality": "one"
                  },
                  "actions": [
                    {
                      "kind": "delete"
                    }
                  ],
                  "commit": "later"
                }
              ]
            }
            """);

        var result = IpcExecuteArgumentsContractReader.TryRead(
            argumentsObject: document.RootElement,
            profile: IpcExecuteArgumentsContractReadProfile.StrictExecute,
            argumentsContract: out _,
            error: out var error);

        Assert.False(result);
        Assert.Equal(IpcExecuteArgumentsContractReadErrorKind.StepEditContractViolation, error.Kind);
        Assert.Equal(0, error.StepIndex);
        Assert.Equal("edit-unsupported-commit", error.StepId);
        Assert.Equal("Edit step property 'step.commit' must be one of 'none', 'context', or 'project'.", error.DiagnosticMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_StrictExecute_ReturnsStepEditContractViolation_WhenSelectFromContainsUnknownProperty ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "protocolVersion": 1,
              "steps": [
                {
                  "kind": "edit",
                  "id": "edit-query-extra",
                  "on": {
                    "scene": "Assets/Scenes/Main.unity"
                  },
                  "select": {
                    "from": {
                      "op": "__SCENE_QUERY_OP__",
                      "args": {
                        "pathPrefix": "Root"
                      },
                      "extra": 1
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
              ]
            }
            """
                .Replace("__SCENE_QUERY_OP__", UcliPrimitiveOperationNames.SceneQuery, StringComparison.Ordinal));

        var result = IpcExecuteArgumentsContractReader.TryRead(
            argumentsObject: document.RootElement,
            profile: IpcExecuteArgumentsContractReadProfile.StrictExecute,
            argumentsContract: out _,
            error: out var error);

        Assert.False(result);
        Assert.Equal(IpcExecuteArgumentsContractReadErrorKind.StepEditContractViolation, error.Kind);
        Assert.Equal(0, error.StepIndex);
        Assert.Equal("edit-query-extra", error.StepId);
        Assert.Equal("Edit step property 'step.select.from' contains an unknown property: extra.", error.DiagnosticMessage);
    }
}
