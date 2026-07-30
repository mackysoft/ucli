using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Assurance;

namespace MackySoft.Ucli.Tests;

public sealed class BuildRunCommandResultFactoryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithPassVerdict_ReturnsOkStatusAndExitCodeZero ()
    {
        var output = BuildRunTestData.CreatePassedOutput();

        var result = BuildRunCommandResultFactory.Create(BuildExecutionResult.Completed(output));

        Assert.Equal(CommandResultStatus.Ok, result.Status);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Empty(result.Errors);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithFailVerdict_ReturnsOkStatusAndExitCodeOne ()
    {
        var output = BuildRunTestData.CreateFailedOutput();

        var result = BuildRunCommandResultFactory.Create(BuildExecutionResult.Completed(output));

        Assert.Equal(CommandResultStatus.Ok, result.Status);
        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Errors);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithIncompleteVerdict_ReturnsOkStatusAndExitCodeOne ()
    {
        var output = BuildRunTestData.CreateIncompleteOutput();

        var result = BuildRunCommandResultFactory.Create(BuildExecutionResult.Completed(output));

        Assert.Equal(CommandResultStatus.Ok, result.Status);
        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Errors);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithoutBuildReport_OmitsConditionalReportProperty ()
    {
        var source = BuildRunTestData.CreatePassedOutput();
        var output = new BuildExecutionOutput(
            source.Project,
            source.Build,
            source.Verifiers,
            source.Claims,
            new BuildReportsOutput(
                Build: source.Reports.Build,
                BuildReport: null,
                BuildOutputManifest: source.Reports.BuildOutputManifest,
                BuildLog: source.Reports.BuildLog),
            source.ResidualRisks);

        var result = BuildRunCommandResultFactory.Create(BuildExecutionResult.Completed(output));
        var payload = JsonSerializer.SerializeToElement(
            result.Payload,
            CliOutputJsonSerializerOptions.Default);

        Assert.False(payload.GetProperty("reports").TryGetProperty("buildReport", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithDirtySceneCommandFailure_ReturnsErrorStatusAndDirtyStatePayload ()
    {
        var dirtyState = new IpcBuildDirtyState(
            Dirty: true,
            Coverage: IpcBuildDirtyStateCoverage.Full,
            Items:
            [
                new IpcBuildDirtyStateItem(
                    IpcBuildDirtyStateItemKind.Scene,
                    new ProjectMutationAuditPath("Assets/Scenes/Main.unity")),
            ]);
        var project = ProjectIdentityInfoTestFactory.CreateWithProjectPath(projectPath: ProjectPathTestValues.WorkspaceUnityProject);
        var executionResult = BuildExecutionResult.FailedWithDirtyState(
            ExecutionError.InternalError("Dirty scene state is present.", BuildErrorCodes.BuildDirtyStatePresent),
            project,
            dirtyState);

        var result = BuildRunCommandResultFactory.Create(executionResult);

        Assert.Equal(CommandResultStatus.Error, result.Status);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildDirtyStatePresent, error.Code);
        var payload = JsonSerializer.SerializeToElement(
            result.Payload,
            CliOutputJsonSerializerOptions.Default);
        Assert.Equal(JsonValueKind.Object, payload.GetProperty("project").ValueKind);
        Assert.True(payload.GetProperty("dirtyState").GetProperty("dirty").GetBoolean());
    }
}
