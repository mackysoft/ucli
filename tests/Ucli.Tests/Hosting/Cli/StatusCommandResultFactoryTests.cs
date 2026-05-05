using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Status.Common.Contracts;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Status;

namespace MackySoft.Ucli.Tests;

public sealed class StatusCommandResultFactoryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithSuccessResult_ReturnsOkEnvelopeWithPayload ()
    {
        var executionResult = StatusExecutionResult.Success(
            new StatusExecutionOutput(
                DaemonStatus: DaemonStatusKind.Running,
                UnityVersion: "6000.1.4f1",
                ServerVersion: "0.5.0",
                LifecycleState: "busy",
                BlockingReason: "busy",
                CompileState: "ready",
                CompileGeneration: "12",
                DomainReloadGeneration: "7",
                CanAcceptExecutionRequests: false,
                Runtime: "batchmode",
                TimeoutMilliseconds: 1234));

        var result = StatusCommandResultFactory.Create(executionResult);

        Assert.Equal(UcliCommandNames.Status, result.Command);
        Assert.Equal(IpcProtocol.StatusOk, result.Status);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Empty(result.Errors);

        var payload = JsonSerializer.SerializeToElement(result.Payload);
        JsonAssert.For(payload)
            .HasString("daemonStatus", "running")
            .HasString("unityVersion", "6000.1.4f1")
            .HasString("serverVersion", "0.5.0")
            .HasString("lifecycleState", "busy")
            .HasString("blockingReason", "busy")
            .HasString("compileState", "ready")
            .HasString("compileGeneration", "12")
            .HasString("domainReloadGeneration", "7")
            .HasBoolean("canAcceptExecutionRequests", false)
            .HasString("runtime", "batchmode")
            .HasInt32("timeoutMilliseconds", 1234);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithFailureResult_ReturnsErrorEnvelope ()
    {
        var executionResult = StatusExecutionResult.Failure(ExecutionError.InvalidArgument("timeout is invalid."));

        var result = StatusCommandResultFactory.Create(executionResult);

        Assert.Equal(UcliCommandNames.Status, result.Command);
        Assert.Equal(IpcProtocol.StatusError, result.Status);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.Single(result.Errors);
        Assert.Equal(IpcErrorCodes.InvalidArgument, result.Errors[0].Code);
        Assert.Equal("timeout is invalid.", result.Errors[0].Message);
    }
}
