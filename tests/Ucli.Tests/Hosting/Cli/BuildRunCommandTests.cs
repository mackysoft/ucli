using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class BuildRunCommandTests
{
    private static readonly string[] CurrentArtifactNames =
    [
        "build.json",
        "build-report.json",
        "build.log",
        "output-manifest.json",
        "output/",
    ];

    [Fact]
    [Trait("Size", "Small")]
    public async Task Run_MapsOptionsToServiceInputAndCancellationToken ()
    {
        var service = new StubBuildService((_, _, _) => ValueTask.FromResult(BuildExecutionResult.Success(BuildRunTestData.CreateOutput())));
        var command = new BuildRunCommand(service, CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();

        await StandardOutputCapture.ExecuteAsync(() => command.RunAsync(
            profilePath: "/repo/.ucli/build/player.json",
            projectPath: "/repo/UnityProject",
            mode: "daemon",
            timeout: "120000",
            format: "json",
            cancellationToken: cancellationTokenSource.Token));

        Assert.Equal(cancellationTokenSource.Token, service.CapturedCancellationToken);
        var input = Assert.IsType<BuildCommandInput>(service.CapturedInput);
        Assert.Equal("/repo/.ucli/build/player.json", input.ProfilePath);
        Assert.Equal("/repo/UnityProject", input.ProjectPath);
        Assert.Equal(UnityExecutionMode.Daemon, input.Mode);
        Assert.Equal(120000, input.TimeoutMilliseconds);
        Assert.NotNull(service.CapturedProgressSink);
    }

    [Theory]
    [InlineData("unknown", null, null)]
    [InlineData(null, "not-an-int", null)]
    [InlineData(null, null, "xml")]
    [Trait("Size", "Small")]
    public async Task Run_WhenNormalizedOptionIsInvalid_ReturnsInvalidArgumentWithoutCallingService (
        string? mode,
        string? timeout,
        string? format)
    {
        var service = new StubBuildService((_, _, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new BuildRunCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.RunAsync(
            mode: mode,
            timeout: timeout,
            format: format,
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Equal(string.Empty, standardError);
        Assert.Null(service.CapturedInput);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.BuildRun,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [Trait("Size", "Small")]
    public async Task Run_WithoutProfilePath_ReturnsInvalidArgumentWithoutCallingService (string? profilePath)
    {
        var service = new StubBuildService((_, _, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new BuildRunCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.RunAsync(
            profilePath: profilePath,
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Equal(string.Empty, standardError);
        Assert.Null(service.CapturedInput);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.BuildRun,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Run_WithPassOutput_MatchesGolden ()
    {
        var service = new StubBuildService((_, _, _) => ValueTask.FromResult(BuildExecutionResult.Success(BuildRunTestData.CreateOutput())));
        var command = new BuildRunCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.RunAsync(
            profilePath: "/repo/.ucli/build/player.json",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Equal(string.Empty, standardError);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("build", "pass-success.json"),
            standardOutput);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Run_WithJsonFormat_WritesProgressEntryToStandardError ()
    {
        var service = new StubBuildService(async (_, progressSink, cancellationToken) =>
        {
            Assert.NotNull(progressSink);
            await progressSink!.OnEntryAsync(
                    BuildRunProgressEventNames.Completed,
                    BuildRunTestData.CreateCompletedEntry(),
                    cancellationToken)
                .ConfigureAwait(false);
            return BuildExecutionResult.Success(BuildRunTestData.CreateOutput());
        });
        var command = new BuildRunCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.RunAsync(
            profilePath: "/repo/.ucli/build/player.json",
            format: "json",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.BuildRun,
            IpcProtocol.StatusOk,
            (int)CliExitCode.Success);

        var line = Assert.Single(standardError.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        using var entryJson = JsonDocument.Parse(line);
        AssertBuildStreamEnvelope(entryJson.RootElement, sequence: 1, BuildRunProgressEventNames.Completed);
        Assert.Equal(BuildRunTestData.RunId, entryJson.RootElement.GetProperty("payload").GetProperty("runId").GetString());
        Assert.Equal("pass", entryJson.RootElement.GetProperty("payload").GetProperty("verdict").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Run_WithDefaultFormat_WritesTextProgressToStandardError ()
    {
        var service = new StubBuildService(async (_, progressSink, cancellationToken) =>
        {
            Assert.NotNull(progressSink);
            await progressSink!.OnEntryAsync(
                    BuildRunProgressEventNames.Started,
                    BuildRunTestData.CreateStartedEntry(),
                    cancellationToken)
                .ConfigureAwait(false);
            await progressSink.OnEntryAsync(
                    BuildRunProgressEventNames.Completed,
                    BuildRunTestData.CreateCompletedEntry(),
                    cancellationToken)
                .ConfigureAwait(false);
            return BuildExecutionResult.Success(BuildRunTestData.CreateOutput());
        });
        var command = new BuildRunCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.RunAsync(
            profilePath: "/repo/.ucli/build/player.json",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.BuildRun,
            IpcProtocol.StatusOk,
            (int)CliExitCode.Success);
        Assert.Equal(
            "build runId=build-run-1 buildTarget=standaloneLinux64 requestedMode=daemon resolvedMode=daemon sessionKind=daemon timeoutMs=120000 started" + Environment.NewLine
                + "build runId=build-run-1 verdict=pass result=succeeded completionReason=completed errorCount=0 warningCount=1 completed" + Environment.NewLine,
            standardError);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task BuildRun_WithHelpOutput_ExposesOnlySpecifiedBuildRunOptions ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Build,
            UcliCommandNames.RunSubcommand,
            "--help");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains(UcliContractConstants.CliOption.ProfilePath, result.StdOut, StringComparison.Ordinal);
        Assert.Contains(UcliContractConstants.CliOption.ProjectPath, result.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain(UcliContractConstants.CliOption.BuildTarget, result.StdOut, StringComparison.Ordinal);
        Assert.Contains("-p", result.StdOut, StringComparison.Ordinal);
        Assert.Contains(UcliContractConstants.CliOption.Mode, result.StdOut, StringComparison.Ordinal);
        Assert.Contains(UcliContractConstants.CliOption.Timeout, result.StdOut, StringComparison.Ordinal);
        Assert.Contains("--format", result.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain(UcliContractConstants.CliOption.OutputPath, result.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("profile init", result.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("allowDirty", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task BuildRun_WithHelpOutput_DescribesBuildArtifacts ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Build,
            UcliCommandNames.RunSubcommand,
            "--help");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        foreach (var artifactName in CurrentArtifactNames)
        {
            Assert.Contains(artifactName, result.StdOut, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(UcliContractConstants.CliOption.ProjectPath)]
    [InlineData("-p")]
    [Trait("Size", "Medium")]
    public async Task BuildRun_WithPublicOptions_ReachesProjectPathValidation (string projectPathOption)
    {
        using var scope = TestDirectories.CreateTempScope(
            nameof(BuildRunCommandTests),
            nameof(BuildRun_WithPublicOptions_ReachesProjectPathValidation));
        var missingProfilePath = scope.GetPath("missing-build-profile.json");
        var missingProjectPath = scope.GetPath("missing-unity-project");

        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Build,
            UcliCommandNames.RunSubcommand,
            UcliContractConstants.CliOption.ProfilePath,
            missingProfilePath,
            projectPathOption,
            missingProjectPath,
            UcliContractConstants.CliOption.Mode,
            "daemon",
            UcliContractConstants.CliOption.Timeout,
            "1",
            "--format",
            "json");

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.True(string.IsNullOrEmpty(result.StdErr), result.StdErr);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.BuildRun,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, ProjectContextErrorCodes.ProjectPathNotFound);
    }

    [Theory]
    [InlineData(UcliContractConstants.CliOption.Mode, "unsupported")]
    [InlineData(UcliContractConstants.CliOption.Timeout, "abc")]
    [InlineData("--format", "xml")]
    [Trait("Size", "Medium")]
    public async Task BuildRun_ProcessWithInvalidNormalizedOption_ReturnsJsonInvalidArgument (
        string option,
        string value)
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Build,
            UcliCommandNames.RunSubcommand,
            option,
            value);

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.True(string.IsNullOrEmpty(result.StdErr), result.StdErr);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.BuildRun,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task BuildRun_ProcessWithoutProfilePath_ReturnsJsonInvalidArgument ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Build,
            UcliCommandNames.RunSubcommand,
            UcliContractConstants.CliOption.Mode,
            "daemon");

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.True(string.IsNullOrEmpty(result.StdErr), result.StdErr);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.BuildRun,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    [Theory]
    [InlineData(UcliContractConstants.CliOption.OutputPath)]
    [InlineData(UcliContractConstants.CliOption.BuildTarget)]
    [Trait("Size", "Medium")]
    public async Task BuildRun_WithUnspecifiedBuildOption_ReturnsJsonInvalidArgument (string option)
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Build,
            UcliCommandNames.RunSubcommand,
            option,
            "/tmp/output");

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.Contains($"Argument '{option}' is not recognized.", result.StdErr, StringComparison.Ordinal);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.BuildRun,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    private static void AssertBuildStreamEnvelope (
        JsonElement root,
        int sequence,
        string eventName)
    {
        Assert.Equal(1, root.GetProperty("protocolVersion").GetInt32());
        Assert.Equal(UcliCommandNames.BuildRun, root.GetProperty("command").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("streamId").GetString()));
        Assert.Equal(sequence, root.GetProperty("sequence").GetInt32());
        Assert.True(DateTimeOffset.TryParse(root.GetProperty("timestamp").GetString(), out _));
        Assert.Equal(eventName, root.GetProperty("event").GetString());
        Assert.Equal(JsonValueKind.Object, root.GetProperty("payload").ValueKind);
    }

    private sealed class StubBuildService : IBuildService
    {
        private readonly Func<BuildCommandInput, ICommandProgressSink?, CancellationToken, ValueTask<BuildExecutionResult>> execute;

        public StubBuildService (
            Func<BuildCommandInput, ICommandProgressSink?, CancellationToken, ValueTask<BuildExecutionResult>> execute)
        {
            this.execute = execute;
        }

        public BuildCommandInput? CapturedInput { get; private set; }

        public ICommandProgressSink? CapturedProgressSink { get; private set; }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public ValueTask<BuildExecutionResult> ExecuteAsync (
            BuildCommandInput input,
            ICommandProgressSink? progressSink = null,
            CancellationToken cancellationToken = default)
        {
            CapturedInput = input;
            CapturedProgressSink = progressSink;
            CapturedCancellationToken = cancellationToken;
            return execute(input, progressSink, cancellationToken);
        }
    }
}
