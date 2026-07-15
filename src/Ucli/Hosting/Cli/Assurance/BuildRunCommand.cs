using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Common.Streaming;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli.Assurance;

/// <summary> Provides the <c>build run</c> CLI command entry point. </summary>
internal sealed class BuildRunCommand
{
    private readonly IBuildService buildService;

    private readonly ICommandResultWriter commandResultWriter;

    private readonly CliStreamEntryWriterFactory streamEntryWriterFactory;

    /// <summary> Initializes a new instance of the <see cref="BuildRunCommand" /> class. </summary>
    public BuildRunCommand (
        IBuildService buildService,
        ICommandResultWriter commandResultWriter,
        CliStreamEntryWriterFactory streamEntryWriterFactory)
    {
        this.buildService = buildService ?? throw new ArgumentNullException(nameof(buildService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
        this.streamEntryWriterFactory = streamEntryWriterFactory ?? throw new ArgumentNullException(nameof(streamEntryWriterFactory));
    }

    /// <summary> Runs Unity BuildPipeline from a build profile and emits final JSON with build artifacts: build.json, build-report.json, build.log, output-manifest.json, output/. </summary>
    /// <param name="profilePath"> --profilePath, path to the build profile JSON that defines build inputs, runner, and policy. </param>
    /// <param name="projectPath"> -p|--projectPath, optional target Unity project path. </param>
    /// <param name="mode"> Unity execution mode (auto|daemon|oneshot). </param>
    /// <param name="timeout"> Timeout in milliseconds. </param>
    /// <param name="format"> Progress entry format (text|json) for entries written to standard error. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.RunSubcommand)]
    public async Task<int> RunAsync (
        string? profilePath = null,
        string? projectPath = null,
        string? mode = null,
        string? timeout = null,
        string? format = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CommandExecutionState.MarkStarted();

        var formatResult = CliStreamEntryFormatOptionNormalizer.Normalize(format);
        if (!formatResult.IsSuccess)
        {
            var errorResult = BuildRunCommandResultFactory.CreateExecutionError(formatResult.Error!);
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var modeResult = ExecutionModeOptionNormalizer.Normalize(mode);
        if (!modeResult.IsSuccess)
        {
            var errorResult = BuildRunCommandResultFactory.CreateExecutionError(modeResult.Error!);
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var timeoutResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!timeoutResult.IsSuccess)
        {
            var errorResult = BuildRunCommandResultFactory.CreateExecutionError(timeoutResult.Error!);
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        if (string.IsNullOrWhiteSpace(profilePath))
        {
            var errorResult = BuildRunCommandResultFactory.CreateExecutionError(ExecutionError.InvalidArgument(
                "--profilePath is required for build run.",
                UcliCoreErrorCodes.InvalidArgument));
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var progressSink = new CliCommandProgressSink(
            formatResult.Format,
            streamEntryWriterFactory.Create(UcliCommandNames.BuildRun),
            new BuildRunProgressTextProjector());
        var executionResult = await buildService.ExecuteAsync(
                new BuildCommandInput(
                    ProfilePath: profilePath,
                    ProjectPath: projectPath,
                    Mode: modeResult.Mode,
                    TimeoutMilliseconds: timeoutResult.TimeoutMilliseconds),
                progressSink,
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = BuildRunCommandResultFactory.Create(executionResult);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
