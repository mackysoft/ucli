using System.Text.Json.Serialization;
using MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Represents the detailed public error branch for <c>ucli refresh</c>. </summary>
internal sealed record RefreshExecutionErrorCommandPayload (
    ProjectIdentityInfo Project,
    Guid RequestId,
    ExecutionRef? LifecycleExecutionRef,
    ExecutionApplicationState ApplicationState,
    RefreshLifecycleStartEvidence? Refresh,
    UnityEditorObservation? ObservedLifecycle,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ExecutionReadPostcondition? ReadPostcondition)
    : CommandErrorPayload<RefreshExecutionErrorCommandPayload>
{
    public static RefreshExecutionErrorCommandPayload From (
        RefreshExecutionErrorOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new RefreshExecutionErrorCommandPayload(
            output.Project,
            output.RequestId,
            output.LifecycleExecutionRef is null
                ? null
                : RequireFailureReference(
                    output.LifecycleExecutionRef,
                    LifecycleExecutionKind.Refresh),
            output.ApplicationState,
            output.Refresh,
            output.ObservedLifecycle,
            output.ReadPostcondition);
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
}
