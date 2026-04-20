using System.Runtime.Versioning;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Tests.Helpers;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorManifestStoreTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteReadDelete_RoundTripsManifestJson ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-manifest-store", "roundtrip");
        var store = new SupervisorManifestStore();
        var manifest = CreateManifest();

        await store.Write(scope.FullPath, manifest, CancellationToken.None);

        var loadedManifest = await store.ReadOrNull(scope.FullPath, CancellationToken.None);

        Assert.NotNull(loadedManifest);
        Assert.Equal(manifest.ProcessId, loadedManifest.ProcessId);
        Assert.Equal(manifest.SessionToken, loadedManifest.SessionToken);
        Assert.Equal(manifest.EndpointTransportKind, loadedManifest.EndpointTransportKind);
        Assert.Equal(manifest.EndpointAddress, loadedManifest.EndpointAddress);
        Assert.Equal(manifest.IssuedAtUtc, loadedManifest.IssuedAtUtc);

        store.DeleteIfExists(scope.FullPath);

        var readAfterDelete = await store.ReadOrNull(scope.FullPath, CancellationToken.None);
        Assert.Null(readAfterDelete);
    }

    [Fact]
    [Trait("Size", "Small")]
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("linux")]
    public async Task Write_OnUnix_SavesManifestJsonUnderOwnerOnlyBoundary ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("supervisor-manifest-store", "owner-only");
        var store = new SupervisorManifestStore();
        var manifest = CreateManifest();

        await store.Write(scope.FullPath, manifest, CancellationToken.None);

        var localDirectoryPath = UcliStoragePathResolver.ResolveLocalDirectoryPath(scope.FullPath);
        var supervisorDirectoryPath = UcliStoragePathResolver.ResolveSupervisorDirectoryPath(scope.FullPath);
        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(scope.FullPath);

        PosixAccessBoundaryAssert.DirectoryIsOwnerOnly(localDirectoryPath);
        PosixAccessBoundaryAssert.DirectoryIsOwnerOnly(supervisorDirectoryPath);
        PosixAccessBoundaryAssert.FileIsOwnerOnly(manifestPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    [SupportedOSPlatform("windows")]
    public async Task Write_OnWindows_SavesManifestJsonUnderCurrentUserOnlyBoundary ()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("supervisor-manifest-store", "current-user-only");
        var store = new SupervisorManifestStore();
        var manifest = CreateManifest();

        await store.Write(scope.FullPath, manifest, CancellationToken.None);

        var localDirectoryPath = UcliStoragePathResolver.ResolveLocalDirectoryPath(scope.FullPath);
        var supervisorDirectoryPath = UcliStoragePathResolver.ResolveSupervisorDirectoryPath(scope.FullPath);
        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(scope.FullPath);

        WindowsAccessBoundaryAssert.DirectoryIsCurrentUserOnly(localDirectoryPath);
        WindowsAccessBoundaryAssert.DirectoryIsCurrentUserOnly(supervisorDirectoryPath);
        WindowsAccessBoundaryAssert.FileIsCurrentUserOnly(manifestPath);
    }

    private static SupervisorInstanceManifest CreateManifest ()
    {
        return new SupervisorInstanceManifest(
            ProcessId: 2468,
            SessionToken: "supervisor-session-token",
            EndpointTransportKind: "unixDomainSocket",
            EndpointAddress: "/tmp/ucli-supervisor-test/ipc.sock",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 14, 0, 0, 0, TimeSpan.Zero));
    }
}