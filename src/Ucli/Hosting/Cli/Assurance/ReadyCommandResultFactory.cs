using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Assurance;

/// <summary> Creates command-level JSON results from <c>ready</c> execution results. </summary>
internal static class ReadyCommandResultFactory
{
    /// <summary> Gets the serializer contract used by successful <c>ready</c> payloads. </summary>
    public static JsonTypeInfo SuccessPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(ReadyExecutionOutput));

    /// <summary> Gets the serializer contract used by failed <c>ready</c> payloads. </summary>
    public static JsonTypeInfo ErrorPayloadTypeInfo { get; } =
        CommandErrorPayload.TypeInfo<ReadyFailureCommandPayload>();

    public static object CreateEmptyErrorPayload ()
    {
        return CommandErrorPayload.Empty<ReadyFailureCommandPayload>();
    }

    /// <summary> Creates one command result for <c>ready</c>. </summary>
    public static CommandResult Create (ReadyExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        return executionResult switch
        {
            ReadyExecutionResult.CompletedResult completed => CreateSuccess(completed),
            ReadyExecutionResult.FailedResult failed => CreateFailure(failed),
            _ => throw new ArgumentOutOfRangeException(
                nameof(executionResult),
                executionResult.GetType(),
                "Ready execution result variant is unsupported."),
        };
    }

    /// <summary> Creates one command result for <c>ready</c> from a normalized execution error. </summary>
    public static CommandResult CreateExecutionError (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Create(ReadyExecutionResult.Failed(error, project: null));
    }

    private static CommandResult CreateSuccess (ReadyExecutionResult.CompletedResult executionResult)
    {
        return CommandResult.CompletedWithVerdict(
            UcliCommandNames.Ready,
            executionResult.Message,
            executionResult.Output);
    }

    private static CommandResult CreateFailure (ReadyExecutionResult.FailedResult executionResult)
    {
        var startupFailure = StartupFailureFinder.FindInFailures([executionResult.Failure]);
        return CommandFailureProjector.Create(
            UcliCommandNames.Ready,
            executionResult.Message,
            CommandErrorPayload.Detailed(new ReadyFailureCommandPayload(
                executionResult.Project,
                startupFailure?.Startup,
                startupFailure?.Diagnosis,
                startupFailure?.RetryDisposition,
                startupFailure?.SafeToRetryImmediately)),
            [executionResult.Failure]);
    }

    private sealed record ReadyFailureCommandPayload (
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        ProjectIdentityInfo? Project,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        DaemonStartupObservationOutput? Startup,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        DaemonDiagnosisOutput? Diagnosis,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        DaemonStartupRetryDisposition? RetryDisposition,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        bool? SafeToRetryImmediately)
        : CommandErrorPayload<ReadyFailureCommandPayload>;
}
