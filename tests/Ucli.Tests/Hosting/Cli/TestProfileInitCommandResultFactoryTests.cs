using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Testing.Profiles.Common.Contracts;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Testing;

namespace MackySoft.Ucli.Tests;

public sealed class TestProfileInitCommandResultFactoryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithSuccessResult_ReturnsOkEnvelopeWithPayload ()
    {
        var executionResult = TestProfileInitExecutionResult.Success(
            new TestProfileInitExecutionOutput("/repo/test.profile.json"));

        var result = TestProfileInitCommandResultFactory.Create(executionResult);

        Assert.Equal(UcliCommandNames.TestProfileInit, result.Command);
        Assert.Equal(IpcProtocol.StatusOk, result.Status);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Empty(result.Errors);

        var payload = JsonSerializer.SerializeToElement(result.Payload);
        JsonAssert.For(payload)
            .HasString("profilePath", "/repo/test.profile.json");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithFailureResult_ReturnsErrorEnvelope ()
    {
        var executionResult = TestProfileInitExecutionResult.Failure(
            ExecutionError.InvalidArgument("Output path already exists."));

        var result = TestProfileInitCommandResultFactory.Create(executionResult);

        Assert.Equal(UcliCommandNames.TestProfileInit, result.Command);
        Assert.Equal(IpcProtocol.StatusError, result.Status);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.Single(result.Errors);
        Assert.Equal(IpcErrorCodes.InvalidArgument, result.Errors[0].Code);
        Assert.Equal("Output path already exists.", result.Errors[0].Message);
    }
}
