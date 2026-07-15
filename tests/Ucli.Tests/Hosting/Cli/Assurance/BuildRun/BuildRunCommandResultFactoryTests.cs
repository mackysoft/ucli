using MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;
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

        Assert.Equal(CommandResultStatus.Ok, result.Status);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Empty(result.Errors);
        Assert.Same(output, result.Payload);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithFailedVerifierVerdict_ReturnsOkStatusAndExitCodeOne ()
    {
        var output = BuildRunTestData.CreateOutput(
            AssuranceVerdict.Fail,
            IpcBuildReportResult.Failed,
            IpcBuildLogCompletionReason.Failed,
            errorCount: 1);

        var result = BuildRunCommandResultFactory.Create(BuildExecutionResult.Success(output));

        Assert.Equal(CommandResultStatus.Ok, result.Status);
        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Errors);
        Assert.Same(output, result.Payload);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithIncompleteVerifierVerdict_ReturnsOkStatusAndExitCodeOne ()
    {
        var output = BuildRunTestData.CreateOutput(AssuranceVerdict.Incomplete);

        var result = BuildRunCommandResultFactory.Create(BuildExecutionResult.Success(output));

        Assert.Equal(CommandResultStatus.Ok, result.Status);
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
            Coverage: IpcBuildDirtyStateCoverage.Full,
            Items:
            [
                new IpcBuildDirtyStateItem(
                    IpcBuildDirtyStateItemKind.Scene,
                    "Assets/Scenes/Main.unity"),
            ]);
        var project = ProjectIdentityInfoTestFactory.CreateWithProjectPath(projectPath: ProjectPathTestValues.WorkspaceUnityProject);
        var executionResult = BuildExecutionResult.Failure(
            ExecutionError.InternalError("Dirty scene state is present.", BuildErrorCodes.BuildDirtyStatePresent),
            project,
            dirtyState);

        var result = BuildRunCommandResultFactory.Create(executionResult);

        Assert.Equal(CommandResultStatus.Error, result.Status);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildDirtyStatePresent, error.Code);
        var payload = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(result.Payload);
        Assert.True(payload.ContainsKey("project"));
        Assert.Same(dirtyState, payload["dirtyState"]);
    }
}
