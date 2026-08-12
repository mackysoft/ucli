using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Eval;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Requests.Eval.Input;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Provides the eval CLI command entry point. </summary>
internal sealed class EvalCommand
{
    private readonly IEvalService evalService;

    private readonly IEvalSourceInputReader sourceInputReader;

    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the <see cref="EvalCommand" /> class. </summary>
    /// <param name="evalService"> The eval workflow service dependency. </param>
    /// <param name="sourceInputReader"> The eval source input reader dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public EvalCommand (
        IEvalService evalService,
        IEvalSourceInputReader sourceInputReader,
        ICommandResultWriter commandResultWriter)
    {
        this.evalService = evalService ?? throw new ArgumentNullException(nameof(evalService));
        this.sourceInputReader = sourceInputReader ?? throw new ArgumentNullException(nameof(sourceInputReader));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Evaluates C# from --source, --file, or redirected standard input through the dedicated eval protocol. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="mode">Unity execution mode (auto|daemon|oneshot).</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="allowDangerous">--allowDangerous, Explicitly allows dangerous eval execution when the project config also permits it.</param>
    /// <param name="allowPlayMode">--allowPlayMode, Allows Play Mode mutation when the target is a GUI Editor session in Play Mode.</param>
    /// <param name="failFast">--failFast, Fails immediately when Unity editor lifecycle is not yet ready.</param>
    /// <param name="source">C# source text to evaluate.</param>
    /// <param name="file">Path to a C# source file to evaluate.</param>
    /// <param name="sourceKind">--sourceKind, Source interpretation: snippet or compilationUnit.</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.Eval)]
    public async Task<int> EvalAsync (
        [AbsolutePathArgumentParser] AbsolutePath? projectPath = null,
        string? mode = null,
        string? timeout = null,
        bool allowDangerous = false,
        bool allowPlayMode = false,
        bool failFast = false,
        string? source = null,
        [AbsolutePathArgumentParser] AbsolutePath? file = null,
        string? sourceKind = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();
        var requestId = Guid.NewGuid();

        var normalizedTimeoutResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!normalizedTimeoutResult.IsSuccess)
        {
            return WriteExecutionError(normalizedTimeoutResult.Error!);
        }

        var normalizedModeResult = ExecutionModeOptionNormalizer.Normalize(mode);
        if (!normalizedModeResult.IsSuccess)
        {
            return WriteExecutionError(normalizedModeResult.Error!);
        }

        var sourceInputReadResult = await sourceInputReader.ReadAsync(source, file, cancellationToken)
            .ConfigureAwait(false);
        if (!sourceInputReadResult.IsSuccess)
        {
            return WriteExecutionError(sourceInputReadResult.Error!);
        }

        if (!TryParseSourceKind(sourceKind, out var parsedSourceKind))
        {
            return WriteExecutionError(ExecutionError.InvalidArgument("sourceKind must be 'snippet' or 'compilationUnit'."));
        }

        var serviceResult = await evalService.ExecuteAsync(
                requestId,
                new EvalCommandInput(
                    projectPath,
                    normalizedModeResult.Mode,
                    normalizedTimeoutResult.TimeoutMilliseconds,
                    allowDangerous,
                    allowPlayMode,
                    failFast,
                    sourceInputReadResult.Source!,
                    parsedSourceKind),
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = EvalCommandResultFactory.Create(serviceResult);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }

    private int WriteExecutionError (ExecutionError error)
    {
        var commandResult = EvalCommandResultFactory.Create(EvalServiceResult.Failure(error));
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }

    private static bool TryParseSourceKind (string? value, out CsEvalSourceKind sourceKind)
    {
        if (value is null || string.Equals(value, "snippet", StringComparison.Ordinal))
        {
            sourceKind = CsEvalSourceKind.Snippet;
            return true;
        }

        if (string.Equals(value, "compilationUnit", StringComparison.Ordinal))
        {
            sourceKind = CsEvalSourceKind.CompilationUnit;
            return true;
        }

        sourceKind = default;
        return false;
    }
}
