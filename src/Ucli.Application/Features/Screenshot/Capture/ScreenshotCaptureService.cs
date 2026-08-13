using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Screenshot.Artifacts;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Application.Shared.Identifiers;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Presentation;

namespace MackySoft.Ucli.Application.Features.Screenshot.Capture;

/// <summary> Captures one GUI Editor presentation surface and commits a validated PNG artifact. </summary>
internal sealed class ScreenshotCaptureService : IScreenshotCaptureService
{
    private const string RequiresGuiSessionMessage = "Screenshot capture requires a registered GUI Editor session.";

    private readonly IProjectContextResolver projectContextResolver;
    private readonly IDaemonSessionStore daemonSessionStore;
    private readonly IUnityRequestExecutor unityRequestExecutor;
    private readonly IScreenshotArtifactStore artifactStore;
    private readonly IGuidGenerator captureIdGenerator;

    /// <summary> Initializes a new screenshot capture service. </summary>
    public ScreenshotCaptureService (
        IProjectContextResolver projectContextResolver,
        IDaemonSessionStore daemonSessionStore,
        IUnityRequestExecutor unityRequestExecutor,
        IScreenshotArtifactStore artifactStore,
        IGuidGenerator captureIdGenerator)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.unityRequestExecutor = unityRequestExecutor ?? throw new ArgumentNullException(nameof(unityRequestExecutor));
        this.artifactStore = artifactStore ?? throw new ArgumentNullException(nameof(artifactStore));
        this.captureIdGenerator = captureIdGenerator ?? throw new ArgumentNullException(nameof(captureIdGenerator));
    }

    /// <inheritdoc />
    public async ValueTask<ScreenshotCaptureResult> CaptureAsync (
        ScreenshotCaptureInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        var inputError = ValidateInput(input);
        if (inputError is not null)
        {
            return ScreenshotCaptureResult.Failure(inputError);
        }

        var contextResult = await projectContextResolver.ResolveAsync(input.ProjectPath, cancellationToken).ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return ScreenshotCaptureResult.Failure(contextResult.Error!);
        }

        var context = contextResult.Context!;
        var timeoutResult = IpcCommandTimeoutResolver.ResolveNormalized(
            input.TimeoutMilliseconds,
            UcliCommandIds.Screenshot,
            context.Config);
        if (!timeoutResult.IsSuccess)
        {
            return ScreenshotCaptureResult.Failure(timeoutResult.Error!);
        }

        var sessionResult = await daemonSessionStore.ReadAsync(
                context.UnityProject.RepositoryRoot,
                context.UnityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!sessionResult.IsSuccess)
        {
            return ScreenshotCaptureResult.Failure(sessionResult.Error!);
        }

        if (!sessionResult.Exists || sessionResult.Session!.EditorMode != UnityEditorMode.Gui)
        {
            return ScreenshotCaptureResult.Failure(ExecutionError.InternalError(
                RequiresGuiSessionMessage,
                ScreenshotErrorCodes.ScreenshotRequiresGuiSession));
        }

        var captureId = captureIdGenerator.Generate();
        var preparation = artifactStore.Prepare(context.UnityProject, captureId);
        if (!preparation.IsSuccess)
        {
            return ScreenshotCaptureResult.Failure(preparation.Error!);
        }

        var artifactLease = preparation.Lease!;
        ScreenshotCaptureResult captureResult;
        try
        {
            captureResult = await CapturePreparedAsync(
                    input,
                    context,
                    timeoutResult.Timeout!.Value,
                    captureId,
                    artifactLease,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            var cleanupResult = artifactLease.Discard();
            if (!cleanupResult.IsSuccess)
            {
                return ScreenshotCaptureResult.Failure(ExecutionError.InternalError(
                    "Screenshot capture was interrupted and artifact cleanup failed. "
                    + $"CaptureError={exception.Message} CleanupError={cleanupResult.Error!.Message}"));
            }

            throw;
        }

        var discardResult = artifactLease.Discard();
        return discardResult.IsSuccess
            ? captureResult
            : ScreenshotCaptureResult.Failure(discardResult.Error!);
    }

    /// <inheritdoc />
    public async ValueTask<ScreenshotCaptureResult> CaptureOnFixedHostAsync (
        ProjectContext context,
        IUnityExecutionHostBinding binding,
        IpcScreenshotTarget target,
        PixelDimensions? requestedDimensions,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(deadline);
        var input = new ScreenshotCaptureInput(
            ProjectPath: null,
            Target: target,
            RequestedDimensions: requestedDimensions,
            TimeoutMilliseconds: null);
        var inputError = ValidateInput(input);
        if (inputError is not null)
        {
            return ScreenshotCaptureResult.Failure(inputError);
        }
        if (!deadline.TryGetRemainingTimeout(out _))
        {
            return ScreenshotCaptureResult.Failure(ExecutionError.Timeout("Program screenshot Step deadline elapsed before capture could begin."));
        }
        if (binding.Target != UnityExecutionTarget.Daemon)
        {
            return ScreenshotCaptureResult.Failure(ExecutionError.InternalError(RequiresGuiSessionMessage, ScreenshotErrorCodes.ScreenshotRequiresGuiSession));
        }
        var captureId = captureIdGenerator.Generate();
        var preparation = artifactStore.Prepare(context.UnityProject, captureId);
        if (!preparation.IsSuccess)
        {
            return ScreenshotCaptureResult.Failure(preparation.Error!);
        }

        var artifactLease = preparation.Lease!;
        try
        {
            var execution = await binding.ExecuteAsync(
                    UcliCommandIds.Screenshot,
                    new UnityRequestPayload.ScreenshotCapture(new IpcScreenshotCaptureRequest(captureId, target, requestedDimensions)),
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!execution.IsSuccess)
            {
                return ScreenshotCaptureResult.Failure(
                    CreateError(execution.FailureInfo!),
                    ScreenshotCaptureFailureDisposition.CommunicationLost);
            }
            if (execution.Response!.Errors.Count != 0)
            {
                return ScreenshotCaptureResult.Failure(CreateError(execution.Response));
            }
            if (!IpcPayloadCodec.TryDeserialize(execution.Response.Payload, out IpcScreenshotCaptureResponse response, out var payloadError))
            {
                return ScreenshotCaptureResult.Failure(
                    ExecutionError.InternalError($"Unity screenshot capture payload is invalid. {payloadError.Message}"),
                    ScreenshotCaptureFailureDisposition.ContractInvalid);
            }
            var validationError = ValidateResponse(input, captureId, response);
            if (validationError is not null)
            {
                return ScreenshotCaptureResult.Failure(validationError, ScreenshotCaptureFailureDisposition.ContractInvalid);
            }
            var committed = await artifactLease.CommitAsync(response.Staging, cancellationToken).ConfigureAwait(false);
            return committed.IsSuccess
                ? ScreenshotCaptureResult.Success(new ScreenshotCaptureOutput(ProjectIdentityInfo.From(context.UnityProject), response.Capture, committed.Artifact!))
                : ScreenshotCaptureResult.Failure(committed.Error!, ScreenshotCaptureFailureDisposition.ContractInvalid);
        }
        finally
        {
            _ = artifactLease.Discard();
        }
    }

    private async ValueTask<ScreenshotCaptureResult> CapturePreparedAsync (
        ScreenshotCaptureInput input,
        ProjectContext context,
        TimeSpan timeout,
        Guid captureId,
        IScreenshotArtifactLease artifactLease,
        CancellationToken cancellationToken)
    {
        var screenshotRequest = new IpcScreenshotCaptureRequest(
            CaptureId: captureId,
            Target: input.Target,
            RequestedDimensions: input.RequestedDimensions);
        var executionResult = await unityRequestExecutor.ExecuteAsync(
                UcliCommandIds.Screenshot,
                UnityExecutionMode.Daemon,
                timeout,
                context.Config,
                context.UnityProject,
                new UnityRequestPayload.ScreenshotCapture(screenshotRequest),
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            return ScreenshotCaptureResult.Failure(CreateError(executionResult.FailureInfo!));
        }

        var response = executionResult.Response!;
        if (response.Errors.Count != 0)
        {
            return ScreenshotCaptureResult.Failure(CreateError(response));
        }

        if (!IpcPayloadCodec.TryDeserialize(
                response.Payload,
                out IpcScreenshotCaptureResponse screenshotResponse,
                out var payloadError))
        {
            return ScreenshotCaptureResult.Failure(ExecutionError.InternalError(
                $"Unity screenshot capture payload is invalid. {payloadError.Message}"));
        }

        var validationError = ValidateResponse(input, captureId, screenshotResponse);
        if (validationError is not null)
        {
            return ScreenshotCaptureResult.Failure(validationError);
        }

        var capture = screenshotResponse.Capture;
        var staging = screenshotResponse.Staging;
        var commitResult = await artifactLease.CommitAsync(
                staging,
                cancellationToken)
            .ConfigureAwait(false);
        if (!commitResult.IsSuccess)
        {
            return ScreenshotCaptureResult.Failure(commitResult.Error!);
        }

        var artifact = commitResult.Artifact!;
        return ScreenshotCaptureResult.Success(new ScreenshotCaptureOutput(
            ProjectIdentityInfo.From(context.UnityProject),
            capture,
            artifact));
    }

    private static ExecutionError? ValidateInput (ScreenshotCaptureInput input)
    {
        if (!TextVocabulary.IsDefined(input.Target))
        {
            return ExecutionError.InvalidArgument(
                $"Screenshot target must be one of: {string.Join(", ", TextVocabulary.GetTexts<IpcScreenshotTarget>())}.");
        }

        var requestedDimensions = input.RequestedDimensions;
        if (requestedDimensions is not null
            && !IpcScreenshotCaptureLimits.TryCalculateRgba8Layout(
                requestedDimensions.Width,
                requestedDimensions.Height,
                out _,
                out _))
        {
            return ExecutionError.InvalidArgument(
                "Requested dimensions exceed the supported screenshot layout.");
        }

        if (input.Target == IpcScreenshotTarget.Scene && requestedDimensions is not null)
        {
            return ExecutionError.InvalidArgument("SceneView screenshot capture does not accept a requested resolution.");
        }

        return null;
    }

    private static ExecutionError? ValidateResponse (
        ScreenshotCaptureInput input,
        Guid captureId,
        IpcScreenshotCaptureResponse response)
    {
        if (response.CaptureId != captureId)
        {
            return InvalidResponse("capture identifier does not match the request");
        }

        var capture = response.Capture;
        var expectedSizeMode = input.RequestedDimensions is not null
            ? IpcScreenshotSizeMode.RequestedResolution
            : IpcScreenshotSizeMode.CurrentSurface;
        if (capture.Target != input.Target
            || capture.SizeMode != expectedSizeMode
            || capture.RequestedDimensions != input.RequestedDimensions)
        {
            return InvalidResponse("capture target or requested-size metadata does not match the request");
        }

        return null;
    }

    private static ExecutionError CreateError (UnityRequestFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return failure.Code == ExecutionErrorCodes.IpcTimeout
            ? ExecutionError.Timeout(failure.Message, failure.Code)
            : ExecutionError.InternalError(failure.Message, failure.Code);
    }

    private static ExecutionError CreateError (UnityRequestResponse response)
    {
        var firstError = response.Errors[0];
        var message = firstError.Message;
        return firstError.Code == ExecutionErrorCodes.IpcTimeout
            ? ExecutionError.Timeout(message, firstError.Code)
            : ExecutionError.InternalError(message, firstError.Code);
    }

    private static ExecutionError InvalidResponse (string detail)
    {
        return ExecutionError.InternalError($"Unity screenshot capture payload is invalid: {detail}.");
    }
}
