using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Screenshot.Artifacts;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Application.Shared.Identifiers;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

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

        if (!sessionResult.Exists || sessionResult.Session!.EditorMode != DaemonEditorMode.Gui)
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
            RequestedWidth: input.RequestedWidth,
            RequestedHeight: input.RequestedHeight);
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
        if (!ContractLiteralCodec.IsDefined(input.Target))
        {
            return ExecutionError.InvalidArgument(
                $"Screenshot target must be one of: {string.Join(", ", ContractLiteralCodec.GetLiterals<IpcScreenshotTarget>())}.");
        }

        var hasWidth = input.RequestedWidth.HasValue;
        var hasHeight = input.RequestedHeight.HasValue;
        if (hasWidth != hasHeight
            || (hasWidth && input.RequestedWidth!.Value <= 0)
            || (hasHeight && input.RequestedHeight!.Value <= 0)
            || (hasWidth && !IpcScreenshotCaptureLimits.TryCalculateRgba8Layout(
                input.RequestedWidth!.Value,
                input.RequestedHeight!.Value,
                out _,
                out _)))
        {
            return ExecutionError.InvalidArgument(
                "Requested width and height must be omitted together or specified together within the supported screenshot layout.");
        }

        if (input.Target == IpcScreenshotTarget.Scene && hasWidth)
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
        var expectedSizeMode = input.RequestedWidth.HasValue
            ? IpcScreenshotSizeMode.RequestedResolution
            : IpcScreenshotSizeMode.CurrentSurface;
        if (capture.Target != input.Target
            || capture.SizeMode != expectedSizeMode
            || capture.RequestedWidth != input.RequestedWidth
            || capture.RequestedHeight != input.RequestedHeight)
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
