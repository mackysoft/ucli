using MackySoft.Ucli.Application.Features.Init.Common.Contracts;
using MackySoft.Ucli.Application.Features.Init.Ports;
using MackySoft.Ucli.Application.Features.Init.UseCases.Init;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Tests.Init;

public sealed class InitServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ExecuteAsync_WritesDefaultConfigThroughTemplateStore ()
    {
        var expectedResult = InitExecutionResult.Success(new InitExecutionOutput(
            ConfigPath: "/repo/.ucli/config.json",
            GitIgnorePath: "/repo/.ucli/.gitignore"));
        var templateStore = new StubInitTemplateStore(expectedResult);
        var service = new InitService(templateStore);

        var result = await service.ExecuteAsync(new InitCommandInput(Force: true), CancellationToken.None);

        Assert.Same(expectedResult, result);
        Assert.True(templateStore.LastForce);
        var config = Assert.IsType<UcliConfig>(templateStore.LastConfig);
        Assert.Equal(1, config.SchemaVersion);
        Assert.Equal(OperationPolicy.Safe, config.OperationPolicy);
        Assert.Equal(PlanTokenMode.Optional, config.PlanTokenMode);
        Assert.Equal(ReadIndexMode.RequireFresh, config.ReadIndexDefaultMode);
        Assert.Equal(["^ucli\\."], config.OperationAllowlist);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExecuteAsync_WhenTemplateStoreFails_ReturnsStoreError ()
    {
        var expectedResult = InitExecutionResult.Failure(ExecutionError.InternalError("write failed."));
        var service = new InitService(new StubInitTemplateStore(expectedResult));

        var result = await service.ExecuteAsync(new InitCommandInput(Force: false), CancellationToken.None);

        Assert.Same(expectedResult, result);
    }

    private sealed class StubInitTemplateStore : IInitTemplateStore
    {
        private readonly InitExecutionResult result;

        public StubInitTemplateStore (InitExecutionResult result)
        {
            this.result = result;
        }

        public UcliConfig? LastConfig { get; private set; }

        public bool LastForce { get; private set; }

        public ValueTask<InitExecutionResult> WriteAsync (
            UcliConfig config,
            bool force,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastConfig = config;
            LastForce = force;
            return ValueTask.FromResult(result);
        }
    }
}
