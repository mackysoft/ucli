using System.Text.Json;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Execution.OperationExecute;
using MackySoft.Ucli.Operations;
using MackySoft.Ucli.Refresh;

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
            projectPath: "/repo/UnityProject",
            mode: "oneshot",
            timeout: "1234",
            waitUntilReady: true,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(operationExecuteService.CapturedDefinition);
        Assert.Equal(UcliCommandIds.Refresh, operationExecuteService.CapturedDefinition!.Command);
        Assert.Equal("refresh", operationExecuteService.CapturedDefinition.OperationId);
        Assert.Equal(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh, operationExecuteService.CapturedDefinition.Descriptor.Name);
        Assert.Equal(OperationPolicy.Advanced, operationExecuteService.CapturedDefinition.Descriptor.Policy);
        Assert.Equal(JsonValueKind.Object, operationExecuteService.CapturedDefinition.Args.ValueKind);
        Assert.Equal("/repo/UnityProject", operationExecuteService.CapturedProjectPath);
        Assert.Equal("oneshot", operationExecuteService.CapturedMode);
        Assert.Equal("1234", operationExecuteService.CapturedTimeout);
        Assert.True(operationExecuteService.CapturedWaitUntilReady);
    }

    private sealed class SpyOperationExecuteService : IOperationExecuteService
    {
        private readonly OperationExecuteResult result;

        public SpyOperationExecuteService (OperationExecuteResult result)
        {
            this.result = result;
        }

        public OperationExecuteDefinition? CapturedDefinition { get; private set; }

        public string? CapturedProjectPath { get; private set; }

        public string? CapturedMode { get; private set; }

        public string? CapturedTimeout { get; private set; }

        public bool CapturedWaitUntilReady { get; private set; }

        public ValueTask<OperationExecuteResult> Execute (
            OperationExecuteDefinition definition,
            string? projectPath,
            string? mode,
            string? timeout,
            bool waitUntilReady,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CapturedDefinition = definition;
            CapturedProjectPath = projectPath;
            CapturedMode = mode;
            CapturedTimeout = timeout;
            CapturedWaitUntilReady = waitUntilReady;
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