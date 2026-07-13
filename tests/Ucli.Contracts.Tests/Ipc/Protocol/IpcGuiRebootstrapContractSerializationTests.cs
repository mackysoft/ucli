using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcGuiRebootstrapContractSerializationTests
{
    private const string ProjectFingerprintText = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    [Trait("Size", "Small")]
    public void GuiRebootstrapContracts_SerializeProjectFingerprintAsCanonicalString ()
    {
        var projectFingerprint = new ProjectFingerprint(ProjectFingerprintText);
        var request = IpcPayloadCodec.SerializeToElement(
            new IpcGuiRebootstrapRequest(
                ProjectFingerprint: projectFingerprint,
                ReplaceExistingSession: true));
        var response = IpcPayloadCodec.SerializeToElement(
            new IpcGuiRebootstrapResponse(
                Accepted: true,
                ProjectFingerprint: projectFingerprint,
                ProcessId: 1234));

        JsonAssert.For(request)
            .HasString("projectFingerprint", ProjectFingerprintText)
            .HasBoolean("replaceExistingSession", true);
        JsonAssert.For(response)
            .HasBoolean("accepted", true)
            .HasString("projectFingerprint", ProjectFingerprintText)
            .HasInt32("processId", 1234);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDeserializeGuiRebootstrapRequest_WhenProjectFingerprintIsInvalid_ReturnsDeserializeFailed ()
    {
        using var document = JsonDocument.Parse("""
            {
              "projectFingerprint": "not-a-project-fingerprint",
              "replaceExistingSession": true
            }
            """);

        var parsed = IpcPayloadCodec.TryDeserialize<IpcGuiRebootstrapRequest>(
            document.RootElement,
            out _,
            out var error);

        Assert.False(parsed);
        Assert.Equal(IpcPayloadReadErrorKind.DeserializeFailed, error.Kind);
        Assert.Contains("project fingerprint is invalid", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
