using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Hosting.Cli.Daemon;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.DaemonStartCommandTestSupport;

namespace MackySoft.Ucli.Tests;

public sealed class DaemonStartCommandTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData("batchmode", UnityEditorMode.Batchmode)]
    [InlineData("gui", UnityEditorMode.Gui)]
    public async Task Start_WhenEditorModeIsSpecified_PassesTypedEditorModeToService (
        string editorModeOption,
        UnityEditorMode expectedEditorMode)
    {
        var service = new RecordingDaemonStartService(DaemonStartExecutionResult.Success(CreateSuccessOutput()));
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        CommandExecutionState.Reset();
        var result = await CommandResultCapture.ExecuteAsync(() => command.StartAsync(
            projectPath: AbsolutePath.Parse(ProjectPathTestValues.RepositoryUnityProject),
            timeout: "1234",
            editorMode: editorModeOption,
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        DaemonStartServiceAssert.StartRequestedWithOptions(
            service,
            ProjectPathTestValues.RepositoryUnityProject,
            1234,
            expectedEditorMode,
            DaemonStartupBlockedProcessPolicy.Auto);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null, DaemonStartupBlockedProcessPolicy.Auto)]
    [InlineData("auto", DaemonStartupBlockedProcessPolicy.Auto)]
    [InlineData("keep", DaemonStartupBlockedProcessPolicy.Keep)]
    [InlineData("terminate", DaemonStartupBlockedProcessPolicy.Terminate)]
    public async Task Start_WhenOnStartupBlockedIsSpecified_PassesTypedPolicyToService (
        string? optionValue,
        DaemonStartupBlockedProcessPolicy expectedPolicy)
    {
        var service = new RecordingDaemonStartService(DaemonStartExecutionResult.Success(CreateSuccessOutput()));
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        CommandExecutionState.Reset();
        var result = await CommandResultCapture.ExecuteAsync(() => command.StartAsync(
            onStartupBlocked: optionValue,
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        DaemonStartServiceAssert.StartRequestedWithOptions(
            service,
            expectedProjectPath: null,
            expectedTimeoutMilliseconds: null,
            expectedEditorMode: null,
            expectedOnStartupBlocked: expectedPolicy);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Start_WhenServiceSucceedsWithCompilingLifecycle_EmitsLifecycleSnapshotPayload ()
    {
        var service = new RecordingDaemonStartService(DaemonStartExecutionResult.Success(CreateSuccessOutput(
            lifecycleState: UnityEditorLifecycleState.Compiling,
            blockingReason: UnityEditorBlockingReason.Compile,
            canAcceptExecutionRequests: false)));
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        CommandExecutionState.Reset();
        var result = await CommandResultCapture.ExecuteWithErrorAsync(() => command.StartAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Equal(string.Empty, result.StdErr);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        JsonAssert.For(outputJson.RootElement.GetProperty("payload"))
            .HasString("startStatus", "started")
            .HasString("daemonStatus", "running")
            .HasString("lifecycleState", TextVocabulary.GetText(UnityEditorLifecycleState.Compiling))
            .HasString("blockingReason", TextVocabulary.GetText(UnityEditorBlockingReason.Compile))
            .HasBoolean("canAcceptExecutionRequests", false);

        var payload = outputJson.RootElement.GetProperty("payload");
        Assert.False(payload.TryGetProperty("runtimeKind", out _));
        Assert.False(payload.GetProperty("session").TryGetProperty("runtimeKind", out _));
        JsonGoldenFileAssert.Matches(CliOutputGoldenFiles.GetPath("daemon", "start-compiling-success.json"), result.StdOut);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenEditorModeIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new RecordingDaemonStartService(DaemonStartExecutionResult.Success(CreateSuccessOutput()));
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        CommandExecutionState.Reset();
        var result = await CommandResultCapture.ExecuteAsync(() => command.StartAsync(
            editorMode: "unsupported",
            cancellationToken: CancellationToken.None));

        DaemonStartCommandAssert.InvalidArgumentReturnedWithoutStartExecution(
            result,
            service);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenFormatIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new RecordingDaemonStartService(DaemonStartExecutionResult.Success(CreateSuccessOutput()));
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        CommandExecutionState.Reset();
        var result = await CommandResultCapture.ExecuteWithErrorAsync(() => command.StartAsync(
            format: "yaml",
            cancellationToken: CancellationToken.None));

        DaemonStartCommandAssert.InvalidArgumentReturnedWithoutStartExecutionAndStandardError(
            result,
            service);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenOnStartupBlockedIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new RecordingDaemonStartService(DaemonStartExecutionResult.Success(CreateSuccessOutput()));
        var command = new DaemonStartCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        CommandExecutionState.Reset();
        var result = await CommandResultCapture.ExecuteAsync(() => command.StartAsync(
            onStartupBlocked: "unsupported",
            cancellationToken: CancellationToken.None));

        DaemonStartCommandAssert.InvalidArgumentReturnedWithoutStartExecution(
            result,
            service);
    }
}
