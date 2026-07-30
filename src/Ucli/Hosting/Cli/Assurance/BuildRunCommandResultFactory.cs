using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Assurance;

/// <summary> Creates command-level JSON results from <c>build run</c> execution results. </summary>
internal static class BuildRunCommandResultFactory
{
    /// <summary> Gets the serializer contract used by successful <c>build.run</c> payloads. </summary>
    public static JsonTypeInfo SuccessPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(BuildExecutionOutput));

    /// <summary> Gets the serializer contract used by failed <c>build.run</c> payloads. </summary>
    public static JsonTypeInfo ErrorPayloadTypeInfo { get; } =
        CommandErrorPayload.TypeInfo<BuildRunFailureCommandPayload>();

    public static object CreateEmptyErrorPayload ()
    {
        return CommandErrorPayload.Empty<BuildRunFailureCommandPayload>();
    }

    /// <summary> Creates one command result for <c>build run</c>. </summary>
    public static CommandResult Create (BuildExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        return executionResult switch
        {
            BuildExecutionResult.CompletedResult completed => CreateSuccess(completed),
            BuildExecutionResult.FailedResult failed => CreateFailure(
                failed.Failure,
                failed.Message,
                failed.Project,
                dirtyState: null),
            BuildExecutionResult.DirtyStateFailedResult failed => CreateFailure(
                failed.Failure,
                failed.Message,
                failed.Project,
                failed.DirtyState),
            _ => throw new ArgumentOutOfRangeException(
                nameof(executionResult),
                executionResult.GetType(),
                "Build execution result variant is unsupported."),
        };
    }

    /// <summary> Creates one command result for <c>build run</c> from a normalized execution error. </summary>
    public static CommandResult CreateExecutionError (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Create(BuildExecutionResult.Failed(
            error,
            project: null));
    }

    private static CommandResult CreateSuccess (BuildExecutionResult.CompletedResult executionResult)
    {
        return CommandResult.CompletedWithVerdict(
            UcliCommandNames.BuildRun,
            executionResult.Message,
            executionResult.Output);
    }

    private static CommandResult CreateFailure (
        ApplicationFailure failure,
        string message,
        ProjectIdentityInfo? project,
        IpcBuildDirtyState? dirtyState)
    {
        var startupFailure = StartupFailureFinder.FindInFailures([failure]);
        return CommandFailureProjector.Create(
            UcliCommandNames.BuildRun,
            message,
            CommandErrorPayload.Detailed(new BuildRunFailureCommandPayload(
                project,
                dirtyState,
                startupFailure?.Startup,
                startupFailure?.Diagnosis,
                startupFailure?.RetryDisposition,
                startupFailure?.SafeToRetryImmediately)),
            [failure]);
    }

    private sealed record BuildRunFailureCommandPayload (
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        ProjectIdentityInfo? Project,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IpcBuildDirtyState? DirtyState,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        DaemonStartupObservationOutput? Startup,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        DaemonDiagnosisOutput? Diagnosis,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        DaemonStartupRetryDisposition? RetryDisposition,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        bool? SafeToRetryImmediately)
        : CommandErrorPayload<BuildRunFailureCommandPayload>;
}
