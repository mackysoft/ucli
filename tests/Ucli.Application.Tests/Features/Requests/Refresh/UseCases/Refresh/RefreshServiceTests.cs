using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Refresh;

public sealed class RefreshServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_DelegatesToOperationExecuteServiceWithRefreshDefinition ()
    {
        var operationExecuteService = new SpyOperationExecuteService(RefreshTestResultFactory.Success());
        var service = new RefreshService(operationExecuteService);

        var result = await service.ExecuteAsync(
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
        Assert.Equal(UcliPrimitiveOperationNames.ProjectRefresh, operationExecuteService.CapturedDefinition.Descriptor.Name);
        Assert.Equal(UcliOperationKind.Command, operationExecuteService.CapturedDefinition.Descriptor.Kind);
        Assert.Equal(OperationPolicy.Advanced, operationExecuteService.CapturedDefinition.Descriptor.Policy);
        Assert.Equal(JsonValueKind.Object, operationExecuteService.CapturedDefinition.Args.ValueKind);
        Assert.Equal("uCLI refresh completed.", operationExecuteService.CapturedDefinition.SuccessMessage);
        Assert.Equal("uCLI refresh failed.", operationExecuteService.CapturedDefinition.FailureMessage);
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

        public ValueTask<OperationExecuteResult> ExecuteAsync (
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

    private static class RefreshTestResultFactory
    {
        public static OperationExecuteResult Success ()
        {
            return OperationExecuteResultFactory.Success(
                "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                [],
                "uCLI refresh completed.",
                readPostcondition: null,
                project: new ProjectIdentityInfo(
                    ProjectPath: "/repo/UnityProject",
                    ProjectFingerprint: "project-fingerprint",
                    UnityVersion: "6000.1.4f1"));
        }
    }
}
