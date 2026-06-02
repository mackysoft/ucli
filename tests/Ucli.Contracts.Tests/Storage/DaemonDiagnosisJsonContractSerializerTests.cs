using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Storage;

public sealed class DaemonDiagnosisJsonContractSerializerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Deserialize_WithValidJson_ReturnsContract ()
    {
        const string Json = """
            {
              "reason": "shutdownRequested",
              "message": "daemon shutdown completed",
              "reportedBy": "unity",
              "isInferred": false,
              "updatedAtUtc": "2026-03-09T00:00:00+00:00",
              "processId": 1234,
              "editorInstancePath": "/repo/UnityProject/Library/EditorInstance.json",
              "sessionIssuedAtUtc": "2026-03-09T00:00:01+00:00",
              "processStartedAtUtc": "2026-03-09T00:00:02+00:00",
              "unityLogPath": "/repo/.ucli/unity.log",
              "startupPhase": "scriptCompilation",
              "actionRequired": "fixCompileErrors",
              "primaryDiagnostic": {
                "kind": "compiler",
                "code": "CS1739",
                "file": "Assets/Foo.cs",
                "line": 74,
                "column": 17,
                "message": "Missing parameter"
              }
            }
            """;

        var contract = DaemonDiagnosisJsonContractSerializer.Deserialize(Json);

        Assert.NotNull(contract);
        Assert.Equal("shutdownRequested", contract.Reason);
        Assert.Equal("daemon shutdown completed", contract.Message);
        Assert.Equal("unity", contract.ReportedBy);
        Assert.False(contract.IsInferred);
        Assert.Equal(DateTimeOffset.Parse("2026-03-09T00:00:00+00:00"), contract.UpdatedAtUtc);
        Assert.Equal(1234, contract.ProcessId);
        Assert.Equal("/repo/UnityProject/Library/EditorInstance.json", contract.EditorInstancePath);
        Assert.Equal(DateTimeOffset.Parse("2026-03-09T00:00:01+00:00"), contract.SessionIssuedAtUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-03-09T00:00:02+00:00"), contract.ProcessStartedAtUtc);
        Assert.Equal("/repo/.ucli/unity.log", contract.UnityLogPath);
        Assert.Equal("scriptCompilation", contract.StartupPhase);
        Assert.Equal("fixCompileErrors", contract.ActionRequired);
        Assert.NotNull(contract.PrimaryDiagnostic);
        Assert.Equal("compiler", contract.PrimaryDiagnostic!.Kind);
        Assert.Equal("CS1739", contract.PrimaryDiagnostic.Code);
        Assert.Equal("Assets/Foo.cs", contract.PrimaryDiagnostic.File);
        Assert.Equal(74, contract.PrimaryDiagnostic.Line);
        Assert.Equal(17, contract.PrimaryDiagnostic.Column);
        Assert.Equal("Missing parameter", contract.PrimaryDiagnostic.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Deserialize_WithNullLiteral_ReturnsNull ()
    {
        var contract = DaemonDiagnosisJsonContractSerializer.Deserialize("null");

        Assert.Null(contract);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Deserialize_WithWhitespace_ThrowsArgumentException ()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            DaemonDiagnosisJsonContractSerializer.Deserialize(" ");
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Serialize_WithContract_WritesCamelCaseFields ()
    {
        var contract = new DaemonDiagnosisJsonContract(
            Reason: "shutdownRequested",
            Message: "daemon shutdown completed",
            ReportedBy: DaemonDiagnosisReportedByValues.Unity,
            IsInferred: false,
            UpdatedAtUtc: DateTimeOffset.Parse("2026-03-09T00:00:00+00:00"),
            ProcessId: 1234,
            EditorInstancePath: null,
            SessionIssuedAtUtc: DateTimeOffset.Parse("2026-03-09T00:00:01+00:00"),
            ProcessStartedAtUtc: DateTimeOffset.Parse("2026-03-09T00:00:02+00:00"),
            UnityLogPath: "/repo/.ucli/unity.log",
            StartupPhase: ContractLiteralCodec.ToValue(DaemonDiagnosisStartupPhase.ScriptCompilation),
            ActionRequired: DaemonDiagnosisActionRequiredValues.FixCompileErrors,
            PrimaryDiagnostic: new DaemonDiagnosisPrimaryDiagnosticJsonContract(
                Kind: DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler,
                Code: "CS1739",
                File: "Assets/Foo.cs",
                Line: 74,
                Column: 17,
                Message: "Missing parameter"));

        var json = DaemonDiagnosisJsonContractSerializer.Serialize(contract);
        using var jsonDocument = JsonDocument.Parse(json);

        JsonAssert.For(jsonDocument.RootElement)
            .MatchesSchema(DiagnosisJsonSchema, nameof(DiagnosisJsonSchema));
    }

    private static JsonSchemaNode DiagnosisJsonSchema => JsonSchemaNode.Object(
        builder => builder
            .Required("reason", JsonSchemaNode.Value(JsonSchemaType.String))
            .Required("message", JsonSchemaNode.Value(JsonSchemaType.String))
            .Required("reportedBy", JsonSchemaNode.Value(JsonSchemaType.String))
            .Required("isInferred", JsonSchemaNode.Value(JsonSchemaType.Boolean))
            .Required("updatedAtUtc", JsonSchemaNode.Value(JsonSchemaType.String))
            .Required("processId", JsonSchemaNode.Value(JsonSchemaType.Int32))
            .Required("editorInstancePath", JsonSchemaNode.Union(JsonSchemaType.String, JsonSchemaType.Null))
            .Required("sessionIssuedAtUtc", JsonSchemaNode.Value(JsonSchemaType.String))
            .Required("processStartedAtUtc", JsonSchemaNode.Union(JsonSchemaType.String, JsonSchemaType.Null))
            .Required("unityLogPath", JsonSchemaNode.Union(JsonSchemaType.String, JsonSchemaType.Null))
            .Required("startupPhase", JsonSchemaNode.Union(JsonSchemaType.String, JsonSchemaType.Null))
            .Required("actionRequired", JsonSchemaNode.Union(JsonSchemaType.String, JsonSchemaType.Null))
            .RequiredObject(
                "primaryDiagnostic",
                builder => builder
                    .Required("kind", JsonSchemaNode.Union(JsonSchemaType.String, JsonSchemaType.Null))
                    .Required("code", JsonSchemaNode.Union(JsonSchemaType.String, JsonSchemaType.Null))
                    .Required("file", JsonSchemaNode.Union(JsonSchemaType.String, JsonSchemaType.Null))
                    .Required("line", JsonSchemaNode.Union(JsonSchemaType.Int32, JsonSchemaType.Null))
                    .Required("column", JsonSchemaNode.Union(JsonSchemaType.Int32, JsonSchemaType.Null))
                    .Required("message", JsonSchemaNode.Union(JsonSchemaType.String, JsonSchemaType.Null)),
                allowAdditionalProperties: false),
        allowAdditionalProperties: false);
}
