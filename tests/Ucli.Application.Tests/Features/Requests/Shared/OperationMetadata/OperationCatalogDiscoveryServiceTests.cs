using System.Text.Json;
using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.TestSupport.OperationCatalogTestFixtures;

namespace MackySoft.Ucli.Application.Tests;

public sealed class OperationCatalogDiscoveryServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Discover_WhenTimeoutIsOmitted_UsesDefaultOpsTimeout ()
    {
        var config = UcliConfig.CreateDefault();
        var reader = new RecordingOpsCatalogReader
        {
            Result = CreateSceneOpenFetchResult(),
        };
        var service = new OperationCatalogDiscoveryService(reader);

        var operations = await service.DiscoverAsync(
            ProjectContextTestFactory.CreateTemporaryFixtureUnityProject(),
            config,
            cancellationToken: CancellationToken.None);

        OperationCatalogInvocationAssert.OpsCatalogReadRequestedWithTimeout(
            reader,
            TimeSpan.FromMilliseconds(config.IpcTimeoutMillisecondsByCommand[UcliCommandIds.Ops.Name]!.Value),
            expectedFailFast: false,
            expectedRequireReadinessGate: false,
            expectedIncludeEditLoweringOnly: true);
        Assert.Single(operations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Discover_WhenFailFastIsSpecified_PropagatesToReader ()
    {
        var reader = new RecordingOpsCatalogReader
        {
            Result = CreateSceneOpenFetchResult(),
        };
        var service = new OperationCatalogDiscoveryService(reader);

        _ = await service.DiscoverAsync(
            ProjectContextTestFactory.CreateTemporaryFixtureUnityProject(),
            UcliConfig.CreateDefault(),
            failFast: true,
            cancellationToken: CancellationToken.None);

        OperationCatalogInvocationAssert.OpsCatalogReadRequestedOnce(
            reader,
            expectedFailFast: true,
            expectedRequireReadinessGate: false,
            expectedIncludeEditLoweringOnly: true);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Discover_WhenCatalogReaderReturnsInvalidArgument_ThrowsTypedLoadException ()
    {
        var service = new OperationCatalogDiscoveryService(
            new RecordingOpsCatalogReader
            {
                Result = OpsCatalogFetchResult.Failure(
                    "Mode must be auto, daemon, or oneshot.",
                    UcliCoreErrorCodes.InvalidArgument),
            });

        var exception = await Assert.ThrowsAsync<OperationCatalogLoadException>(async () =>
            await service.DiscoverAsync(
                ProjectContextTestFactory.CreateTemporaryFixtureUnityProject(),
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
            new RecordingOpsCatalogReader
            {
                Result = OpsCatalogFetchResult.Failure(
                    "Timed out before Unity IPC request dispatch could begin.",
                    ExecutionErrorCodes.IpcTimeout),
            });

        var exception = await Assert.ThrowsAsync<OperationCatalogLoadException>(async () =>
            await service.DiscoverAsync(
                ProjectContextTestFactory.CreateTemporaryFixtureUnityProject(),
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
            new RecordingOpsCatalogReader
            {
                Result = OpsCatalogFetchResult.Failure(
                    "Daemon is not running for mode=daemon.",
                    UnityExecutionModeDecisionErrorCodes.DaemonNotRunning),
            });

        var exception = await Assert.ThrowsAsync<OperationCatalogLoadException>(async () =>
            await service.DiscoverAsync(
                ProjectContextTestFactory.CreateTemporaryFixtureUnityProject(),
                UcliConfig.CreateDefault(),
                mode: UnityExecutionMode.Daemon,
                timeout: TimeSpan.FromMilliseconds(1200),
                cancellationToken: CancellationToken.None));

        Assert.Equal(ExecutionErrorKind.InternalError, exception.Error.Kind);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, exception.ErrorCode);
        Assert.Contains("Operation catalog discovery failed.", exception.Error.Message, StringComparison.Ordinal);
    }

    private static OpsCatalogFetchResult CreateSceneOpenFetchResult ()
    {
        var describe = UcliOperationDescribeContractBuilder.Create<ScenePathArgs, UcliNoResult>(
            "Opens a Unity scene asset in the editor.",
            new UcliOperationAssuranceContract(
                sideEffects: Array.Empty<UcliOperationSideEffect>(),
                touchedKinds: Array.Empty<UcliTouchedResourceKind>(),
                planMode: UcliOperationPlanMode.ObservesLiveUnity,
                planSemantics: "Validate arguments and observe Unity state without applying mutation.",
                callSemantics: "Read Unity state without applying mutation.",
                touchedContract: "Returns no touched resources.",
                readPostconditionContract: "Does not stale read surfaces by itself.",
                failureSemantics: "Failure means the observation was not fully produced.",
                dangerousNotes: Array.Empty<string>()));

        return OpsCatalogFetchResult.Success(
            CreateSnapshot(
                DateTimeOffset.UtcNow,
                [
                    new IndexOpEntryJsonContract(
                        Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen,
                        Kind: "command",
                        Policy: "safe",
                        ArgsSchemaJson: JsonSerializer.Serialize(new
                        {
                            type = "object",
                            additionalProperties = false,
                        }))
                    {
                        Description = describe.Description,
                        Inputs = describe.Inputs,
                        ResultContract = describe.ResultContract,
                        Assurance = describe.Assurance,
                    },
                ]));
    }
}
