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

        if (executionResult.IsSuccess)
        {
            return CreateSuccess(executionResult);
        }

        var startupFailure = StartupFailureFinder.FindInFailures(executionResult.Errors);
        return CommandFailureProjector.Create(
            UcliCommandNames.Ready,
            executionResult.Message,
            CommandErrorPayload.Detailed(new ReadyFailureCommandPayload(
                executionResult.Project,
                startupFailure?.Startup,
                startupFailure?.Diagnosis,
                startupFailure?.RetryDisposition,
                startupFailure?.SafeToRetryImmediately)),
            executionResult.Errors);
    }

    /// <summary> Creates one command result for <c>ready</c> from a normalized execution error. </summary>
    public static CommandResult CreateExecutionError (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Create(ReadyExecutionResult.Failure(error));
    }

    private static CommandResult CreateSuccess (ReadyExecutionResult executionResult)
    {
        var output = executionResult.Output!;
        return new CommandResult(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            Command: UcliCommandNames.Ready,
            Status: CommandResultStatus.Ok,
            ExitCode: output.Verdict == AssuranceVerdict.Pass
                ? (int)CliExitCode.Success
                : 1,
            Message: executionResult.Message,
            Payload: output,
            Errors: []);
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
