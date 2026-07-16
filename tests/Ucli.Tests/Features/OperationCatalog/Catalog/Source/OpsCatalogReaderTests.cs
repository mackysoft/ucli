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

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Snapshot);
        Assert.Single(result.Snapshot.Operations);
        Assert.Equal(UcliPrimitiveOperationNames.GoDescribe, result.Snapshot.Operations[0].Name);
        Assert.Equal(UcliOperationKind.Query, result.Snapshot.Operations[0].Kind);
        Assert.Equal(OperationPolicy.Safe, result.Snapshot.Operations[0].Policy);
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
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.ErrorCode);
        Assert.Equal("invalid request", result.Message);
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
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
        Assert.Contains("payload is invalid", result.Message, StringComparison.Ordinal);
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
