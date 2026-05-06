using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Contracts.Ipc.Validation;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.ContractReading;

public sealed class IpcRequestContractReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_PermissivePreflight_AllowsMissingHeadersAndSteps ()
    {
        using var document = JsonDocument.Parse("{}");

        var result = IpcRequestContractReader.TryRead(
            requestObject: document.RootElement,
            profile: IpcRequestContractReadProfile.PermissivePreflight,
            requestContract: out var parsedDocument,
            error: out var error);

        Assert.True(result);
        Assert.Equal(IpcRequestContractReadErrorKind.None, error.Kind);
        Assert.Equal(0, parsedDocument.ProtocolVersion);
        Assert.Null(parsedDocument.RequestId);
        Assert.Null(parsedDocument.Steps);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_PermissivePreflight_StoresNullForNonObjectStep ()
    {
        using var document = JsonDocument.Parse("""{"steps":[1,{"kind":"op"}]}""");

        var result = IpcRequestContractReader.TryRead(
            requestObject: document.RootElement,
            profile: IpcRequestContractReadProfile.PermissivePreflight,
            requestContract: out var parsedDocument,
            error: out var error);

        Assert.True(result);
        Assert.Equal(IpcRequestContractReadErrorKind.None, error.Kind);
        Assert.NotNull(parsedDocument.Steps);
        Assert.Equal(2, parsedDocument.Steps.Count);
        Assert.Null(parsedDocument.Steps[0]);
        Assert.NotNull(parsedDocument.Steps[1]);
        Assert.Equal(IpcRequestStepKind.Op, parsedDocument.Steps[1]!.Kind);
        Assert.Null(parsedDocument.Steps[1]!.Id);
        Assert.Null(parsedDocument.Steps[1]!.OperationName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_PermissivePreflight_ReturnsProtocolVersionTypeMismatch_WhenProtocolVersionIsNonInteger ()
    {
        using var document = JsonDocument.Parse("""{"protocolVersion":"1"}""");

        var result = IpcRequestContractReader.TryRead(
            requestObject: document.RootElement,
            profile: IpcRequestContractReadProfile.PermissivePreflight,
            requestContract: out _,
            error: out var error);

        Assert.False(result);
        Assert.Equal(IpcRequestContractReadErrorKind.ProtocolVersionTypeMismatch, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_PermissivePreflight_ReturnsRequestIdTypeMismatch_WhenRequestIdIsNonString ()
    {
        using var document = JsonDocument.Parse("""{"requestId":1}""");

        var result = IpcRequestContractReader.TryRead(
            requestObject: document.RootElement,
            profile: IpcRequestContractReadProfile.PermissivePreflight,
            requestContract: out _,
            error: out var error);

        Assert.False(result);
        Assert.Equal(IpcRequestContractReadErrorKind.RequestIdContractViolation, error.Kind);
        Assert.Equal(JsonStringReadErrorKind.TypeMismatch, error.JsonStringReadError.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_StrictExecute_ReturnsFormatError_WhenRequestIdIsNotCanonicalGuid ()
    {
        using var document = JsonDocument.Parse("""{"protocolVersion":1,"requestId":"invalid","steps":[]}""");

        var result = IpcRequestContractReader.TryRead(
            requestObject: document.RootElement,
            profile: IpcRequestContractReadProfile.StrictExecute,
            requestContract: out _,
            error: out var error);

        Assert.False(result);
        Assert.Equal(IpcRequestContractReadErrorKind.RequestIdFormatMismatch, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_StrictExecute_ReturnsDuplicatedStepIdError_WhenStepIdIsDuplicated ()
    {
        using var document = JsonDocument.Parse(
            """
            {"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","steps":[{"kind":"op","id":"same","op":"__RESOLVE_OP__","args":{}},{"kind":"op","id":"same","op":"__RESOLVE_OP__","args":{}}]}
            """
                .Replace("__RESOLVE_OP__", UcliPrimitiveOperationNames.Resolve, StringComparison.Ordinal));

        var result = IpcRequestContractReader.TryRead(
            requestObject: document.RootElement,
            profile: IpcRequestContractReadProfile.StrictExecute,
            requestContract: out _,
            error: out var error);

        Assert.False(result);
        Assert.Equal(IpcRequestContractReadErrorKind.DuplicatedStepId, error.Kind);
        Assert.Equal(1, error.StepIndex);
        Assert.Equal("same", error.DuplicatedStepId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_StrictExecute_NormalizesRequestIdAndReadsOpStep ()
    {
        using var document = JsonDocument.Parse(
            """
            {"protocolVersion":1,"requestId":"9B0E6D1E-3F55-4A6B-8C66-5B9A3A7C9C62","steps":[{"kind":"op","id":"op-1","op":"__RESOLVE_OP__","args":{}}]}
            """
                .Replace("__RESOLVE_OP__", UcliPrimitiveOperationNames.Resolve, StringComparison.Ordinal));

        var result = IpcRequestContractReader.TryRead(
            requestObject: document.RootElement,
            profile: IpcRequestContractReadProfile.StrictExecute,
            requestContract: out var parsedDocument,
            error: out var error);

        Assert.True(result);
        Assert.Equal(IpcRequestContractReadErrorKind.None, error.Kind);
        Assert.Equal("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62", parsedDocument.RequestId);
        Assert.NotNull(parsedDocument.Steps);
        var step = Assert.Single(parsedDocument.Steps!);
        Assert.NotNull(step);
        Assert.Equal(IpcRequestStepKind.Op, step!.Kind);
        Assert.Equal("op-1", step.Id);
        Assert.Equal(UcliPrimitiveOperationNames.Resolve, step.OperationName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_StrictExecute_ReadsEditStep ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "protocolVersion": 1,
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
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

        var result = IpcRequestContractReader.TryRead(
            requestObject: document.RootElement,
            profile: IpcRequestContractReadProfile.StrictExecute,
            requestContract: out var parsedDocument,
            error: out var error);

        Assert.True(result);
        Assert.Equal(IpcRequestContractReadErrorKind.None, error.Kind);
        var step = Assert.Single(parsedDocument.Steps!);
        Assert.NotNull(step);
        Assert.Equal(IpcRequestStepKind.Edit, step!.Kind);
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
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
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

        var result = IpcRequestContractReader.TryRead(
            requestObject: document.RootElement,
            profile: IpcRequestContractReadProfile.StrictExecute,
            requestContract: out _,
            error: out var error);

        Assert.False(result);
        Assert.Equal(IpcRequestContractReadErrorKind.StepSelectContractViolation, error.Kind);
        Assert.Equal(0, error.StepIndex);
        Assert.Equal("edit-1", error.StepId);
        Assert.Equal(StepPropertyReadErrorKind.TypeMismatch, error.StepPropertyReadErrorKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_StrictExecute_ReturnsUnknownRequestPropertyError_WhenUnknownPropertyExists ()
    {
        using var document = JsonDocument.Parse(
            """{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","steps":[],"unknown":true}""");

        var result = IpcRequestContractReader.TryRead(
            requestObject: document.RootElement,
            profile: IpcRequestContractReadProfile.StrictExecute,
            requestContract: out _,
            error: out var error);

        Assert.False(result);
        Assert.Equal(IpcRequestContractReadErrorKind.UnknownRequestProperty, error.Kind);
        Assert.Equal("unknown", error.UnknownPropertyName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_StrictExecute_ReadsEditStepWithDirectSelection ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "protocolVersion": 1,
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
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

        var result = IpcRequestContractReader.TryRead(
            requestObject: document.RootElement,
            profile: IpcRequestContractReadProfile.StrictExecute,
            requestContract: out var parsedDocument,
            error: out var error);

        Assert.True(result);
        Assert.Equal(IpcRequestContractReadErrorKind.None, error.Kind);
        Assert.NotNull(parsedDocument.Steps);
        var step = Assert.Single(parsedDocument.Steps!);
        Assert.NotNull(step);
        Assert.Equal(IpcRequestStepKind.Edit, step!.Kind);
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
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
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

        var result = IpcRequestContractReader.TryRead(
            requestObject: document.RootElement,
            profile: IpcRequestContractReadProfile.StrictExecute,
            requestContract: out _,
            error: out var error);

        Assert.False(result);
        Assert.Equal(IpcRequestContractReadErrorKind.StepEditContractViolation, error.Kind);
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
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
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

        var result = IpcRequestContractReader.TryRead(
            requestObject: document.RootElement,
            profile: IpcRequestContractReadProfile.StrictExecute,
            requestContract: out _,
            error: out var error);

        Assert.False(result);
        Assert.Equal(IpcRequestContractReadErrorKind.StepEditContractViolation, error.Kind);
        Assert.Equal(0, error.StepIndex);
        Assert.Equal("edit-query-extra", error.StepId);
        Assert.Equal("Edit step property 'step.select.from' contains an unknown property: extra.", error.DiagnosticMessage);
    }
}
