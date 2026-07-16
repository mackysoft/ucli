using System.Text.Json;
using MackySoft.Ucli.Application.Features.Init.Common.Contracts;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Init;

namespace MackySoft.Ucli.Tests;

public sealed class InitCommandResultFactoryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithSuccessResult_ReturnsOkEnvelopeWithPayload ()
    {
        var executionResult = InitExecutionResult.Success(
            new InitExecutionOutput(
                ConfigPath: "/repo/.ucli/config.json",
                GitIgnorePath: "/repo/.ucli/.gitignore"));

        var result = InitCommandResultFactory.Create(executionResult);

        Assert.Equal(UcliCommandNames.Init, result.Command);
        Assert.Equal(CommandResultStatus.Ok, result.Status);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Empty(result.Errors);

        var payload = JsonSerializer.SerializeToElement(result.Payload);
        JsonAssert.For(payload)
            .HasString("configPath", "/repo/.ucli/config.json")
            .HasString("gitignorePath", "/repo/.ucli/.gitignore");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithFailureResult_ReturnsErrorEnvelope ()
    {
        var executionResult = InitExecutionResult.Failure(
            ExecutionError.InternalError("Failed to create .ucli directory."));

        var result = InitCommandResultFactory.Create(executionResult);

        Assert.Equal(UcliCommandNames.Init, result.Command);
        Assert.Equal(CommandResultStatus.Error, result.Status);
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.Errors[0].Code);
        Assert.Equal("Failed to create .ucli directory.", result.Errors[0].Message);
    }
}
