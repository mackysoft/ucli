namespace MackySoft.Ucli.Tests.Daemon;

using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start.GuiAttach;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Tests.Helpers.Ipc;

internal static class DaemonGuiRebootstrapClientTestSupport
{
    public static readonly DateTimeOffset ProcessStartedAtUtc =
        new(2026, 5, 9, 1, 2, 3, TimeSpan.Zero);

    public static DaemonGuiRebootstrapClient CreateClient (StubIpcTransportClient transportClient)
    {
        return new DaemonGuiRebootstrapClient(
            new GuiSupervisorManifestStore(),
            transportClient,
            new ManualTimeProvider());
    }

    public static GuiSupervisorManifestJsonContract CreateManifest ()
    {
        return new GuiSupervisorManifestJsonContract(
            SchemaVersion: GuiSupervisorManifestJsonContract.CurrentSchemaVersion,
            SessionToken: "supervisor-token",
            ProjectFingerprint: "fingerprint",
            EndpointTransportKind: ContractLiteralCodec.ToValue(IpcTransportKind.UnixDomainSocket),
            EndpointAddress: "/tmp/ucli-gui-supervisor.sock",
            ProcessId: 1234,
            ProcessStartedAtUtc: ProcessStartedAtUtc,
            IssuedAtUtc: new DateTimeOffset(2026, 5, 9, 1, 2, 4, TimeSpan.Zero));
    }

    public static async Task WriteManifestAsync (
        string storageRoot,
        string projectFingerprint,
        GuiSupervisorManifestJsonContract manifest)
    {
        var manifestPath = UcliStoragePathResolver.ResolveGuiSupervisorManifestPath(storageRoot, projectFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        await File.WriteAllTextAsync(
            manifestPath,
            JsonSerializer.Serialize(manifest, IpcJsonSerializerOptions.Default));
    }
}
