using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

public sealed class CommandFailureProjectorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithInvalidInput_ReturnsInvalidArgumentExitCode ()
    {
        var result = CommandFailureProjector.Create(
            "test",
            ApplicationFailure.InvalidInput(
                "Invalid input.",
                opId: new IpcExecuteStepId("step-1")));

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
        Assert.Equal("Invalid input.", error.Message);
        Assert.Equal("step-1", error.OpId?.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithToolFailures_ReturnsToolErrorExitCode ()
    {
        AssertProjectedExitCode(ApplicationFailure.Timeout("Execution timed out."), (int)CliExitCode.ToolError);
        AssertProjectedExitCode(ApplicationFailure.UnityIpcFailure("Unity IPC failed."), (int)CliExitCode.ToolError);
        AssertProjectedExitCode(ApplicationFailure.ContractViolation("Contract violation."), (int)CliExitCode.ToolError);
        AssertProjectedExitCode(ApplicationFailure.InternalError("Internal failure."), (int)CliExitCode.ToolError);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithInfrastructureFailure_ReturnsInfrastructureExitCode ()
    {
        var result = CommandFailureProjector.Create(
            "test",
            ApplicationFailure.ExternalProcessFailure(
                "Unity test infrastructure failed.",
                outcome: ApplicationOutcome.InfrastructureError));

        Assert.Equal(2, result.ExitCode);
    }

    private static void AssertProjectedExitCode (
        ApplicationFailure failure,
        int expectedExitCode)
    {
        var result = CommandFailureProjector.Create("test", failure);

        Assert.Equal(expectedExitCode, result.ExitCode);
        Assert.Single(result.Errors);
    }
}
