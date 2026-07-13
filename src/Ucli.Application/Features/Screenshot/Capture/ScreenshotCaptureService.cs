using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Screenshot.Artifacts;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Foundation;
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
    private readonly IScreenshotCaptureIdFactory captureIdFactory;

    /// <summary> Initializes a new screenshot capture service. </summary>
    public ScreenshotCaptureService (
        IProjectContextResolver projectContextResolver,
        IDaemonSessionStore daemonSessionStore,
        IUnityRequestExecutor unityRequestExecutor,
        IScreenshotArtifactStore artifactStore,
        IScreenshotCaptureIdFactory captureIdFactory)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.unityRequestExecutor = unityRequestExecutor ?? throw new ArgumentNullException(nameof(unityRequestExecutor));
        this.artifactStore = artifactStore ?? throw new ArgumentNullException(nameof(artifactStore));
        this.captureIdFactory = captureIdFactory ?? throw new ArgumentNullException(nameof(captureIdFactory));
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

        var command = input.Target == IpcScreenshotTarget.Game
            ? UcliCommandIds.ScreenshotGame
            : UcliCommandIds.ScreenshotScene;
        var contextResult = await projectContextResolver.ResolveAsync(input.ProjectPath, cancellationToken).ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return ScreenshotCaptureResult.Failure(contextResult.Error!);
        }

        var context = contextResult.Context!;
        var timeoutResult = IpcCommandTimeoutResolver.ResolveNormalized(
            input.TimeoutMilliseconds,
            command,
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

        if (!sessionResult.Exists || !IsGuiSession(sessionResult.Session!))
        {
            return ScreenshotCaptureResult.Failure(ExecutionError.InternalError(
                RequiresGuiSessionMessage,
                ScreenshotErrorCodes.ScreenshotRequiresGuiSession));
        }

        var preparation = artifactStore.Prepare(context.UnityProject, captureIdFactory.Create());
        if (!preparation.IsSuccess)
        {
            return ScreenshotCaptureResult.Failure(preparation.Error!);
        }

        var paths = preparation.Paths!;
        ScreenshotCaptureResult captureResult;
        try
        {
            captureResult = await CapturePreparedAsync(
                    input,
                    command,
                    context,
                    timeoutResult.Timeout!.Value,
                    paths,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            await artifactStore.DiscardAsync(paths, CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        if (captureResult.IsSuccess)
        {
            return captureResult;
        }

        var discardResult = await artifactStore.DiscardAsync(paths, CancellationToken.None).ConfigureAwait(false);
        return discardResult.IsSuccess
            ? captureResult
            : ScreenshotCaptureResult.Failure(discardResult.Error!);
    }

    private async ValueTask<ScreenshotCaptureResult> CapturePreparedAsync (
        ScreenshotCaptureInput input,
        UcliCommand command,
        ProjectContext context,
        TimeSpan timeout,
        ScreenshotArtifactPaths paths,
        CancellationToken cancellationToken)
    {
        var target = ContractLiteralCodec.ToValue(input.Target);
        var executionResult = await unityRequestExecutor.ExecuteAsync(
                command,
                UnityExecutionMode.Daemon,
                timeout,
                context.Config,
                context.UnityProject,
                new UnityRequestPayload.ScreenshotCapture(
                    target,
                    input.RequestedWidth,
                    input.RequestedHeight,
                    paths.RawStagingPath,
                    checked((int)timeout.TotalMilliseconds)),
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            return ScreenshotCaptureResult.Failure(CreateError(executionResult.FailureInfo!));
        }

        var response = executionResult.Response!;
        if (response.HasFailureStatus || response.Errors.Count != 0)
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

        var validationError = ValidateResponse(input, paths, screenshotResponse);
        if (validationError is not null)
        {
            return ScreenshotCaptureResult.Failure(validationError);
        }

        var capture = screenshotResponse.Capture;
        var staging = screenshotResponse.Staging;
        var commitResult = await artifactStore.CommitAsync(
                new ScreenshotArtifactCommitRequest(
                    paths,
                    staging.Path,
                    capture.Width,
                    capture.Height,
                    staging.PixelFormat,
                    staging.RowOrder,
                    staging.RowStrideBytes,
                    staging.SizeBytes),
                cancellationToken)
            .ConfigureAwait(false);
        if (!commitResult.IsSuccess)
        {
            return ScreenshotCaptureResult.Failure(commitResult.Error!);
        }

        var artifact = commitResult.Artifact!;
        return ScreenshotCaptureResult.Success(new ScreenshotCaptureOutput(
            ProjectIdentityInfo.From(context.UnityProject),
            input.Target,
            capture.RequestedWidth,
            capture.RequestedHeight,
            capture.Width,
            capture.Height,
            capture.ColorSpace,
            capture.LifecycleStateAtCapture,
            capture.CompileStateAtCapture,
            capture.DomainReloadGeneration,
            capture.PlayModeState,
            artifact.Path,
            artifact.Digest,
            artifact.SizeBytes,
            artifact.CreatedAtUtc));
    }

    private static ExecutionError? ValidateInput (ScreenshotCaptureInput input)
    {
        var hasWidth = input.RequestedWidth.HasValue;
        var hasHeight = input.RequestedHeight.HasValue;
        if (hasWidth != hasHeight
            || (hasWidth && input.RequestedWidth!.Value <= 0)
            || (hasHeight && input.RequestedHeight!.Value <= 0))
        {
            return ExecutionError.InvalidArgument(
                "Requested width and height must be omitted together or specified together as positive integers.");
        }

        if (input.Target == IpcScreenshotTarget.Scene && hasWidth)
        {
            return ExecutionError.InvalidArgument("SceneView screenshot capture does not accept a requested resolution.");
        }

        return null;
    }

    private static ExecutionError? ValidateResponse (
        ScreenshotCaptureInput input,
        ScreenshotArtifactPaths paths,
        IpcScreenshotCaptureResponse response)
    {
        if (response.Capture is null || response.Staging is null)
        {
            return InvalidResponse("capture or staging metadata is missing");
        }

        var capture = response.Capture;
        var staging = response.Staging;
        var expectedTarget = ContractLiteralCodec.ToValue(input.Target);
        var expectedSizeMode = input.RequestedWidth.HasValue
            ? IpcScreenshotSizeMode.RequestedResolution
            : IpcScreenshotSizeMode.CurrentSurface;
        if (!string.Equals(capture.Target, expectedTarget, StringComparison.Ordinal)
            || !ContractLiteralCodec.Matches(capture.SizeMode, expectedSizeMode)
            || capture.RequestedWidth != input.RequestedWidth
            || capture.RequestedHeight != input.RequestedHeight)
        {
            return InvalidResponse("capture target or requested-size metadata does not match the request");
        }

        if (capture.Width <= 0
            || capture.Height <= 0
            || capture.Width > IpcScreenshotCaptureLimits.MaximumDimension
            || capture.Height > IpcScreenshotCaptureLimits.MaximumDimension
            || (input.RequestedWidth.HasValue
                && (capture.Width != input.RequestedWidth || capture.Height != input.RequestedHeight)))
        {
            return InvalidResponse("captured dimensions do not satisfy the request");
        }

        if (!ContractLiteralCodec.IsDefined<IpcScreenshotColorSpace>(capture.ColorSpace)
            || !ContractLiteralCodec.IsDefined<IpcEditorLifecycleState>(capture.LifecycleStateAtCapture)
            || !ContractLiteralCodec.IsDefined<IpcCompileState>(capture.CompileStateAtCapture)
            || capture.DomainReloadGeneration < 0
            || !ContractLiteralCodec.IsDefined<IpcPlayModeState>(capture.PlayModeState))
        {
            return InvalidResponse("capture state metadata is invalid");
        }

        if (!string.Equals(staging.Path, paths.RawStagingPath, StringComparison.Ordinal)
            || !ContractLiteralCodec.Matches(staging.PixelFormat, IpcScreenshotPixelFormat.Rgba8Srgb)
            || !ContractLiteralCodec.Matches(staging.RowOrder, IpcScreenshotRowOrder.TopDown)
            || staging.RowStrideBytes <= 0
            || (long)staging.RowStrideBytes != (long)capture.Width * 4
            || staging.SizeBytes != (long)staging.RowStrideBytes * capture.Height
            || staging.SizeBytes > IpcScreenshotCaptureLimits.MaximumRawImageBytes)
        {
            return InvalidResponse("raw staging metadata is invalid");
        }

        return null;
    }

    private static bool IsGuiSession (DaemonSession session)
    {
        return ContractLiteralInputParser.TryParseTrimmed<DaemonEditorMode>(session.EditorMode, out var editorMode)
            && editorMode == DaemonEditorMode.Gui;
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
        var firstError = response.Errors.FirstOrDefault();
        var message = string.IsNullOrWhiteSpace(firstError?.Message)
            ? $"Unity screenshot IPC failed with status '{response.FailureStatus}'."
            : firstError!.Message;
        return firstError?.Code == ExecutionErrorCodes.IpcTimeout
            ? ExecutionError.Timeout(message, firstError.Code)
            : ExecutionError.InternalError(message, firstError?.Code);
    }

    private static ExecutionError InvalidResponse (string detail)
    {
        return ExecutionError.InternalError($"Unity screenshot capture payload is invalid: {detail}.");
    }
}
