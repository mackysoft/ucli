using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call.Projection;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Phase;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Validation;

namespace MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call;

/// <summary> Implements the <c>call</c> workflow by combining static preflight, dangerous-op guarding, and Unity IPC execution. </summary>
internal sealed class CallService : ICallService
{
    private readonly IRequestPreparationService requestPreparationService;

    private readonly IPhaseExecutionPreflightService phaseExecutionPreflightService;

    private readonly ICallDangerousOperationGuard dangerousOperationGuard;

    private readonly ICallUnityExecutionService callUnityExecutionService;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="CallService" /> class. </summary>
    /// <param name="requestPreparationService"> The shared request-preparation dependency. </param>
    /// <param name="phaseExecutionPreflightService"> The request preflight dependency. </param>
    /// <param name="dangerousOperationGuard"> The dangerous-operation guard dependency. </param>
    /// <param name="callUnityExecutionService"> The Unity execution coordinator dependency. </param>
    public CallService (
        IRequestPreparationService requestPreparationService,
        IPhaseExecutionPreflightService phaseExecutionPreflightService,
        ICallDangerousOperationGuard dangerousOperationGuard,
        ICallUnityExecutionService callUnityExecutionService,
        TimeProvider? timeProvider = null)
    {
        this.requestPreparationService = requestPreparationService ?? throw new ArgumentNullException(nameof(requestPreparationService));
        this.phaseExecutionPreflightService = phaseExecutionPreflightService ?? throw new ArgumentNullException(nameof(phaseExecutionPreflightService));
        this.dangerousOperationGuard = dangerousOperationGuard ?? throw new ArgumentNullException(nameof(dangerousOperationGuard));
        this.callUnityExecutionService = callUnityExecutionService ?? throw new ArgumentNullException(nameof(callUnityExecutionService));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async ValueTask<CallServiceResult> Execute (
        CallCommandInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

        var requestPreparationResult = await requestPreparationService.Prepare(
                input.ProjectPath,
                input.RequestJson,
                cancellationToken)
            .ConfigureAwait(false);
        var preparedRequestContext = requestPreparationResult.PreparedRequest;
        var baseOutput = CallExecutionOutputFactory.CreateBase(preparedRequestContext?.Request.RequestId);
        if (requestPreparationResult.Error != null)
        {
            return CallFailureResultFactory.FromExecutionError(requestPreparationResult.Error, baseOutput);
        }

        if (preparedRequestContext == null)
        {
            throw new InvalidOperationException("Prepared request must be available when request preparation succeeds.");
        }
        var timeoutResolutionResult = IpcCommandTimeoutResolver.ResolveNormalized(
            input.TimeoutMilliseconds,
            UcliCommandIds.Call,
            preparedRequestContext.ProjectContext.Config);
        if (!timeoutResolutionResult.IsSuccess)
        {
            return CallFailureResultFactory.FromExecutionError(timeoutResolutionResult.Error!, baseOutput);
        }
        var executionMode = input.Mode ?? UnityExecutionMode.Auto;

        var deadline = ExecutionDeadline.Start(timeoutResolutionResult.Timeout!.Value, timeProvider);
        var preflightResult = await phaseExecutionPreflightService.Prepare(
                preparedRequestContext,
                executionMode,
                deadline,
                input.FailFast,
                cancellationToken)
            .ConfigureAwait(false);

        var preparedRequest = preflightResult.PreparedRequest;
        baseOutput = CallExecutionOutputFactory.CreateBase(preparedRequest?.Request.RequestId);
        if (preflightResult.Error != null)
        {
            return CallFailureResultFactory.FromExecutionError(preflightResult.Error, baseOutput, preflightResult.ErrorCode);
        }

        if (preflightResult.HasValidationErrors)
        {
            return CallFailureResultFactory.FromValidationErrors(preflightResult.ValidationErrors, baseOutput);
        }

        if (preparedRequest == null)
        {
            throw new InvalidOperationException("Prepared request must be available when preflight succeeds.");
        }
        var dangerousValidationFailure = dangerousOperationGuard.Validate(
            preparedRequest,
            input.AllowDangerous);
        if (dangerousValidationFailure != null)
        {
            return CallServiceResult.Failure(
                dangerousValidationFailure.Message,
                [
                    new OperationExecutionError(
                        dangerousValidationFailure.Code,
                        dangerousValidationFailure.Message,
                        dangerousValidationFailure.OpId),
                ],
                ApplicationOutcome.InvalidArgument,
                baseOutput);
        }

        return await callUnityExecutionService.Execute(
                preparedRequest,
                executionMode,
                input,
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
    }

}
