using System.Text.Json;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Assurance;

public sealed class CompileVerifyFiniteContractTests
{
    private static readonly Sha256Digest ProfileDigest = Sha256Digest.Parse(
        "1111111111111111111111111111111111111111111111111111111111111111");

    [Fact]
    [Trait("Size", "Small")]
    public void FiniteValues_UseCanonicalContractLiterals ()
    {
        Assert.Equal("builtIn", ContractLiteralCodec.ToValue(VerifyProfileSource.BuiltIn));
        Assert.Equal("postRead", ContractLiteralCodec.ToValue(VerifyStepKind.PostRead));
        Assert.Equal("unityTestRunner", ContractLiteralCodec.ToValue(AssuranceEffect.UnityTestRunner));
        Assert.Equal("error", ContractLiteralCodec.ToValue(VerifyDiagnosticImpact.Error));
        Assert.Equal("diagnosticsRead", ContractLiteralCodec.ToValue(CompileRefreshOrigin.DiagnosticsRead));
        Assert.Equal("lifecycleSnapshot", ContractLiteralCodec.ToValue(CompileEvidenceKind.LifecycleSnapshot));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ProgressContracts_WithUndefinedFiniteValues_ThrowArgumentOutOfRangeException ()
    {
        Assert.Equal(
            "ProfileSource",
            Assert.Throws<ArgumentOutOfRangeException>(() => new VerifyProgressEntry(
                default,
                "default",
                null,
                ProfileDigest,
                1,
                null)).ParamName);
        Assert.Equal(
            "Kind",
            Assert.Throws<ArgumentOutOfRangeException>(() => new VerifyStepProgressEntry(
                default,
                Required: true,
                Effects: [],
                SkipReason: null)).ParamName);
        Assert.Equal(
            "Severity",
            Assert.Throws<ArgumentOutOfRangeException>(() => new VerifyDiagnosticEntry(
                "VERIFY_STUB",
                "stub diagnostic",
                default,
                VerifyStepKind.Compile)).ParamName);
        Assert.Equal(
            "RefreshOrigin",
            Assert.Throws<ArgumentOutOfRangeException>(() => new CompileRefreshStartedEntry(
                Guid.NewGuid(),
                default,
                "hostDispatch")).ParamName);
        Assert.Equal(
            "Origin",
            Assert.Throws<ArgumentOutOfRangeException>(() => new IpcCompileSummary.RefreshEvidence(
                default,
                Requested: true,
                StartedAtUtc: DateTimeOffset.UtcNow,
                CompletedAtUtc: null,
                Completed: false)).ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void VerifyStepProgressEntry_SnapshotsEffects ()
    {
        var effects = new[]
        {
            AssuranceEffect.ScriptCompilation,
        };
        var entry = new VerifyStepProgressEntry(
            VerifyStepKind.Compile,
            Required: true,
            effects,
            SkipReason: null);

        effects[0] = AssuranceEffect.UnityTestRunner;

        Assert.Equal(new[] { AssuranceEffect.ScriptCompilation }, entry.Effects);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ProgressContracts_SerializeFiniteValuesAsContractLiterals ()
    {
        var profile = IpcPayloadCodec.SerializeToElement(new VerifyProgressEntry(
            VerifyProfileSource.File,
            "project",
            "verify.json",
            ProfileDigest,
            1,
            null));
        var step = IpcPayloadCodec.SerializeToElement(new VerifyStepProgressEntry(
            VerifyStepKind.Test,
            Required: true,
            Effects: [AssuranceEffect.UnityTestRunner],
            SkipReason: null));
        var refresh = IpcPayloadCodec.SerializeToElement(new CompileRefreshStartedEntry(
            Guid.NewGuid(),
            CompileRefreshOrigin.AssetDatabaseRefresh,
            "hostDispatch"));

        Assert.Equal("file", profile.GetProperty("profileSource").GetString());
        Assert.Equal("test", step.GetProperty("kind").GetString());
        Assert.Equal("unityTestRunner", step.GetProperty("effects")[0].GetString());
        Assert.Equal("assetDatabaseRefresh", refresh.GetProperty("refreshOrigin").GetString());
    }

    [Theory]
    [InlineData("""{"required":true,"effects":[],"skipReason":null}""")]
    [InlineData("""{"kind":"ready","required":true,"skipReason":null}""")]
    [InlineData("""{"kind":"external","required":true,"effects":[],"skipReason":null}""")]
    [Trait("Size", "Small")]
    public void VerifyStepProgressEntry_WithMissingOrUnknownFiniteValues_FailsDeserialization (string json)
    {
        using var document = JsonDocument.Parse(json);

        var success = IpcPayloadCodec.TryDeserialize<VerifyStepProgressEntry>(
            document.RootElement,
            out _,
            out var error);

        Assert.False(success);
        Assert.Equal(IpcPayloadReadErrorKind.DeserializeFailed, error.Kind);
    }
}
