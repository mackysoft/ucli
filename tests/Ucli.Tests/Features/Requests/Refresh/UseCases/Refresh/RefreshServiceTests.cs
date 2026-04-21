using System.Text.Json;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Features.Requests.Refresh.UseCases.Refresh;
using MackySoft.Ucli.Features.Requests.Shared.Execution.OperationExecute;
using MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Tests.Refresh;

public sealed class RefreshServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_DelegatesToOperationExecuteServiceWithRefreshDefinition ()
    {
        var operationExecuteService = new SpyOperationExecuteService(OperationExecuteResultFactory.Success());
        var service = new RefreshService(operationExecuteService);

        var result = await service.Execute(
            new RefreshCommandInput(
                ProjectPath: "/repo/UnityProject",
                Mode: UnityExecutionMode.Oneshot,
                TimeoutMilliseconds: 1234,
                FailFast: true),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(operationExecuteService.CapturedDefinition);
        Assert.Equal(UcliCommandIds.Refresh, operationExecuteService.CapturedDefinition!.Command);
        Assert.Equal("refresh", operationExecuteService.CapturedDefinition.OperationId);
        Assert.Equal(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh, operationExecuteService.CapturedDefinition.Descriptor.Name);
        Assert.Equal(OperationPolicy.Advanced, operationExecuteService.CapturedDefinition.Descriptor.Policy);
        Assert.Equal(JsonValueKind.Object, operationExecuteService.CapturedDefinition.Args.ValueKind);
        Assert.NotNull(operationExecuteService.CapturedInput);
        Assert.Equal("/repo/UnityProject", operationExecuteService.CapturedInput!.ProjectPath);
        Assert.Equal(UnityExecutionMode.Oneshot, operationExecuteService.CapturedInput.Mode);
        Assert.Equal(1234, operationExecuteService.CapturedInput.TimeoutMilliseconds);
        Assert.True(operationExecuteService.CapturedInput.FailFast);
    }

    private sealed class SpyOperationExecuteService : IOperationExecuteService
    {
        private readonly OperationExecuteResult result;

        public SpyOperationExecuteService (OperationExecuteResult result)
        {
            this.result = result;
        }

        public OperationExecuteDefinition? CapturedDefinition { get; private set; }

        public OperationExecuteInput? CapturedInput { get; private set; }

        public ValueTask<OperationExecuteResult> Execute (
            OperationExecuteDefinition definition,
            OperationExecuteInput input,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CapturedDefinition = definition;
            CapturedInput = input;
            return ValueTask.FromResult(result);
        }
    }

    private static class OperationExecuteResultFactory
    {
        public static OperationExecuteResult Success ()
        {
            return new OperationExecuteResult(
                ProtocolVersion: 1,
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                OpResults: [],
                Errors: [],
                ExitCode: 0);
        }
    }
}