using System.Text.Json;
using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Cli.Requests;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Context;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.Operations;
using MackySoft.Ucli.ReadIndex;
using MackySoft.Ucli.Validate;

namespace MackySoft.Ucli.Plan;

/// <summary> Implements the <c>plan</c> workflow by combining static preflight and Unity IPC plan execution. </summary>
internal sealed class PlanService : IPlanService
{
    private const string ReadIndexDisabledReason = "readIndex disabled by mode.";

    private readonly IRequestPreparationService requestPreparationService;

    private readonly IProjectContextResolver projectContextResolver;

    private readonly IValidateMetadataResolver validateMetadataResolver;

    private readonly IRequestStaticValidator requestStaticValidator;

    private readonly IUnityIpcRequestExecutor unityIpcRequestExecutor;

    /// <summary> Initializes a new instance of the <see cref="PlanService" /> class. </summary>
    /// <param name="requestPreparationService"> The request-preparation dependency. </param>
    /// <param name="projectContextResolver"> The project-context resolver dependency. </param>
    /// <param name="validateMetadataResolver"> The validate metadata resolver dependency. </param>
    /// <param name="requestStaticValidator"> The static-validator dependency. </param>
    /// <param name="unityIpcRequestExecutor"> The Unity IPC request executor dependency. </param>
    public PlanService (
        IRequestPreparationService requestPreparationService,
        IProjectContextResolver projectContextResolver,
        IValidateMetadataResolver validateMetadataResolver,
        IRequestStaticValidator requestStaticValidator,
        IUnityIpcRequestExecutor unityIpcRequestExecutor)
    {
        this.requestPreparationService = requestPreparationService ?? throw new ArgumentNullException(nameof(requestPreparationService));
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.validateMetadataResolver = validateMetadataResolver ?? throw new ArgumentNullException(nameof(validateMetadataResolver));
        this.requestStaticValidator = requestStaticValidator ?? throw new ArgumentNullException(nameof(requestStaticValidator));
        this.unityIpcRequestExecutor = unityIpcRequestExecutor ?? throw new ArgumentNullException(nameof(unityIpcRequestExecutor));
    }

