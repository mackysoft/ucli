using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonArtifactCleanerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenUnixFallbackSocketDirectoryBecomesEmpty_DeletesFallbackDirectory ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var socketPath = UnixSocketPathUtilities.BuildFallbackSocketPath(UcliIpcEndpointNames.DaemonAddressPrefix, Guid.NewGuid().ToString("N"));
        var socketDirectoryPath = Path.GetDirectoryName(socketPath)!;
        Directory.CreateDirectory(socketDirectoryPath);
        await File.WriteAllTextAsync(socketPath, string.Empty, CancellationToken.None);

        var cleaner = new DaemonArtifactCleaner(
            new StubDaemonSessionStore(),
            new StubEndpointResolver(new IpcEndpoint(IpcTransportKind.UnixDomainSocket, socketPath)));

        var result = await cleaner.Cleanup(
            new ResolvedUnityProjectContext(
                UnityProjectRoot: "/tmp/unity-project",
                RepositoryRoot: "/tmp/repo-root",
                ProjectFingerprint: "fingerprint-cleanup",
                PathSource: UnityProjectPathSource.CommandOption),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(File.Exists(socketPath));
        Assert.False(Directory.Exists(socketDirectoryPath));
    }

    private sealed class StubDaemonSessionStore : IDaemonSessionStore
    {
        public ValueTask<DaemonSessionReadResult> Read (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<DaemonSessionStoreOperationResult> Write (
            string storageRoot,
            DaemonSession session,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<DaemonSessionStoreOperationResult> Delete (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonSessionStoreOperationResult.Success());
        }
    }

    private sealed class StubEndpointResolver : IIpcEndpointResolver
    {
        private readonly IpcEndpoint endpoint;

        public StubEndpointResolver (IpcEndpoint endpoint)
        {
            this.endpoint = endpoint;
        }

        public IpcEndpoint Resolve (
            string storageRoot,
            string projectFingerprint)
        {
            return endpoint;
        }
    }
}