using System.Text.Json.Serialization.Metadata;
using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Contracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Common.Streaming;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli.Daemon;

/// <summary> Provides the daemon start CLI command entry point. </summary>
internal sealed class DaemonStartCommand
{
    /// <summary> Gets the serializer contract used by successful <c>daemon start</c> payloads. </summary>
    public static JsonTypeInfo SuccessPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(DaemonStartExecutionOutput));

    /// <summary> Gets the serializer contract used by failed <c>daemon start</c> payloads. </summary>
    public static JsonTypeInfo ErrorPayloadTypeInfo { get; } =
        CommandErrorPayload.TypeInfo<DaemonStartErrorCommandPayload>();

    public static object CreateEmptyErrorPayload ()
    {
        return CommandErrorPayload.Empty<DaemonStartErrorCommandPayload>();
    }

    private readonly IDaemonStartService daemonStartService;

    private readonly ICommandResultWriter commandResultWriter;

    private readonly CliStreamEntryWriterFactory streamEntryWriterFactory;

    /// <summary> Initializes a new instance of the DaemonStartCommand class. </summary>
    /// <param name="daemonStartService"> The daemon-start service dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when daemonStartService is null. </exception>
    public DaemonStartCommand (
        IDaemonStartService daemonStartService,
        ICommandResultWriter commandResultWriter,
        CliStreamEntryWriterFactory streamEntryWriterFactory)
    {
        this.daemonStartService = daemonStartService ?? throw new ArgumentNullException(nameof(daemonStartService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
        this.streamEntryWriterFactory = streamEntryWriterFactory ?? throw new ArgumentNullException(nameof(streamEntryWriterFactory));
    }

    /// <summary> Executes the daemon start command and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path. When omitted, the current working directory is used.</param>
    /// <param name="timeout"> Optional daemon start timeout in milliseconds. When omitted, timeout is resolved from config defaults. </param>
    /// <param name="editorMode">--editorMode, Optional daemon Editor mode (batchmode|gui).</param>
    /// <param name="onStartupBlocked">--onStartupBlocked, Optional process policy when startup is blocked before endpoint registration.</param>
    /// <param name="format"> Progress entry format (text|json). </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.StartSubcommand)]
    public async Task<int> StartAsync (
        [AbsolutePathArgumentParser] AbsolutePath? projectPath = null,
        string? timeout = null,
        string? editorMode = null,
        string? onStartupBlocked = null,
        string? format = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var formatResult = CliStreamEntryFormatOptionNormalizer.Normalize(format);
        if (!formatResult.IsSuccess)
        {
            var errorResult = CreateExecutionErrorResult(formatResult.Error!);
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var normalizedTimeoutResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!normalizedTimeoutResult.IsSuccess)
        {
            var errorResult = CreateExecutionErrorResult(normalizedTimeoutResult.Error!);
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var normalizedEditorModeResult = UnityEditorModeOptionNormalizer.Normalize(editorMode);
        if (!normalizedEditorModeResult.IsSuccess)
        {
            var errorResult = CreateExecutionErrorResult(normalizedEditorModeResult.Error!);
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var normalizedOnStartupBlockedResult = DaemonStartupBlockedProcessPolicyOptionNormalizer.Normalize(onStartupBlocked);
        if (!normalizedOnStartupBlockedResult.IsSuccess)
        {
            var errorResult = CreateExecutionErrorResult(normalizedOnStartupBlockedResult.Error!);
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var progressSink = new CliCommandProgressSink(
            formatResult.Format,
            streamEntryWriterFactory.Create(UcliCommandNames.DaemonStart),
            new DaemonStartProgressTextProjector());

        var executionResult = await daemonStartService.StartAsync(
                projectPath,
                normalizedTimeoutResult.TimeoutMilliseconds,
                normalizedEditorModeResult.EditorMode,
                normalizedOnStartupBlockedResult.Policy,
                progressSink,
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = CreateCommandResult(executionResult);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }

    /// <summary> Creates command-level JSON result from daemon-start execution result. </summary>
    /// <param name="executionResult"> The daemon-start execution result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when executionResult is null. </exception>
    private static CommandResult CreateCommandResult (DaemonStartExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        if (executionResult.IsSuccess)
        {
            var output = executionResult.Output!;
            return CommandResult.Success(
                command: UcliCommandNames.DaemonStart,
                message: "uCLI daemon start completed.",
                payload: output);
        }

        if (executionResult.FailureOutput is null)
        {
            return CreateExecutionErrorResult(executionResult.Error!);
        }

        var failureOutput = executionResult.FailureOutput;
        return CommandFailureProjector.Create(
            UcliCommandNames.DaemonStart,
            ApplicationFailure.FromExecutionError(executionResult.Error!),
            payload: CommandErrorPayload.Detailed(new DaemonStartErrorCommandPayload(
                StartStatus: DaemonStartErrorStatus.Failed,
                DaemonStatus: failureOutput.DaemonStatus,
                TimeoutMilliseconds: failureOutput.TimeoutMilliseconds,
                Session: null,
                Startup: failureOutput.Startup,
                Diagnosis: failureOutput.Diagnosis,
                RetryDisposition: failureOutput.RetryDisposition,
                SafeToRetryImmediately: failureOutput.SafeToRetryImmediately)));
    }

    private static CommandResult CreateExecutionErrorResult (ExecutionError error)
    {
        return CommandFailureProjector.Create(
            UcliCommandNames.DaemonStart,
            ApplicationFailure.FromExecutionError(error),
            CreateEmptyErrorPayload());
    }

    private sealed record DaemonStartErrorCommandPayload (
        DaemonStartErrorStatus StartStatus,
        DaemonStatusKind DaemonStatus,
        int TimeoutMilliseconds,
        DaemonSessionOutput? Session,
        DaemonStartupObservationOutput? Startup,
        DaemonDiagnosisOutput? Diagnosis,
        DaemonStartupRetryDisposition RetryDisposition,
        bool SafeToRetryImmediately)
        : CommandErrorPayload<DaemonStartErrorCommandPayload>;

    [VocabularyDefinition]
    private enum DaemonStartErrorStatus
    {
        [VocabularyText("failed")]
        Failed = 0,
    }
}
