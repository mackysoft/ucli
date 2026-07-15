using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Contracts.Tests.Storage;

public sealed class SensitiveStorageContractTextRepresentationTests
{
    private const string ProjectFingerprintText = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly ProjectFingerprint ProjectFingerprint = new(ProjectFingerprintText);

    [Fact]
    [Trait("Size", "Small")]
    public void DaemonSessionJsonContract_TextRepresentations_DoNotExposeSessionToken ()
    {
        const string SessionToken = "sensitive-daemon-session-token-DO-NOT-LOG";
        var contract = new DaemonSessionJsonContract(
            SchemaVersion: DaemonSessionStorageContract.CurrentSchemaVersion,
            SessionGenerationId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            SessionToken: SessionToken,
            ProjectFingerprint: ProjectFingerprint,
            IssuedAtUtc: new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero),
            EditorMode: DaemonEditorMode.Batchmode,
            OwnerKind: DaemonSessionOwnerKind.Cli,
            CanShutdownProcess: true,
            EndpointTransportKind: IpcTransportKind.NamedPipe,
            EndpointAddress: "ucli-daemon-endpoint",
            ProcessId: 1234,
            ProcessStartedAtUtc: new DateTimeOffset(2026, 7, 13, 0, 0, 1, TimeSpan.Zero),
            OwnerProcessId: 5678,
            EditorInstanceId: null);

        string[] textRepresentations =
        [
            contract.ToString() ?? string.Empty,
            $"Contract={contract}",
            new DiagnosticEnvelope(contract).ToString(),
        ];

        Assert.All(
            textRepresentations,
            text => Assert.DoesNotContain(SessionToken, text, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GuiSupervisorManifestJsonContract_TextRepresentations_DoNotExposeSessionToken ()
    {
        const string SessionToken = "sensitive-gui-supervisor-token-DO-NOT-LOG";
        var contract = new GuiSupervisorManifestJsonContract(
            SchemaVersion: GuiSupervisorManifestJsonContract.CurrentSchemaVersion,
            SessionToken: SessionToken,
            ProjectFingerprint: ProjectFingerprint,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-gui-supervisor-endpoint",
            ProcessId: 1234,
            ProcessStartedAtUtc: new DateTimeOffset(2026, 7, 13, 0, 0, 1, TimeSpan.Zero),
            IssuedAtUtc: new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero));

        string[] textRepresentations =
        [
            contract.ToString() ?? string.Empty,
            $"Contract={contract}",
            new DiagnosticEnvelope(contract).ToString(),
        ];

        Assert.All(
            textRepresentations,
            text => Assert.DoesNotContain(SessionToken, text, StringComparison.Ordinal));
    }

    private sealed record DiagnosticEnvelope (object Value);
}
