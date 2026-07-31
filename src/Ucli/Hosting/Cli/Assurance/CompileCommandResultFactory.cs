using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Assurance;

/// <summary> Creates command-level JSON results from <c>compile</c> execution results. </summary>
internal static class CompileCommandResultFactory
{
    /// <summary> Gets the serializer contract used by successful <c>compile</c> payloads. </summary>
    public static JsonTypeInfo SuccessPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(CompileExecutionOutput));

    /// <summary> Gets the serializer contract used by failed <c>compile</c> payloads. </summary>
    public static JsonTypeInfo ErrorPayloadTypeInfo { get; } =
        CommandErrorPayload.TypeInfo<CompileFailureCommandPayload>();

    public static object CreateEmptyErrorPayload ()
    {
        return CommandErrorPayload.Empty<CompileFailureCommandPayload>();
    }

    /// <summary> Creates one command result for <c>compile</c>. </summary>
    public static CommandResult Create (CompileExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        return executionResult switch
        {
            CompileExecutionResult.CompletedResult completed => CreateSuccess(completed),
            CompileExecutionResult.FailedResult failed => CreateFailure(failed),
            _ => throw new ArgumentOutOfRangeException(
                nameof(executionResult),
                executionResult.GetType(),
                "Compile execution result variant is unsupported."),
        };
    }

    /// <summary> Creates one command result for <c>compile</c> from a normalized execution error. </summary>
    public static CommandResult CreateExecutionError (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return CommandFailureProjector.Create(
            UcliCommandNames.Compile,
            ApplicationFailure.FromExecutionError(error),
            CreateEmptyErrorPayload());
    }

    private static CommandResult CreateSuccess (CompileExecutionResult.CompletedResult executionResult)
    {
        return CommandResult.CompletedWithVerdict(
            UcliCommandNames.Compile,
            executionResult.Message,
            executionResult.Output);
    }

    private static CommandResult CreateFailure (CompileExecutionResult.FailedResult executionResult)
    {
        var payload = executionResult.Project is null
            ? CreateEmptyErrorPayload()
            : CommandErrorPayload.Detailed(new CompileFailureCommandPayload(
                executionResult.Project,
                executionResult.LifecycleExecutionRef is null
                    ? null
                    : RequireFailureReference(
                        executionResult.LifecycleExecutionRef,
                        LifecycleExecutionKind.Compile),
                executionResult.ApplicationState));
        return CommandFailureProjector.Create(
            UcliCommandNames.Compile,
            executionResult.Message,
            payload,
            [executionResult.Failure]);
    }

    private static ExecutionRef RequireFailureReference (
        ExecutionRef executionRef,
        LifecycleExecutionKind expectedKind)
    {
        LifecycleExecutionContractGuard.RequireFailureReference(
            executionRef,
            nameof(executionRef),
            expectedKind);
        return executionRef;
    }

    private sealed record CompileFailureCommandPayload (
        ProjectIdentityInfo Project,
        ExecutionRef? LifecycleExecutionRef,
        ExecutionApplicationState ApplicationState)
        : CommandErrorPayload<CompileFailureCommandPayload>;
}
