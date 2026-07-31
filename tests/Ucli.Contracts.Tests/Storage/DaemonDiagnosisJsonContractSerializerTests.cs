using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Editor;

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
        Assert.Equal(DaemonDiagnosisReason.ShutdownRequested, contract.Reason);
        Assert.Equal("daemon shutdown completed", contract.Message);
        Assert.Equal(DaemonDiagnosisReportedBy.Unity, contract.ReportedBy);
        Assert.False(contract.IsInferred);
        Assert.Equal(DateTimeOffset.Parse("2026-03-09T00:00:00+00:00"), contract.UpdatedAtUtc);
        Assert.Equal(1234, contract.ProcessId);
        Assert.Equal("/repo/UnityProject/Library/EditorInstance.json", contract.EditorInstancePath);
        Assert.Equal(DateTimeOffset.Parse("2026-03-09T00:00:01+00:00"), contract.SessionIssuedAtUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-03-09T00:00:02+00:00"), contract.ProcessStartedAtUtc);
        Assert.Equal("/repo/.ucli/unity.log", contract.UnityLogPath);
        Assert.Equal(DaemonDiagnosisStartupPhase.ScriptCompilation, contract.StartupPhase);
        Assert.Equal(UnityEditorActionRequired.FixCompileErrors, contract.ActionRequired);
        Assert.NotNull(contract.PrimaryDiagnostic);
        Assert.Equal(UnityEditorPrimaryDiagnosticKind.Compiler, contract.PrimaryDiagnostic!.Kind);
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
    public void Deserialize_WithUnknownFiniteLiteral_ThrowsJsonException ()
    {
        const string Json = """
            {
              "reason": "unknown",
              "message": "diagnosis",
              "reportedBy": "unity",
              "isInferred": false,
              "updatedAtUtc": "2026-03-09T00:00:00+00:00",
              "processId": null,
              "editorInstancePath": null,
              "sessionIssuedAtUtc": "2026-03-09T00:00:01+00:00",
              "processStartedAtUtc": null,
              "unityLogPath": null,
              "startupPhase": null,
              "actionRequired": null,
              "primaryDiagnostic": null
            }
            """;

        Assert.Throws<JsonException>(() => DaemonDiagnosisJsonContractSerializer.Deserialize(Json));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Deserialize_WhenRequiredFiniteLiteralIsMissing_ThrowsJsonException ()
    {
        const string Json = """
            {
              "message": "diagnosis",
              "reportedBy": "unity",
              "isInferred": false,
              "updatedAtUtc": "2026-03-09T00:00:00+00:00",
              "processId": null,
              "editorInstancePath": null,
              "sessionIssuedAtUtc": "2026-03-09T00:00:01+00:00",
              "processStartedAtUtc": null,
              "unityLogPath": null,
              "startupPhase": null,
              "actionRequired": null,
              "primaryDiagnostic": null
            }
            """;

        Assert.Throws<JsonException>(() => DaemonDiagnosisJsonContractSerializer.Deserialize(Json));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Serialize_WithContract_WritesCamelCaseFields ()
    {
        var contract = new DaemonDiagnosisJsonContract(
            Reason: DaemonDiagnosisReason.ShutdownRequested,
            Message: "daemon shutdown completed",
            ReportedBy: DaemonDiagnosisReportedBy.Unity,
            IsInferred: false,
            UpdatedAtUtc: DateTimeOffset.Parse("2026-03-09T00:00:00+00:00"),
            ProcessId: 1234,
            EditorInstancePath: null,
            SessionIssuedAtUtc: DateTimeOffset.Parse("2026-03-09T00:00:01+00:00"),
            ProcessStartedAtUtc: DateTimeOffset.Parse("2026-03-09T00:00:02+00:00"),
            UnityLogPath: "/repo/.ucli/unity.log",
            StartupPhase: DaemonDiagnosisStartupPhase.ScriptCompilation,
            ActionRequired: UnityEditorActionRequired.FixCompileErrors,
            PrimaryDiagnostic: new DaemonDiagnosisPrimaryDiagnosticJsonContract(
                Kind: UnityEditorPrimaryDiagnosticKind.Compiler,
                Code: "CS1739",
                File: "Assets/Foo.cs",
                Line: 74,
                Column: 17,
                Message: "Missing parameter"));

        var json = DaemonDiagnosisJsonContractSerializer.Serialize(contract);
        using var jsonDocument = JsonDocument.Parse(json);

        var root = jsonDocument.RootElement;
        Assert.Equal(
            [
                "actionRequired",
                "editorInstancePath",
                "isInferred",
                "message",
                "primaryDiagnostic",
                "processId",
                "processStartedAtUtc",
                "reason",
                "reportedBy",
                "sessionIssuedAtUtc",
                "startupPhase",
                "unityLogPath",
                "updatedAtUtc",
            ],
            root.EnumerateObject()
                .Select(static property => property.Name)
                .Order(StringComparer.Ordinal)
                .ToArray());
        JsonAssert.For(root)
            .HasString("reason", "shutdownRequested")
            .HasString("reportedBy", "unity")
            .HasProperty("primaryDiagnostic", static diagnostic => diagnostic
                .HasString("kind", "compiler")
                .HasString("code", "CS1739"));
    }
}
