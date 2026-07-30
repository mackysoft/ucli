using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using static MackySoft.Ucli.TestSupport.OperationCatalogTestFixtures;

namespace MackySoft.Ucli.Tests.Ops.Source;

public sealed class OpsCatalogReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenResponseIsSuccessful_ReturnsCatalogPayload ()
    {
        var executor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(CreateResponse(
                IpcResponseStatus.Ok,
                Array.Empty<IpcError>(),
                new IpcOpsReadResponse(
                    DateTimeOffset.Parse("2026-03-07T00:00:00+00:00"),
                    [
                        CreateGoDescribeEntry(),
                    ]))));
        var reader = new OpsCatalogReader(executor);

        var result = await reader.ReadAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UnityExecutionMode.Daemon,
            TimeSpan.FromMilliseconds(1200),
            failFast: true,
            requireReadinessGate: false,
            includeEditLoweringOnly: true,
            cancellationToken: CancellationToken.None);

        var succeeded = Assert.IsType<OpsCatalogFetchResult.Succeeded>(result);
        Assert.Single(succeeded.Snapshot.Operations);
        Assert.Equal(UcliPrimitiveOperationNames.GoDescribe, succeeded.Snapshot.Operations[0].Name);
        Assert.Equal(UcliOperationKind.Query, succeeded.Snapshot.Operations[0].Kind);
        Assert.Equal(OperationPolicy.Safe, succeeded.Snapshot.Operations[0].Policy);
        var execution = UnityRequestExecutorAssert.PayloadExecutedOnce<UnityRequestPayload.OpsRead>(
            executor,
            UcliCommandIds.Ops,
            UnityExecutionMode.Daemon);
        Assert.True(execution.Payload.FailFast);
        Assert.False(execution.Payload.RequireReadinessGate);
        Assert.True(execution.Payload.IncludeEditLoweringOnly);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenResponseContainsIpcFailure_ReturnsFailure ()
    {
        var executor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(CreateResponse(
                IpcResponseStatus.Error,
                [
                    new IpcError(
                        UcliCoreErrorCodes.InvalidArgument,
                        "invalid request",
                        null),
                ],
                new { })));
        var reader = new OpsCatalogReader(executor);

        var result = await reader.ReadAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UnityExecutionMode.Auto,
            TimeSpan.FromMilliseconds(1200),
            failFast: false,
            requireReadinessGate: true,
            includeEditLoweringOnly: false,
            cancellationToken: CancellationToken.None);

        var failed = Assert.IsType<OpsCatalogFetchResult.Failed>(result);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, failed.Error.Code);
        Assert.Equal("invalid request", failed.Error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenPayloadIsMalformed_ReturnsFailure ()
    {
        var executor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(CreateResponse(
                IpcResponseStatus.Ok,
                Array.Empty<IpcError>(),
                new
                {
                    generatedAtUtc = "2026-03-07T00:00:00+00:00",
                })));
        var reader = new OpsCatalogReader(executor);

        var result = await reader.ReadAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UnityExecutionMode.Auto,
            TimeSpan.FromMilliseconds(1200),
            failFast: false,
            requireReadinessGate: false,
            includeEditLoweringOnly: false,
            cancellationToken: CancellationToken.None);

        var failed = Assert.IsType<OpsCatalogFetchResult.Failed>(result);
        Assert.Equal(UcliCoreErrorCodes.InternalError, failed.Error.Code);
        Assert.Contains("payload is invalid", failed.Error.Message, StringComparison.Ordinal);
    }

    private static UnityRequestResponse CreateResponse (
        IpcResponseStatus status,
        IReadOnlyList<IpcError> errors,
        object payload)
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            IpcProtocol.CurrentVersion,
            Guid.NewGuid(),
            status,
            IpcPayloadCodec.SerializeToElement(payload),
            errors));
    }

}
