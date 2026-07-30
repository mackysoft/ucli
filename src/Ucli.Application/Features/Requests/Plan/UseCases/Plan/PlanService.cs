using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Plan.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Plan.UseCases.Plan.Projection;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Validation;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Projection;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Application.Features.Requests.Plan.UseCases.Plan;

/// <summary> Implements the <c>plan</c> workflow by combining static-validation preflight and Unity IPC plan execution. </summary>
internal sealed class PlanService : IPlanService
{
    private const string PlayModeReadIndexFallbackReason = "Play Mode mutation uses live Unity state.";

    private readonly IRequestPreparationService requestPreparationService;

    private readonly IRequestStaticValidationPreflightService requestStaticValidationPreflightService;

    private readonly IOperationCatalog operationCatalog;

    private readonly IRequestStaticValidator requestStaticValidator;

    private readonly IUnityRequestExecutor unityIpcRequestExecutor;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="PlanService" /> class. </summary>
    /// <param name="requestPreparationService"> The shared request-preparation dependency. </param>
    /// <param name="requestStaticValidationPreflightService"> The shared static-validation preflight dependency. </param>
    /// <param name="operationCatalog"> The live operation-catalog dependency used when persisted metadata is unavailable or must be bypassed. </param>
    /// <param name="requestStaticValidator"> The product-specific static request validator. </param>
    /// <param name="unityIpcRequestExecutor"> The Unity IPC request executor dependency. </param>
    /// <param name="timeProvider"> The clock used to maintain one timeout budget across catalog discovery and plan execution. </param>
    public PlanService (
        IRequestPreparationService requestPreparationService,
        IRequestStaticValidationPreflightService requestStaticValidationPreflightService,
        IOperationCatalog operationCatalog,
        IRequestStaticValidator requestStaticValidator,
        IUnityRequestExecutor unityIpcRequestExecutor,
        TimeProvider timeProvider)
    {
        this.requestPreparationService = requestPreparationService ?? throw new ArgumentNullException(nameof(requestPreparationService));
        this.requestStaticValidationPreflightService = requestStaticValidationPreflightService ?? throw new ArgumentNullException(nameof(requestStaticValidationPreflightService));
        this.operationCatalog = operationCatalog ?? throw new ArgumentNullException(nameof(operationCatalog));
        this.requestStaticValidator = requestStaticValidator ?? throw new ArgumentNullException(nameof(requestStaticValidator));
        this.unityIpcRequestExecutor = unityIpcRequestExecutor ?? throw new ArgumentNullException(nameof(unityIpcRequestExecutor));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public async ValueTask<PlanServiceResult> ExecuteAsync (
        Guid requestId,
        PlanCommandInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Request id must not be empty.", nameof(requestId));
        }

        ArgumentNullException.ThrowIfNull(input);

        if (input.AllowPlayMode && input.ReadIndexMode != null)
        {
            return PlanFailureResultFactory.FromExecutionError(
                ExecutionError.InvalidArgument("--allowPlayMode cannot be combined with --readIndexMode.", UcliCoreErrorCodes.InvalidArgument),
                output: null);
        }

        var requestPreparationResult = await requestPreparationService.PrepareAsync(
                input.ProjectPath,
                input.RequestJson,
                cancellationToken)
            .ConfigureAwait(false);
        if (!requestPreparationResult.IsSuccess)
        {
            return PlanFailureResultFactory.FromExecutionError(
                requestPreparationResult.Error!,
                output: null);
        }

        var preparedRequest = requestPreparationResult.PreparedRequest!;
        PlanExecutionOutput baseOutput;
        RequestStaticValidationCatalog validationCatalog;
        if (!input.AllowPlayMode)
        {
            var requestStaticValidationPreflightResult = await requestStaticValidationPreflightService.PrepareAsync(
                    preparedRequest,
                    input.ReadIndexMode,
                    cancellationToken)
                .ConfigureAwait(false);

            var preflightPreparedRequest = requestStaticValidationPreflightResult.PreparedRequest;
            var preflightBaseOutput = PlanExecutionOutputFactory.CreateBase(
                requestId,
                preflightPreparedRequest,
                requestStaticValidationPreflightResult.ReadIndex);
            if (requestStaticValidationPreflightResult.Error != null)
            {
                return PlanFailureResultFactory.FromExecutionError(
                    requestStaticValidationPreflightResult.Error,
                    preflightBaseOutput);
            }

            if (requestStaticValidationPreflightResult.HasValidationErrors)
            {
                return PlanFailureResultFactory.FromValidationErrors(
                    requestStaticValidationPreflightResult.ValidationErrors,
                    preflightBaseOutput);
            }

            preparedRequest = preflightPreparedRequest;
            baseOutput = preflightBaseOutput;
            validationCatalog = requestStaticValidationPreflightResult.Catalog;
        }
        else
        {
            preparedRequest = preparedRequest with
            {
                Request = preparedRequest.Request with
                {
                    AllowPlayMode = true,
                },
            };
            baseOutput = PlanExecutionOutputFactory.CreateBase(
                requestId,
                preparedRequest,
                ReadIndexInfoFactory.Unity(PlayModeReadIndexFallbackReason));
            validationCatalog = RequestStaticValidationCatalog.Unavailable;
        }

        var timeoutResolutionResult = IpcCommandTimeoutResolver.ResolveNormalized(
            input.TimeoutMilliseconds,
            UcliCommandIds.Plan,
            preparedRequest.ProjectContext.Config);
        if (!timeoutResolutionResult.IsSuccess)
        {
            return PlanFailureResultFactory.FromExecutionError(
                timeoutResolutionResult.Error!,
                baseOutput);
        }

        var executionMode = input.Mode ?? UnityExecutionMode.Auto;
        var deadline = ExecutionDeadline.Start(
            timeoutResolutionResult.Timeout!.Value,
            timeProvider);
        var requiresLiveCatalog = input.AllowPlayMode
            || (!validationCatalog.IsAvailable
                && HasDirectOperationSteps(preparedRequest.Request));
        if (requiresLiveCatalog)
        {
            if (!deadline.TryGetRemainingTimeout(out var catalogTimeout))
            {
                return PlanFailureResultFactory.FromExecutionError(
                    ExecutionError.Timeout(
                        "Timed out before operation metadata discovery could begin.",
                        ExecutionErrorCodes.IpcTimeout),
                    baseOutput);
            }

            IReadOnlyList<UcliOperationDescriptor> operations;
            try
            {
                operations = await operationCatalog.GetAllAsync(
                        preparedRequest.ProjectContext.UnityProject,
                        preparedRequest.ProjectContext.Config,
                        executionMode,
                        catalogTimeout,
                        input.FailFast,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCatalogLoadException exception)
            {
                var failure = exception.CreatePrefixedFailure(
                        input.AllowPlayMode
                            ? "Play Mode static validation could not load operation metadata."
                            : "Plan response validation could not load operation metadata.");
                return PlanServiceResult.Failure(
                    failure.Message,
                    [failure],
                    baseOutput);
            }
            catch (InvalidOperationException exception)
            {
                return PlanFailureResultFactory.FromExecutionError(
                    ExecutionError.InternalError(
                        $"Plan could not load operation metadata. {exception.Message}",
                        UcliCoreErrorCodes.InternalError),
                    baseOutput);
            }

            validationCatalog = RequestStaticValidationCatalog.Available(operations);
        }

        if (input.AllowPlayMode)
        {
            var validationResult = await requestStaticValidator.ValidateAsync(
                    preparedRequest.Request,
                    validationCatalog,
                    preparedRequest.ProjectContext.Config,
                    cancellationToken)
                .ConfigureAwait(false);
            if (validationResult.Error != null)
            {
                return PlanFailureResultFactory.FromExecutionError(
                    validationResult.Error,
                    baseOutput);
            }

            if (!validationResult.IsValid)
            {
                return PlanFailureResultFactory.FromValidationErrors(
                    validationResult.Errors,
                    baseOutput);
            }
        }

        if (!deadline.TryGetRemainingTimeout(out var planTimeout))
        {
            return PlanFailureResultFactory.FromExecutionError(
                ExecutionError.Timeout(
                    "Timed out before Unity IPC plan request could begin.",
                    ExecutionErrorCodes.IpcTimeout),
                baseOutput);
        }

        var executionResult = await unityIpcRequestExecutor.ExecuteAsync(
                UcliCommandIds.Plan,
                executionMode,
                planTimeout,
                preparedRequest.ProjectContext.Config,
                preparedRequest.ProjectContext.UnityProject,
                CreateExecuteRequestPayload(preparedRequest.RequestJson, input.FailFast, input.AllowPlayMode),
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            var failure = RequestFailureNormalizer.FromUnityRequestFailure(executionResult.FailureInfo!);
            return PlanServiceResult.Failure(
                failure.Message,
                [
                    failure,
                ],
                baseOutput);
        }

        var convertedResponse = ExecuteResponseConverter.Convert(
            executionResult.Response!,
            preparedRequest.ProjectContext.UnityProject);
        if (!OperationExecutionResultContractValidator.TryValidate(
                preparedRequest.Request,
                validationCatalog.OperationsByName,
                IpcExecuteOperationPhase.Plan,
                convertedResponse,
                out var responseContractError))
        {
            return PlanFailureResultFactory.FromExecutionError(
                ExecutionError.InternalError(
                    responseContractError,
                    UcliCoreErrorCodes.InternalError),
                baseOutput);
        }

        var executionOutput = baseOutput with
        {
            Project = convertedResponse.Project ?? baseOutput.Project,
            OpResults = convertedResponse.OpResults,
            ContractViolations = convertedResponse.ContractViolations,
        };
        if (!convertedResponse.IsSuccess)
        {
            var failures = RequestFailureNormalizer.FromOperationErrors(convertedResponse.Errors);
            return PlanServiceResult.Failure(
                failures[0].Message,
                failures,
                executionOutput);
        }

        if (convertedResponse.PlanToken == null)
        {
            return PlanFailureResultFactory.FromExecutionError(
                ExecutionError.InternalError(
                    "Execute response payload is invalid. The 'planToken' field is missing.",
                    UcliCoreErrorCodes.InternalError),
                executionOutput);
        }

        return PlanServiceResult.Success(
            executionOutput with
            {
                PlanToken = convertedResponse.PlanToken,
            },
            "uCLI plan completed.");
    }

    private static UnityRequestPayload CreateExecuteRequestPayload (
        string requestJson,
        bool failFast,
        bool allowPlayMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestJson);

        using var document = JsonDocument.Parse(requestJson);
        return new UnityRequestPayload.ExecuteJson(
            UcliCommandIds.Plan,
            document.RootElement.Clone(),
            failFast,
            AllowPlayMode: allowPlayMode);
    }

    private static bool HasDirectOperationSteps (ValidateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        for (var i = 0; i < request.Steps.Count; i++)
        {
            switch (request.Steps[i].Kind)
            {
                case IpcExecuteStepKind.Op:
                    return true;
                case IpcExecuteStepKind.Edit:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(request),
                        request.Steps[i].Kind,
                        "Prepared request step kind must be a defined value.");
            }
        }

        return false;
    }

}
