using System.Text.Json;
using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

public sealed class OperationCatalogDiscoveryServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Discover_WhenTimeoutIsOmitted_UsesDefaultOpsTimeout ()
    {
        var config = UcliConfig.CreateDefault();
        var reader = new SpyOpsCatalogReader();
        var service = new OperationCatalogDiscoveryService(reader);

        var operations = await service.Discover(
            CreateUnityProject(),
            config,
            cancellationToken: CancellationToken.None);

        Assert.Equal(
            TimeSpan.FromMilliseconds(config.IpcTimeoutMillisecondsByCommand[UcliCommandIds.Ops.Name]!.Value),
            reader.ReceivedTimeout);
        Assert.False(reader.ReceivedFailFast);
        Assert.False(reader.ReceivedRequireReadinessGate);
        Assert.Single(operations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Discover_WhenFailFastIsSpecified_PropagatesToReader ()
    {
        var reader = new SpyOpsCatalogReader();
        var service = new OperationCatalogDiscoveryService(reader);

        _ = await service.Discover(
            CreateUnityProject(),
            UcliConfig.CreateDefault(),
            failFast: true,
            cancellationToken: CancellationToken.None);

        Assert.True(reader.ReceivedFailFast);
        Assert.False(reader.ReceivedRequireReadinessGate);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Discover_WhenCatalogReaderReturnsInvalidArgument_ThrowsTypedLoadException ()
    {
        var service = new OperationCatalogDiscoveryService(
            new StubOpsCatalogReader(OpsCatalogFetchResult.Failure(
                "Mode must be auto, daemon, or oneshot.",
                UcliCoreErrorCodes.InvalidArgument)));

        var exception = await Assert.ThrowsAsync<OperationCatalogLoadException>(async () =>
            await service.Discover(
                CreateUnityProject(),
                UcliConfig.CreateDefault(),
                mode: (UnityExecutionMode)999,
                timeout: TimeSpan.FromMilliseconds(1200),
                cancellationToken: CancellationToken.None));

        Assert.Equal(ExecutionErrorKind.InvalidArgument, exception.Error.Kind);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, exception.ErrorCode);
        Assert.Contains("Operation catalog discovery failed.", exception.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Discover_WhenCatalogReaderReturnsTimeout_ThrowsTypedLoadException ()
    {
        var service = new OperationCatalogDiscoveryService(
            new StubOpsCatalogReader(OpsCatalogFetchResult.Failure(
                "Timed out before Unity IPC request dispatch could begin.",
                ExecutionErrorCodes.IpcTimeout)));

        var exception = await Assert.ThrowsAsync<OperationCatalogLoadException>(async () =>
            await service.Discover(
                CreateUnityProject(),
                UcliConfig.CreateDefault(),
                timeout: TimeSpan.FromMilliseconds(1200),
                cancellationToken: CancellationToken.None));

        Assert.Equal(ExecutionErrorKind.Timeout, exception.Error.Kind);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, exception.ErrorCode);
        Assert.Contains("Operation catalog discovery failed.", exception.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Discover_WhenCatalogReaderReturnsModeContractError_PreservesOriginalErrorCode ()
    {
        var service = new OperationCatalogDiscoveryService(
            new StubOpsCatalogReader(OpsCatalogFetchResult.Failure(
                "Daemon is not running for mode=daemon.",
                UnityExecutionModeDecisionErrorCodes.DaemonNotRunning)));

        var exception = await Assert.ThrowsAsync<OperationCatalogLoadException>(async () =>
            await service.Discover(
                CreateUnityProject(),
                UcliConfig.CreateDefault(),
                mode: UnityExecutionMode.Daemon,
                timeout: TimeSpan.FromMilliseconds(1200),
                cancellationToken: CancellationToken.None));

        Assert.Equal(ExecutionErrorKind.InternalError, exception.Error.Kind);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, exception.ErrorCode);
        Assert.Contains("Operation catalog discovery failed.", exception.Error.Message, StringComparison.Ordinal);
    }

    private static ResolvedUnityProjectContext CreateUnityProject ()
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/tmp/project",
            RepositoryRoot: "/tmp/repository",
            ProjectFingerprint: "project-fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private sealed class SpyOpsCatalogReader : IOpsCatalogReader
    {
        public TimeSpan ReceivedTimeout { get; private set; }

        public bool ReceivedFailFast { get; private set; }

        public bool ReceivedRequireReadinessGate { get; private set; }

        public ValueTask<OpsCatalogFetchResult> Read (
            ResolvedUnityProjectContext project,
            UcliConfig config,
            UnityExecutionMode mode,
            TimeSpan timeout,
            bool failFast,
            bool requireReadinessGate,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReceivedTimeout = timeout;
            ReceivedFailFast = failFast;
            ReceivedRequireReadinessGate = requireReadinessGate;

            return ValueTask.FromResult(OpsCatalogFetchResult.Success(new IpcOpsReadResponse(
                GeneratedAtUtc: DateTimeOffset.UtcNow,
                Operations:
                [
                    new IndexOpEntryJsonContract(
                        Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen,
                        Kind: "query",
                        Policy: "safe",
                        ArgsSchemaJson: JsonSerializer.Serialize(new
                        {
                            type = "object",
                            additionalProperties = false,
                        })),
                ])));
        }
    }

    private sealed class StubOpsCatalogReader : IOpsCatalogReader
    {
        private readonly OpsCatalogFetchResult result;

        public StubOpsCatalogReader (OpsCatalogFetchResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public ValueTask<OpsCatalogFetchResult> Read (
            ResolvedUnityProjectContext project,
            UcliConfig config,
            UnityExecutionMode mode,
            TimeSpan timeout,
            bool failFast,
            bool requireReadinessGate,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }
}
