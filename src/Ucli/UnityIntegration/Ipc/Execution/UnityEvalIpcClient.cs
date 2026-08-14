using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Execution;

/// <summary>
/// Owns one eval.plan/eval.call exchange. The selected target, endpoint, authorization token, and
/// deadline are fixed before the plan request, so a call is never replayed or moved to another host.
/// </summary>
internal sealed class UnityEvalIpcClient : IUnityEvalClient
{
    private readonly UnityIpcRequestBuilder requestBuilder;
    private readonly UnityIpcExecutionTargetResolver targetResolver;
    private readonly UnityIpcClientSelector clientSelector;
    private readonly UnityDaemonReadinessGate daemonReadinessGate;
    private readonly TimeProvider timeProvider;

    public UnityEvalIpcClient (
        UnityIpcRequestBuilder requestBuilder,
        UnityIpcExecutionTargetResolver targetResolver,
        UnityIpcClientSelector clientSelector,
        UnityDaemonReadinessGate daemonReadinessGate,
        TimeProvider timeProvider)
    {
        this.requestBuilder = requestBuilder ?? throw new ArgumentNullException(nameof(requestBuilder));
        this.targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
        this.clientSelector = clientSelector ?? throw new ArgumentNullException(nameof(clientSelector));
        this.daemonReadinessGate = daemonReadinessGate ?? throw new ArgumentNullException(nameof(daemonReadinessGate));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async ValueTask<UnityEvalExecutionResult> ExecuteAsync (
        UnityExecutionMode mode,
        TimeSpan timeout,
        ResolvedUnityProjectContext unityProject,
        IpcEvalPlanRequest planRequest,
        bool failFast,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(planRequest);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        var targetResolution = await targetResolver.ResolveAsync(mode, unityProject, deadline, cancellationToken)
            .ConfigureAwait(false);
        if (!targetResolution.IsSuccess)
        {
            return UnityEvalExecutionResult.PlanFailure(ToExecutionError(targetResolution.Failure!));
        }

        var planDispatch = requestBuilder.Build(new UnityRequestPayload.EvalPlan(planRequest));
        var client = clientSelector.Select(targetResolution.Target);
        if (client is UnityDaemonIpcClient daemon)
        {
            var binding = await daemon.BindHostAsync(unityProject, deadline, cancellationToken).ConfigureAwait(false);
            if (!binding.IsSuccess)
            {
                return UnityEvalExecutionResult.PlanFailure(ToExecutionError(binding.Failure!));
            }

            return await ExecuteDaemonAsync(daemon, binding.Session!, unityProject, planDispatch, planRequest, failFast, deadline, cancellationToken)
                .ConfigureAwait(false);
        }

        if (client is UnityOneshotIpcClient oneshot)
        {
            var binding = await oneshot.BindHostAsync(unityProject, deadline, cancellationToken).ConfigureAwait(false);
            if (!binding.IsSuccess)
            {
                return UnityEvalExecutionResult.PlanFailure(ToExecutionError(binding.Failure!));
            }

            try
            {
                return await ExecuteOneshotAsync(oneshot, binding.Lease!, planDispatch, planRequest, failFast, deadline, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                await oneshot.DisposeBoundLeaseAsync(binding.Lease!).ConfigureAwait(false);
            }
        }

        return UnityEvalExecutionResult.PlanFailure(ExecutionError.InternalError("The selected Unity IPC client cannot execute eval."));
    }

    private async ValueTask<UnityEvalExecutionResult> ExecuteDaemonAsync (
        UnityDaemonIpcClient client,
        DaemonSession session,
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest planDispatch,
        IpcEvalPlanRequest planRequest,
        bool failFast,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        var plan = await daemonReadinessGate.ExecuteInitialEvalAsync(
                unityProject,
                planDispatch,
                failFast,
                deadline,
                client,
                session,
                cancellationToken)
            .ConfigureAwait(false);
        return await ContinueAsync(
                plan,
                call => client.SendExactAsync(call, deadline, session, cancellationToken),
                planRequest,
                new UnityProjectIdentity(unityProject.UnityProjectRoot.Value, unityProject.ProjectFingerprint, unityProject.UnityVersion),
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<UnityEvalExecutionResult> ExecuteOneshotAsync (
        UnityOneshotIpcClient client,
        OneshotHostLease lease,
        UnityIpcDispatchRequest planDispatch,
        IpcEvalPlanRequest planRequest,
        bool failFast,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        var plan = await client.SendExactAsync(planDispatch, deadline, lease, failFast, cancellationToken).ConfigureAwait(false);
        return await ContinueAsync(
                plan,
                call => client.SendExactAsync(call, deadline, lease, failFast, cancellationToken),
                planRequest,
                new UnityProjectIdentity(lease.Project.UnityProjectRoot.Value, lease.Project.ProjectFingerprint, lease.Project.UnityVersion),
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<UnityEvalExecutionResult> ContinueAsync (
        UnityRequestExecutionResult planResult,
        Func<UnityIpcDispatchRequest, ValueTask<UnityRequestExecutionResult>> sendCall,
        IpcEvalPlanRequest planRequest,
        UnityProjectIdentity expectedProject,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        var expectedSourceDigest = EvalExecutionDigestCalculator.ComputeSourceDigest(planRequest.Source);
        var expectedExecutionDigest = EvalExecutionDigestCalculator.ComputeExecutionDigest(
            planRequest.Source,
            planRequest.SourceKind,
            planRequest.AllowDangerous,
            planRequest.AllowPlayMode);
        if (!TryReadPlan(planResult, out var planResponse, out var planError, out var planErrorResponse))
        {
            if (planErrorResponse is not null
                && !IsExpectedErrorResponse(
                    planErrorResponse,
                    CsEvalPhase.Plan,
                    expectedProject,
                    planRequest.SourceKind,
                    expectedSourceDigest,
                    expectedExecutionDigest,
                    expectedResolvedEntryPoint: null))
            {
                return UnityEvalExecutionResult.PlanFailure(
                    ExecutionError.InternalError("eval.plan returned an error IPC payload that does not match the request."));
            }

            return UnityEvalExecutionResult.PlanFailure(planError!, planErrorResponse);
        }

        if (planResponse.Phase != CsEvalPhase.Plan
            || planResponse.Project != expectedProject
            || planResponse.Eval is not CsEvalPlanSuccessResult
            || planResponse.ApplicationState != ExecutionApplicationState.NotApplied
            || string.IsNullOrWhiteSpace(planResponse.PlanToken)
            || planResponse.ReadPostcondition is not null)
        {
            return UnityEvalExecutionResult.PlanFailure(
                ExecutionError.InternalError("eval.plan returned an invalid successful IPC payload."));
        }

        if (!HasExpectedEvalIdentity(
                planResponse,
                planRequest.SourceKind,
                expectedSourceDigest,
                expectedExecutionDigest,
                expectedResolvedEntryPoint: null))
        {
            return UnityEvalExecutionResult.PlanFailure(ExecutionError.InternalError("eval.plan returned an identity that does not match the CLI input."));
        }

        if (!deadline.TryGetRemainingTimeout(out _))
        {
            return UnityEvalExecutionResult.CallFailure(planResponse, ExecutionError.Timeout("Timed out before eval.call could begin."), false);
        }

        var callRequest = new IpcEvalCallRequest(
            planRequest.Source,
            planRequest.SourceKind,
            planRequest.AllowDangerous,
            planRequest.AllowPlayMode,
            planResponse.PlanToken);
        var callResult = await sendCall(requestBuilder.Build(new UnityRequestPayload.EvalCall(callRequest))).ConfigureAwait(false);
        if (!TryReadCall(callResult, out var callResponse, out var callError, out var callErrorResponse))
        {
            if (callErrorResponse is not null
                && !IsExpectedErrorResponse(
                    callErrorResponse,
                    CsEvalPhase.Call,
                    expectedProject,
                    planRequest.SourceKind,
                    expectedSourceDigest,
                    expectedExecutionDigest,
                    ((CsEvalPlanSuccessResult)planResponse.Eval).ResolvedEntryPoint))
            {
                return UnityEvalExecutionResult.CallFailure(
                    planResponse,
                    ExecutionError.InternalError("eval.call returned an error IPC payload that does not match the request."),
                    true);
            }

            return UnityEvalExecutionResult.CallFailure(planResponse, callError!, true, callErrorResponse);
        }

        if (callResponse.Phase != CsEvalPhase.Call
            || callResponse.Project != expectedProject
            || callResponse.Project != planResponse.Project
            || callResponse.Eval is not CsEvalCallSuccessResult
            || callResponse.ApplicationState != ExecutionApplicationState.Applied
            || callResponse.PlanToken is not null
            || callResponse.ReadPostcondition is null)
        {
            return UnityEvalExecutionResult.CallFailure(
                planResponse,
                ExecutionError.InternalError("eval.call returned an invalid successful IPC payload."),
                true);
        }

        if (!HasExpectedEvalIdentity(
                callResponse,
                planRequest.SourceKind,
                expectedSourceDigest,
                expectedExecutionDigest,
                ((CsEvalPlanSuccessResult)planResponse.Eval).ResolvedEntryPoint))
        {
            return UnityEvalExecutionResult.CallFailure(planResponse, ExecutionError.InternalError("eval.call returned an identity that does not match the CLI input."), true);
        }

        return UnityEvalExecutionResult.Success(planResponse, callResponse);
    }

    private static bool TryReadPlan (
        UnityRequestExecutionResult result,
        out IpcEvalResponse response,
        out ExecutionError? error,
        out IpcEvalErrorResponse? errorResponse)
    {
        if (!result.IsSuccess)
        {
            response = null!;
            error = ToExecutionError(result.FailureInfo!);
            errorResponse = null;
            return false;
        }

        if (result.Response!.Status == IpcResponseStatus.Error)
        {
            response = null!;
            if (!IpcPayloadCodec.TryDeserializeStrict(result.Response.Payload, out errorResponse, out var errorPayloadReadError))
            {
                error = ExecutionError.InternalError($"Eval returned an invalid error IPC payload: {errorPayloadReadError.Message}");
                return false;
            }

            error = ToExecutionError(result.Response.Errors[0]);
            return false;
        }

        if (!IpcPayloadCodec.TryDeserializeStrict(result.Response!.Payload, out response, out var readError))
        {
            error = ExecutionError.InternalError($"Eval returned an invalid IPC payload: {readError.Message}");
            errorResponse = null;
            return false;
        }

        error = null;
        errorResponse = null;
        return true;
    }

    private static bool TryReadCall (
        UnityRequestExecutionResult result,
        out IpcEvalResponse response,
        out ExecutionError? error,
        out IpcEvalErrorResponse? errorResponse) =>
        TryReadPlan(result, out response, out error, out errorResponse);

    private static bool HasExpectedEvalIdentity (
        IpcEvalResponse response,
        CsEvalSourceKind sourceKind,
        MackySoft.Ucli.Contracts.Cryptography.Sha256Digest sourceDigest,
        MackySoft.Ucli.Contracts.Cryptography.Sha256Digest executionDigest,
        string? expectedResolvedEntryPoint)
    {
        return response.Eval switch
        {
            CsEvalPlanSuccessResult plan =>
                plan.SourceKind == sourceKind
                && plan.SourceDigest == sourceDigest
                && plan.ExecutionDigest == executionDigest,
            CsEvalCallSuccessResult call =>
                call.SourceKind == sourceKind
                && call.SourceDigest == sourceDigest
                && call.ExecutionDigest == executionDigest
                && string.Equals(call.ResolvedEntryPoint, expectedResolvedEntryPoint, StringComparison.Ordinal),
            _ => false,
        };
    }

    private static bool IsExpectedErrorResponse (
        IpcEvalErrorResponse response,
        CsEvalPhase expectedPhase,
        UnityProjectIdentity expectedProject,
        CsEvalSourceKind expectedSourceKind,
        MackySoft.Ucli.Contracts.Cryptography.Sha256Digest expectedSourceDigest,
        MackySoft.Ucli.Contracts.Cryptography.Sha256Digest expectedExecutionDigest,
        string? expectedResolvedEntryPoint)
    {
        if (response.Phase != expectedPhase || response.Project != expectedProject)
        {
            return false;
        }

        if (response.Eval is not { } partial)
        {
            return true;
        }

        return partial.SourceKind == expectedSourceKind
            && partial.SourceDigest == expectedSourceDigest
            && partial.ExecutionDigest == expectedExecutionDigest
            && (expectedResolvedEntryPoint is null
                || partial.ResolvedEntryPoint is null
                || string.Equals(partial.ResolvedEntryPoint, expectedResolvedEntryPoint, StringComparison.Ordinal));
    }

    private static ExecutionError ToExecutionError (UnityRequestFailure failure)
    {
        return ExecutionError.InternalError(failure.Message, failure.Code);
    }

    private static ExecutionError ToExecutionError (OperationExecutionError error)
    {
        return ExecutionError.InternalError(error.Message, error.Code);
    }
}
