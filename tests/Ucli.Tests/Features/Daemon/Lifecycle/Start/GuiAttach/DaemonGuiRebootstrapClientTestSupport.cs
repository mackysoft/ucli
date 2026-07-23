namespace MackySoft.Ucli.Tests.Daemon;

using System.Text.Json;
using MackySoft.FileSystem;
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
            transportClient);
    }

    public static GuiSupervisorManifestJsonContract CreateManifest ()
    {
        return new GuiSupervisorManifestJsonContract(
            SchemaVersion: GuiSupervisorManifestJsonContract.CurrentSchemaVersion,
            SessionToken: IpcSessionTokenTestFactory.Create("supervisor-token"),
            ProjectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint"),
            Endpoint: new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-gui-supervisor.sock"),
            ProcessId: 1234,
            ProcessStartedAtUtc: ProcessStartedAtUtc,
            IssuedAtUtc: new DateTimeOffset(2026, 5, 9, 1, 2, 4, TimeSpan.Zero));
    }

    public static async Task WriteManifestAsync<TManifest> (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        TManifest manifest)
    {
        var manifestPath = UcliStoragePathResolver.ResolveGuiSupervisorManifestPath(
            AbsolutePath.Parse(storageRoot),
            projectFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath.Value)!);
        var json = manifest is GuiSupervisorManifestJsonContract contract
            ? GuiSupervisorManifestJsonContractSerializer.Serialize(contract)
            : JsonSerializer.Serialize(manifest, IpcJsonSerializerOptions.Default);
        await File.WriteAllTextAsync(
            manifestPath.Value,
            json);
    }
}
