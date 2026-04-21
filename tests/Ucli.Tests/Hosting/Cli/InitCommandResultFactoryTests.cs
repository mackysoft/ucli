using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Init.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Init;
using MackySoft.Ucli.Shared.Foundation;

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
        Assert.Equal(IpcProtocol.StatusOk, result.Status);
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
        Assert.Equal(IpcProtocol.StatusError, result.Status);
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        Assert.Single(result.Errors);
        Assert.Equal(IpcErrorCodes.InternalError, result.Errors[0].Code);
        Assert.Equal("Failed to create .ucli directory.", result.Errors[0].Message);
    }
}