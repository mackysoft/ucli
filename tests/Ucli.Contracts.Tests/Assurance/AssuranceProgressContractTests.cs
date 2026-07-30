using System.Text.Json;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Assurance;

public sealed class AssuranceProgressContractTests
{
    private static readonly Guid RunId = Guid.Parse("fedcba98-7654-3210-fedc-ba9876543210");
    private static readonly Sha256Digest ProfileDigest = Sha256Digest.Parse(new string('a', 64));

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
    public void VerifyCompletedEntry_SerializesVerdictAsContractLiteral ()
    {
        var json = IpcPayloadCodec.SerializeToElement(new VerifyCompletedEntry(
            VerifyProfileSource.BuiltIn,
            "default",
            null,
            ProfileDigest,
            1,
            Verdict.Incomplete));

        Assert.Equal(
            TextVocabulary.GetText(Verdict.Incomplete),
            json.GetProperty("verdict").GetString());
    }
}
