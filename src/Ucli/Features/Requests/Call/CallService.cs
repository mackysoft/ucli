using System.Text.Json;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Validation;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Hosting.Cli;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Requests.Call;

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
                input.RequestPath,
                input.ProjectPath,
                cancellationToken)
            .ConfigureAwait(false);
        var preparedRequestContext = requestPreparationResult.PreparedRequest;
        var baseOutput = TryCreateBaseOutput(preparedRequestContext?.Request.RequestId);
        if (requestPreparationResult.Error != null)
        {
            return CreateFailureFromExecutionError(requestPreparationResult.Error, baseOutput);
        }

        if (preparedRequestContext == null)
        {
            throw new InvalidOperationException("Prepared request must be available when request preparation succeeds.");
        }
        var timeoutResolutionResult = IpcCommandTimeoutResolver.Resolve(
            input.Timeout,
            UcliCommandIds.Call,
            preparedRequestContext.ProjectContext.Config);
        if (!timeoutResolutionResult.IsSuccess)
        {
            return CreateFailureFromExecutionError(timeoutResolutionResult.Error!, baseOutput);
        }

        var executionModeResult = UnityExecutionModeResolver.Resolve(input.Mode);
        if (!executionModeResult.IsSuccess)
        {
            return CreateFailureFromExecutionError(executionModeResult.Error!, baseOutput);
        }

        var deadline = ExecutionDeadline.Start(timeoutResolutionResult.Timeout!.Value, timeProvider);
        var preflightResult = await phaseExecutionPreflightService.Prepare(
                preparedRequestContext,
                executionModeResult.Mode!.Value,
                deadline,
                input.FailFast,
                cancellationToken)
            .ConfigureAwait(false);

        var preparedRequest = preflightResult.PreparedRequest;
        baseOutput = TryCreateBaseOutput(preparedRequest?.Request.RequestId);
        if (preflightResult.Error != null)
        {
            return CreateFailureFromExecutionError(preflightResult.Error, baseOutput, preflightResult.ErrorCode);
        }

        if (preflightResult.HasValidationErrors)
        {
            return CallServiceResult.Failure(
                "Static validation failed.",
                ConvertValidationErrors(preflightResult.ValidationErrors),
                (int)CliExitCode.InvalidArgument,
                baseOutput);
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
                    new IpcError(
                        dangerousValidationFailure.Code,
                        dangerousValidationFailure.Message,
                        dangerousValidationFailure.OpId),
                ],
                (int)CliExitCode.InvalidArgument,
                baseOutput);
        }

        return await callUnityExecutionService.Execute(
                preparedRequest,
                executionModeResult.Mode!.Value,
                input,
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static CallExecutionOutput? TryCreateBaseOutput (string? requestId)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            return null;
        }

        return new CallExecutionOutput(
            RequestId: requestId,
            OpResults: [],
            Plan: null);
    }

    private static IReadOnlyList<IpcError> ConvertValidationErrors (IReadOnlyList<ValidationError> validationErrors)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);

        var errors = new IpcError[validationErrors.Count];
        for (var i = 0; i < validationErrors.Count; i++)
        {
            var validationError = validationErrors[i];
            errors[i] = new IpcError(validationError.Code, validationError.Message, validationError.OpId);
        }

        return errors;
    }

    private static CallServiceResult CreateFailureFromExecutionError (
        ExecutionError error,
        CallExecutionOutput? output,
        string? errorCode = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        return CallServiceResult.Failure(
            error.Message,
            [
                new IpcError(
                    string.IsNullOrWhiteSpace(errorCode)
                        ? ExecutionErrorKindCodeMapper.ToCode(error.Kind)
                        : errorCode,
                    error.Message,
                    null),
            ],
            error.Kind == ExecutionErrorKind.InvalidArgument
                ? (int)CliExitCode.InvalidArgument
                : (int)CliExitCode.ToolError,
            output);
    }
}