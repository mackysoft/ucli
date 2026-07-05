using MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Assurance;

namespace MackySoft.Ucli.Tests;

public sealed class BuildRunCommandResultFactoryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithPassVerifierVerdict_ReturnsOkStatusAndExitCodeZero ()
    {
        var output = BuildRunTestData.CreateOutput();

        var result = BuildRunCommandResultFactory.Create(BuildExecutionResult.Success(output));

        Assert.Equal(IpcProtocol.StatusOk, result.Status);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Empty(result.Errors);
        Assert.Same(output, result.Payload);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithFailedVerifierVerdict_ReturnsOkStatusAndExitCodeOne ()
    {
        var output = BuildRunTestData.CreateOutput(
            ContractLiteralCodec.ToValue(BuildVerdict.Fail),
            ContractLiteralCodec.ToValue(IpcBuildReportResult.Failed),
            ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Failed),
            errorCount: 1);

        var result = BuildRunCommandResultFactory.Create(BuildExecutionResult.Success(output));

        Assert.Equal(IpcProtocol.StatusOk, result.Status);
        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Errors);
        Assert.Same(output, result.Payload);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithIncompleteVerifierVerdict_ReturnsOkStatusAndExitCodeOne ()
    {
        var output = BuildRunTestData.CreateOutput(ContractLiteralCodec.ToValue(BuildVerdict.Incomplete));

        var result = BuildRunCommandResultFactory.Create(BuildExecutionResult.Success(output));

        Assert.Equal(IpcProtocol.StatusOk, result.Status);
        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Errors);
        Assert.Same(output, result.Payload);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithDirtySceneCommandFailure_ReturnsErrorStatusAndDirtyStatePayload ()
    {
        var dirtyState = new IpcBuildDirtyState(
            Checked: true,
            Dirty: true,
            Coverage: ContractLiteralCodec.ToValue(IpcBuildDirtyStateCoverage.Full),
            Items:
            [
                new IpcBuildDirtyStateItem(
                    ContractLiteralCodec.ToValue(IpcBuildDirtyStateItemKind.Scene),
                    "Assets/Scenes/Main.unity"),
            ]);
        var project = ProjectIdentityInfoTestFactory.Create(projectPath: "/workspace/UnityProject");
        var executionResult = BuildExecutionResult.Failure(
            ExecutionError.InternalError("Dirty scene state is present.", BuildErrorCodes.BuildDirtyStatePresent),
            project,
            dirtyState);

        var result = BuildRunCommandResultFactory.Create(executionResult);

        Assert.Equal(IpcProtocol.StatusError, result.Status);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildDirtyStatePresent, error.Code);
        var payload = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(result.Payload);
        Assert.True(payload.ContainsKey("project"));
        Assert.Same(dirtyState, payload["dirtyState"]);
    }
}
