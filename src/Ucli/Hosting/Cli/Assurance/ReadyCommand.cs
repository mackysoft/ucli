using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli.Assurance;

/// <summary> Provides the ready CLI command entry point. </summary>
internal sealed class ReadyCommand
{
    private readonly IReadyService readyService;

    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the <see cref="ReadyCommand" /> class. </summary>
    public ReadyCommand (
        IReadyService readyService,
        ICommandResultWriter commandResultWriter)
    {
        this.readyService = readyService ?? throw new ArgumentNullException(nameof(readyService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes the ready command and emits the JSON result contract. </summary>
    /// <param name="for">Readiness target (execution|mutation|test|readIndex).</param>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="mode">Unity execution mode (auto|daemon|oneshot).</param>
    /// <param name="readIndexMode">Read-index mode. Supported only with --for readIndex.</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="failFast">--failFast, Fails immediately when Unity editor lifecycle is not yet ready.</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.Ready)]
    public async Task<int> ReadyAsync (
        string? @for = null,
        string? projectPath = null,
        string? mode = null,
        string? readIndexMode = null,
        string? timeout = null,
        bool failFast = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CommandExecutionState.MarkStarted();

        var targetResult = ReadyTargetOptionNormalizer.Normalize(@for);
        if (!targetResult.IsSuccess)
        {
            var errorResult = ReadyCommandResultFactory.CreateExecutionError(targetResult.Error!);
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var modeResult = ExecutionModeOptionNormalizer.Normalize(mode);
        if (!modeResult.IsSuccess)
        {
            var errorResult = ReadyCommandResultFactory.CreateExecutionError(modeResult.Error!);
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var readIndexModeResult = ReadIndexModeOptionNormalizer.Normalize(readIndexMode);
        if (!readIndexModeResult.IsSuccess)
        {
            var errorResult = ReadyCommandResultFactory.CreateExecutionError(readIndexModeResult.Error!);
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var timeoutResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!timeoutResult.IsSuccess)
        {
            var errorResult = ReadyCommandResultFactory.CreateExecutionError(timeoutResult.Error!);
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var executionResult = await readyService.ExecuteAsync(
                new ReadyCommandInput(
                    ProjectPath: projectPath,
                    Target: targetResult.Target!.Value,
                    Mode: modeResult.Mode,
                    TimeoutMilliseconds: timeoutResult.TimeoutMilliseconds,
                    ReadIndexMode: readIndexModeResult.Mode,
                    IsReadIndexModeSpecified: readIndexMode is not null,
                    FailFast: failFast),
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = ReadyCommandResultFactory.Create(executionResult);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
