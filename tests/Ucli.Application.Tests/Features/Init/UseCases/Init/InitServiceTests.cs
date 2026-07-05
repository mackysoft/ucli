using MackySoft.Ucli.Application.Features.Init.Common.Contracts;
using MackySoft.Ucli.Application.Features.Init.UseCases.Init;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests.Init;

public sealed class InitServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenInputIsValid_WritesDefaultConfigThroughTemplateStore ()
    {
        var expectedResult = InitExecutionResult.Success(new InitExecutionOutput(
            ConfigPath: "/repo/.ucli/config.json",
            GitIgnorePath: "/repo/.ucli/.gitignore"));
        var templateStore = new RecordingInitTemplateStore(expectedResult);
        var service = new InitService(templateStore);

        var result = await service.ExecuteAsync(new InitCommandInput(Force: true), CancellationToken.None);

        Assert.Same(expectedResult, result);
        InitTemplateStoreAssert.DefaultConfigWritten(templateStore, expectedForce: true);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenTemplateStoreFails_ReturnsStoreError ()
    {
        var expectedResult = InitExecutionResult.Failure(ExecutionError.InternalError("write failed."));
        var service = new InitService(new RecordingInitTemplateStore(expectedResult));

        var result = await service.ExecuteAsync(new InitCommandInput(Force: false), CancellationToken.None);

        Assert.Same(expectedResult, result);
    }

}
