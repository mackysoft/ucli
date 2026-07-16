using System.Text.Json;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations;

public sealed class CsEvalContractTests
{
    private static readonly Sha256Digest Digest = Sha256Digest.Parse(new string('a', 64));

    [Fact]
    [Trait("Size", "Small")]
    public void FiniteVocabularies_HaveStableLiteralsAndRejectDefaultValues ()
    {
        Assert.Equal(["succeeded", "failed"], ContractLiteralCodec.GetLiterals<CsEvalCompileStatus>());
        Assert.Equal(["info", "warning", "error"], ContractLiteralCodec.GetLiterals<UcliDiagnosticSeverity>());
        Assert.Equal(["log", "warning", "error"], ContractLiteralCodec.GetLiterals<CsEvalLogLevel>());
        Assert.Equal(["null", "json"], ContractLiteralCodec.GetLiterals<CsEvalReturnValueKind>());
        Assert.Equal(["unknown", "none", "declared"], ContractLiteralCodec.GetLiterals<CsEvalTouchedResourceState>());
        Assert.Equal(["csharp"], ContractLiteralCodec.GetLiterals<UcliCodeLanguage>());
        Assert.Equal(["compilationUnit", "snippet"], ContractLiteralCodec.GetLiterals<UcliCodeSourceFormKind>());
        Assert.Equal(["property", "method"], ContractLiteralCodec.GetLiterals<UcliCodeApiMemberKind>());

        Assert.False(ContractLiteralCodec.IsDefined(default(CsEvalCompileStatus)));
        Assert.False(ContractLiteralCodec.IsDefined(default(UcliDiagnosticSeverity)));
        Assert.False(ContractLiteralCodec.IsDefined(default(CsEvalLogLevel)));
        Assert.False(ContractLiteralCodec.IsDefined(default(CsEvalReturnValueKind)));
        Assert.False(ContractLiteralCodec.IsDefined(default(CsEvalTouchedResourceState)));
        Assert.False(ContractLiteralCodec.IsDefined(default(UcliCodeLanguage)));
        Assert.False(ContractLiteralCodec.IsDefined(default(UcliCodeSourceFormKind)));
        Assert.False(ContractLiteralCodec.IsDefined(default(UcliCodeApiMemberKind)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructors_WhenRequiredFiniteValueIsUndefined_ThrowArgumentOutOfRangeException ()
    {
        var cases = new (Action Construct, string ParameterName)[]
        {
            (static () => new CsEvalCompileResult(default, []), "status"),
            (static () => new CsEvalDiagnostic(default, "CS0001", "message", null, null), "severity"),
            (static () => new CsEvalLogEntry(default, "message"), "level"),
            (static () => new CsEvalReturnValue(default, null), "kind"),
            (static () => new CsEvalTouchedResources(default, null), "state"),
            (static () => CreateResult((UcliCodeSourceFormKind)int.MaxValue), "sourceKind"),
        };

        Assert.All(
            cases,
            testCase => Assert.Equal(
                testCase.ParameterName,
                Assert.Throws<ArgumentOutOfRangeException>(testCase.Construct).ParamName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CsEvalReturnValue_WhenKindAndValueDisagree_ThrowsArgumentException ()
    {
        JsonElement jsonValue = JsonSerializer.SerializeToElement(new { ok = true });
        var cases = new Action[]
        {
            () => new CsEvalReturnValue(CsEvalReturnValueKind.Null, jsonValue),
            static () => new CsEvalReturnValue(CsEvalReturnValueKind.Json, null),
        };

        Assert.All(
            cases,
            construct => Assert.Equal("value", Assert.Throws<ArgumentException>(construct).ParamName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CsEvalTouchedResources_WhenStateAndDeclarationsDisagree_ThrowsArgumentException ()
    {
        var declaration = new CsEvalTouchedResourceDeclaration(
            UcliTouchedResourceKind.Asset,
            "Assets/Data.asset");
        var cases = new Action[]
        {
            static () => new CsEvalTouchedResources(CsEvalTouchedResourceState.Declared, null),
            static () => new CsEvalTouchedResources(CsEvalTouchedResourceState.Declared, []),
            () => new CsEvalTouchedResources(CsEvalTouchedResourceState.None, [declaration]),
            () => new CsEvalTouchedResources(CsEvalTouchedResourceState.Unknown, [declaration]),
        };

        Assert.All(
            cases,
            construct => Assert.Equal("declared", Assert.Throws<ArgumentException>(construct).ParamName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CsEvalDiagnostic_WhenSourcePositionIsPartialOrNonPositive_ThrowsArgumentException ()
    {
        var cases = new Action[]
        {
            static () => new CsEvalDiagnostic(UcliDiagnosticSeverity.Error, "CS0001", "message", 1, null),
            static () => new CsEvalDiagnostic(UcliDiagnosticSeverity.Error, "CS0001", "message", null, 1),
            static () => new CsEvalDiagnostic(UcliDiagnosticSeverity.Error, "CS0001", "message", 0, 1),
            static () => new CsEvalDiagnostic(UcliDiagnosticSeverity.Error, "CS0001", "message", 1, 0),
        };

        Assert.All(cases, construct => Assert.ThrowsAny<ArgumentException>(construct));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CollectionContracts_SnapshotCallerOwnedCollections ()
    {
        var diagnostic = new CsEvalDiagnostic(UcliDiagnosticSeverity.Warning, "CS0001", "message", null, null);
        var diagnostics = new[] { diagnostic };
        var compile = new CsEvalCompileResult(CsEvalCompileStatus.Succeeded, diagnostics);

        var log = new CsEvalLogEntry(CsEvalLogLevel.Log, "message");
        var logs = new[] { log };
        var result = CreateResult(UcliCodeSourceFormKind.Snippet, compile, logs);

        var declaration = new CsEvalTouchedResourceDeclaration(UcliTouchedResourceKind.Asset, "Assets/Data.asset");
        var declarations = new[] { declaration };
        var touched = new CsEvalTouchedResources(CsEvalTouchedResourceState.Declared, declarations);

        diagnostics[0] = null!;
        logs[0] = null!;
        declarations[0] = null!;

        Assert.Same(diagnostic, Assert.Single(compile.Diagnostics));
        Assert.Same(log, Assert.Single(result.Logs!));
        Assert.Same(declaration, Assert.Single(touched.Declared!));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CsEvalCompileResult_WithoutRequiredStatus_FailsDeserialization ()
    {
        using var document = JsonDocument.Parse("""
            {
              "diagnostics": []
            }
            """);

        var success = IpcPayloadCodec.TryDeserialize<CsEvalCompileResult>(
            document.RootElement,
            out _,
            out var error);

        Assert.False(success);
        Assert.Equal(IpcPayloadReadErrorKind.DeserializeFailed, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CsEvalResult_SerializesFiniteValuesAsContractLiterals ()
    {
        var result = CreateResult(
            UcliCodeSourceFormKind.Snippet,
            new CsEvalCompileResult(CsEvalCompileStatus.Succeeded, []),
            [new CsEvalLogEntry(CsEvalLogLevel.Warning, "message")]);

        var json = IpcPayloadCodec.SerializeToElement(result);

        Assert.Equal("snippet", json.GetProperty("sourceKind").GetString());
        Assert.Equal("succeeded", json.GetProperty("compile").GetProperty("status").GetString());
        Assert.Equal("warning", json.GetProperty("logs")[0].GetProperty("level").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [Trait("Size", "Small")]
    public void CsEvalArgs_WhenSourceHasNoContent_ThrowsArgumentException (string? source)
    {
        Assert.ThrowsAny<ArgumentException>(() => new CsEvalArgs(source!));
    }

    private static CsEvalResult CreateResult (UcliCodeSourceFormKind sourceKind)
    {
        return CreateResult(
            sourceKind,
            new CsEvalCompileResult(CsEvalCompileStatus.Succeeded, []),
            logs: null);
    }

    private static CsEvalResult CreateResult (
        UcliCodeSourceFormKind sourceKind,
        CsEvalCompileResult compile,
        IReadOnlyList<CsEvalLogEntry>? logs)
    {
        return new CsEvalResult(
            Digest,
            sourceKind,
            "Entry.Run",
            Digest,
            compile,
            durationMilliseconds: null,
            logs,
            returnValue: null,
            touchedResources: null);
    }
}
