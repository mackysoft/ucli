using System.Runtime.Versioning;
using MackySoft.Tests;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Tests.Helpers;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorManifestStoreTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteReadDelete_RoundTripsManifestJson ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-manifest-store", "roundtrip");
        var store = new SupervisorManifestStore();
        var manifest = CreateManifest();

        await store.WriteAsync(scope.FullPath, manifest, CancellationToken.None);

        var loadedManifest = await store.ReadOrNullAsync(scope.FullPath, CancellationToken.None);

        Assert.NotNull(loadedManifest);
        Assert.Equal(manifest.ProcessId, loadedManifest.ProcessId);
        Assert.Equal(manifest.SessionToken, loadedManifest.SessionToken);
        Assert.Equal(manifest.EndpointTransportKind, loadedManifest.EndpointTransportKind);
        Assert.Equal(manifest.EndpointAddress, loadedManifest.EndpointAddress);
        Assert.Equal(manifest.IssuedAtUtc, loadedManifest.IssuedAtUtc);

        store.DeleteIfExists(scope.FullPath);

        var readAfterDelete = await store.ReadOrNullAsync(scope.FullPath, CancellationToken.None);
        Assert.Null(readAfterDelete);
    }

    [Fact]
    [Trait("Size", "Medium")]
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

        await store.WriteAsync(scope.FullPath, manifest, CancellationToken.None);

        var localDirectoryPath = UcliStoragePathResolver.ResolveLocalDirectoryPath(scope.FullPath);
        var supervisorDirectoryPath = UcliStoragePathResolver.ResolveSupervisorDirectoryPath(scope.FullPath);
        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(scope.FullPath);

        PosixAccessBoundaryAssert.DirectoryIsOwnerOnly(localDirectoryPath);
        PosixAccessBoundaryAssert.DirectoryIsOwnerOnly(supervisorDirectoryPath);
        PosixAccessBoundaryAssert.FileIsOwnerOnly(manifestPath);
    }

    [Fact]
    [Trait("Size", "Medium")]
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

        await store.WriteAsync(scope.FullPath, manifest, CancellationToken.None);

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
