using System.Text.Json;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Assurance;

public sealed class AssuranceVerdictContractTests
{
    private static readonly Guid RunId = Guid.Parse("fedcba98-7654-3210-fedc-ba9876543210");
    private static readonly Sha256Digest ProfileDigest = Sha256Digest.Parse(new string('a', 64));
    private static readonly ProjectFingerprint ProjectFingerprint = new(new string('b', 64));

    [Fact]
    [Trait("Size", "Small")]
    public void ContractLiterals_AreStable ()
    {
        Assert.Equal(
            ["pass", "fail", "incomplete"],
            ContractLiteralCodec.GetLiterals<AssuranceVerdict>());
        Assert.False(ContractLiteralCodec.IsDefined(default(AssuranceVerdict)));
        Assert.Equal(
            ["daemon", "transientProbe", "artifactOnly"],
            ContractLiteralCodec.GetLiterals<AssuranceSessionKind>());
        Assert.False(ContractLiteralCodec.IsDefined(default(AssuranceSessionKind)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CompileStartedEntry_WithUndefinedSessionKind_ThrowsArgumentOutOfRangeException ()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(static () => new CompileStartedEntry(
            RunId,
            ProjectFingerprint,
            AssuranceRequestedExecutionMode.Auto,
            AssuranceResolvedExecutionMode.Oneshot,
            default,
            1000));

        Assert.Equal("SessionKind", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ProgressEntries_WithUndefinedVerdict_ThrowArgumentOutOfRangeException ()
    {
        const AssuranceVerdict InvalidVerdict = (AssuranceVerdict)0;
        var constructors = new Action[]
        {
            static () => new BuildProgressEntry(RunId, ProfileDigest, BuildRunProgressPhase.Completed, null, null, InvalidVerdict, [], null),
            static () => new CompileCompletedEntry(RunId, InvalidVerdict, 0, 0, "summary.json", "diagnostics.json"),
            static () => new VerifyProgressEntry(VerifyProfileSource.BuiltIn, "default", null, ProfileDigest, 1, InvalidVerdict),
        };

        Assert.All(
            constructors,
            constructor => Assert.Equal("Verdict", Assert.Throws<ArgumentOutOfRangeException>(constructor).ParamName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ProgressEntries_WithNegativeCounts_ThrowArgumentOutOfRangeException ()
    {
        var testCases = new (Action Construct, string ParameterName)[]
        {
            (static () => new CompileCompletedEntry(RunId, AssuranceVerdict.Pass, -1, 0, "summary.json", "diagnostics.json"), "ErrorCount"),
            (static () => new CompileCompletedEntry(RunId, AssuranceVerdict.Pass, 0, -1, "summary.json", "diagnostics.json"), "WarningCount"),
            (static () => new VerifyProgressEntry(VerifyProfileSource.BuiltIn, "default", null, ProfileDigest, -1, null), "StepCount"),
        };

        Assert.All(
            testCases,
            testCase => Assert.Equal(
                testCase.ParameterName,
                Assert.Throws<ArgumentOutOfRangeException>(testCase.Construct).ParamName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CompileCompletedEntry_WithoutVerdict_FailsDeserialization ()
    {
        using var document = JsonDocument.Parse(
            $$"""
            {
              "runId": "{{RunId:D}}",
              "errorCount": 0,
              "warningCount": 0,
              "summaryJsonPath": "summary.json",
              "diagnosticsJsonPath": "diagnostics.json"
            }
            """);

        var success = IpcPayloadCodec.TryDeserialize<CompileCompletedEntry>(
            document.RootElement,
            out _,
            out var error);

        Assert.False(success);
        Assert.Equal(IpcPayloadReadErrorKind.DeserializeFailed, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CompileStartedEntry_WithoutSessionKind_FailsDeserialization ()
    {
        using var document = JsonDocument.Parse(
            $$"""
            {
              "runId": "{{RunId:D}}",
              "projectFingerprint": "{{ProjectFingerprint}}",
              "requestedMode": "auto",
              "resolvedMode": "oneshot",
              "timeoutMilliseconds": 1000
            }
            """);

        var success = IpcPayloadCodec.TryDeserialize<CompileStartedEntry>(
            document.RootElement,
            out _,
            out var error);

        Assert.False(success);
        Assert.Equal(IpcPayloadReadErrorKind.DeserializeFailed, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void VerifyProgressEntry_SerializesVerdictAsContractLiteral ()
    {
        var json = IpcPayloadCodec.SerializeToElement(new VerifyProgressEntry(
            VerifyProfileSource.BuiltIn,
            "default",
            null,
            ProfileDigest,
            1,
            AssuranceVerdict.Incomplete));

        Assert.Equal("incomplete", json.GetProperty("verdict").GetString());
    }
}
