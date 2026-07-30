using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;
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
            mode: UnityExecutionMode.Auto,
            timeout: null,
            failFast: false,
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
            mode: UnityExecutionMode.Auto,
            timeout: null,
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
                Result = OpsCatalogFetchResult.Failure(ApplicationFailure.InvalidInput(
                    "Mode must be auto, daemon, or oneshot.",
                    UcliCoreErrorCodes.InvalidArgument,
                    instancePath: null,
                    startupFailure: null)),
            });

        var exception = await Assert.ThrowsAsync<OperationCatalogLoadException>(async () =>
            await service.DiscoverAsync(
                ProjectContextTestFactory.CreateTemporaryFixtureUnityProject(),
                UcliConfig.CreateDefault(),
                mode: (UnityExecutionMode)999,
                timeout: TimeSpan.FromMilliseconds(1200),
                failFast: false,
                cancellationToken: CancellationToken.None));

        Assert.Equal(ApplicationFailureKind.InvalidInput, exception.Error.Kind);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, exception.Error.Code);
        Assert.Contains("Operation catalog discovery failed.", exception.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Discover_WhenCatalogReaderReturnsTimeout_ThrowsTypedLoadException ()
    {
        var service = new OperationCatalogDiscoveryService(
            new RecordingOpsCatalogReader
            {
                Result = OpsCatalogFetchResult.Failure(ApplicationFailure.Timeout(
                    "Timed out before Unity IPC request dispatch could begin.",
                    ExecutionErrorCodes.IpcTimeout,
                    instancePath: null,
                    startupFailure: null)),
            });

        var exception = await Assert.ThrowsAsync<OperationCatalogLoadException>(async () =>
            await service.DiscoverAsync(
                ProjectContextTestFactory.CreateTemporaryFixtureUnityProject(),
                UcliConfig.CreateDefault(),
                mode: UnityExecutionMode.Auto,
                timeout: TimeSpan.FromMilliseconds(1200),
                failFast: false,
                cancellationToken: CancellationToken.None));

        Assert.Equal(ApplicationFailureKind.Timeout, exception.Error.Kind);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, exception.Error.Code);
        Assert.Contains("Operation catalog discovery failed.", exception.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Discover_WhenCatalogReaderReturnsModeContractError_PreservesOriginalErrorCode ()
    {
        var service = new OperationCatalogDiscoveryService(
            new RecordingOpsCatalogReader
            {
                Result = OpsCatalogFetchResult.Failure(ApplicationFailure.UnityIpcFailure(
                    "Daemon is not running for mode=daemon.",
                    UnityExecutionModeDecisionErrorCodes.DaemonNotRunning,
                    instancePath: null,
                    startupFailure: null)),
            });

        var exception = await Assert.ThrowsAsync<OperationCatalogLoadException>(async () =>
            await service.DiscoverAsync(
                ProjectContextTestFactory.CreateTemporaryFixtureUnityProject(),
                UcliConfig.CreateDefault(),
                mode: UnityExecutionMode.Daemon,
                timeout: TimeSpan.FromMilliseconds(1200),
                failFast: false,
                cancellationToken: CancellationToken.None));

        Assert.Equal(ApplicationFailureKind.UnityIpcFailure, exception.Error.Kind);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, exception.Error.Code);
        Assert.Contains("Operation catalog discovery failed.", exception.Error.Message, StringComparison.Ordinal);
    }

    private static OpsCatalogFetchResult CreateSceneOpenFetchResult ()
    {
        var generationResult = UcliOperationJsonContractGenerator.Generate(
            MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen,
            IpcJsonSerializerOptions.PublicRawOperationContracts.GetTypeInfo(typeof(ScenePathArgs)),
            resultTypeInfo: null);
        var describe = UcliOperationDescribeContractBuilder.CreateWithoutVerdict(
            generationResult,
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
                dangerousNotes: Array.Empty<string>()),
            codeContract: null);

        return OpsCatalogFetchResult.Success(
            CreateSnapshot(
                DateTimeOffset.UtcNow,
                [
                    new IndexOpEntryJsonContract(
                        Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen,
                        Kind: UcliOperationKind.Command,
                        Policy: OperationPolicy.Safe,
                        ArgsContract: describe.ArgsContract,
                        DescriptorDigest: null,
                        VerdictContract: null,
                        ResultContract: describe.ResultContract,
                        Exposure: null,
                        PlayModeSupport: UcliOperationPlayModeSupport.Disallowed)
                    {
                        Description = describe.Description,
                        Assurance = describe.Assurance,
                    },
                ]));
    }
}