    /// <inheritdoc />
    public async ValueTask<PlanServiceResult> Execute (
        PlanCommandInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

        if (input.ReadIndexMode is not null)
        {
            var explicitReadIndexModeResult = ReadIndexModeResolver.Resolve(input.ReadIndexMode, UcliConfig.CreateDefault());
            if (!explicitReadIndexModeResult.IsSuccess)
            {
                return CreateFailure(
                    explicitReadIndexModeResult.Error!.Message,
                    ExecutionErrorKindCodeMapper.ToCode(explicitReadIndexModeResult.Error.Kind),
                    exitCode: explicitReadIndexModeResult.Error.Kind == ExecutionErrorKind.InvalidArgument
                        ? (int)CliExitCode.InvalidArgument
                        : (int)CliExitCode.ToolError);
            }
        }

        var parsedRequestResult = await requestPreparationService.ReadAndParse(
                input.RequestPath,
                cancellationToken)
            .ConfigureAwait(false);
        if (!parsedRequestResult.IsSuccess)
        {
            var error = parsedRequestResult.Error!;
            return CreateFailure(
                error.Message,
                ExecutionErrorKindCodeMapper.ToCode(error.Kind),
                error.Kind == ExecutionErrorKind.InvalidArgument
                    ? (int)CliExitCode.InvalidArgument
                    : (int)CliExitCode.ToolError);
        }

        var parsedRequest = parsedRequestResult.ParsedRequest!;
        if (string.IsNullOrWhiteSpace(parsedRequest.Request.RequestId))
        {
            return CreateFailure(
                "Request payload is invalid. The 'requestId' field is missing.",
                IpcErrorCodes.InvalidArgument,
                (int)CliExitCode.InvalidArgument);
        }

        var requestId = parsedRequest.Request.RequestId;
        var projectContextResult = await projectContextResolver.Resolve(
                input.ProjectPath,
                cancellationToken)
            .ConfigureAwait(false);
        if (!projectContextResult.IsSuccess)
        {
            var error = projectContextResult.Error!;
            return CreateFailure(
                error.Message,
                ExecutionErrorKindCodeMapper.ToCode(error.Kind),
                error.Kind == ExecutionErrorKind.InvalidArgument
                    ? (int)CliExitCode.InvalidArgument
                    : (int)CliExitCode.ToolError);
        }

        var projectContext = projectContextResult.Context!;
        var readIndexModeResult = ReadIndexModeResolver.Resolve(input.ReadIndexMode, projectContext.Config);
        if (!readIndexModeResult.IsSuccess)
        {
            var error = readIndexModeResult.Error!;
            return CreateFailure(
                error.Message,
                ExecutionErrorKindCodeMapper.ToCode(error.Kind),
                error.Kind == ExecutionErrorKind.InvalidArgument
                    ? (int)CliExitCode.InvalidArgument
                    : (int)CliExitCode.ToolError);
        }

        var effectiveReadIndexMode = readIndexModeResult.Mode!.Value;
        var readIndex = effectiveReadIndexMode == ReadIndexMode.Disabled
            ? CreateReadIndexDisabledOutput()
            : default!;
        var staticValidationCatalog = RequestStaticValidationCatalog.Unavailable;
        if (effectiveReadIndexMode != ReadIndexMode.Disabled)
        {
            var metadataResult = await validateMetadataResolver.Resolve(
                    projectContext.UnityProject,
                    effectiveReadIndexMode,
                    cancellationToken)
                .ConfigureAwait(false);
            readIndex = metadataResult.ReadIndex;
            if (!metadataResult.IsSuccess)
            {
                return CreateFailure(
                    metadataResult.ErrorMessage!,
                    metadataResult.ErrorCode!,
                    ExecuteResponseConverter.ResolveExitCode(metadataResult.ErrorCode!),
                    new PlanExecutionOutput(
                        RequestId: requestId,
                        OpResults: [],
                        ReadIndex: readIndex,
                        PlanToken: null));
            }

            staticValidationCatalog = metadataResult.Catalog;
        }

        var validationResult = await requestStaticValidator.Validate(
                parsedRequest.Request,
                staticValidationCatalog,
                projectContext.Config,
                cancellationToken)
            .ConfigureAwait(false);
        var baseOutput = new PlanExecutionOutput(
            RequestId: requestId,
            OpResults: [],
            ReadIndex: readIndex,
            PlanToken: null);
        if (validationResult.Error != null)
        {
            var error = validationResult.Error;
            return CreateFailure(
                error.Message,
                ExecutionErrorKindCodeMapper.ToCode(error.Kind),
                error.Kind == ExecutionErrorKind.InvalidArgument
                    ? (int)CliExitCode.InvalidArgument
                    : (int)CliExitCode.ToolError,
                baseOutput);
        }

        if (!validationResult.IsValid)
        {
            return PlanServiceResult.Failure(
                "Static validation failed.",
                ConvertValidationErrors(validationResult.Errors),
                (int)CliExitCode.InvalidArgument,
                baseOutput);
        }

        var executionResult = await unityIpcRequestExecutor.Execute(
                UcliCommandIds.Plan,
                input.Mode,
                input.Timeout,
                projectContext.Config,
                projectContext.UnityProject,
                IpcMethodNames.Execute,
                CreateExecuteRequestPayload(parsedRequest.RequestJson, input.FailFast),
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            var errorCode = ResolveErrorCode(executionResult.ErrorCode);
            return CreateFailure(
                executionResult.Message,
                errorCode,
                ExecuteResponseConverter.ResolveExitCode(errorCode),
                baseOutput);
        }

        var convertedResponse = ExecuteResponseConverter.Convert(executionResult.Response!);
        var executionOutput = baseOutput with
        {
            OpResults = convertedResponse.OpResults,
        };
        if (!convertedResponse.IsSuccess)
        {
            return PlanServiceResult.Failure(
                ResolveFailureMessage(convertedResponse.Errors, "uCLI plan failed."),
                convertedResponse.Errors,
                convertedResponse.ExitCode,
                executionOutput);
        }

        if (string.IsNullOrWhiteSpace(convertedResponse.PlanToken))
        {
            return CreateFailure(
                "Execute response payload is invalid. The 'planToken' field is missing.",
                IpcErrorCodes.InternalError,
                (int)CliExitCode.ToolError,
                executionOutput);
        }

        return PlanServiceResult.Success(
            executionOutput with
            {
                PlanToken = convertedResponse.PlanToken,
            },
            "uCLI plan completed.");
    }

    private static JsonElement CreateExecuteRequestPayload (
        string requestJson,
        bool failFast)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestJson);

        using var document = JsonDocument.Parse(requestJson);
        return ExecuteRequestPayloadFactory.Create(
            UcliCommandIds.Plan,
            document.RootElement.Clone(),
            failFast);
    }

    private static PlanServiceResult CreateFailure (
        string message,
        string errorCode,
        int exitCode,
        PlanExecutionOutput? output = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);

        return PlanServiceResult.Failure(
            message,
            [
                new IpcError(errorCode, message, null),
            ],
            exitCode,
            output);
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

    private static ReadIndexInfo CreateReadIndexDisabledOutput ()
    {
        return new ReadIndexInfo(
            Used: false,
            Hit: false,
            Source: ReadIndexInfoTextCodec.SourceIndex,
            Freshness: ReadIndexInfoTextCodec.FreshnessProbable,
            GeneratedAtUtc: null,
            FallbackReason: ReadIndexDisabledReason);
    }

    private static string ResolveErrorCode (string? errorCode)
    {
        return string.IsNullOrWhiteSpace(errorCode)
            ? IpcErrorCodes.InternalError
            : errorCode;
    }

    private static string ResolveFailureMessage (
        IReadOnlyList<IpcError> errors,
        string fallbackMessage)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackMessage);

        for (var i = 0; i < errors.Count; i++)
        {
            var error = errors[i];
            if (!string.IsNullOrWhiteSpace(error.Message))
            {
                return error.Message;
            }
        }

        return fallbackMessage;
    }
}