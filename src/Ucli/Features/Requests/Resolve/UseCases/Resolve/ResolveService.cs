using System.Text.Json;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Requests.Resolve.UseCases.Resolve.Projection;
using MackySoft.Ucli.Features.Requests.Shared.Execution.Conversion;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Context;
using MackySoft.Ucli.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Indexing.Scenes;
using MackySoft.Ucli.UnityIntegration.Indexing.Scenes.Access;

namespace MackySoft.Ucli.Features.Requests.Resolve.UseCases.Resolve;

/// <summary> Implements the <c>resolve</c> workflow across read-index and Unity IPC fallback paths. </summary>
internal sealed class ResolveService : IResolveService
{
    private const string ResolveOperationId = "resolve";

    private readonly IProjectContextResolver projectContextResolver;

    private readonly ISceneTreeLiteAccessService sceneTreeLiteAccessService;

    private readonly IUnityRequestExecutor unityRequestExecutor;

    /// <summary> Initializes a new instance of the <see cref="ResolveService" /> class. </summary>
    public ResolveService (
        IProjectContextResolver projectContextResolver,
        ISceneTreeLiteAccessService sceneTreeLiteAccessService,
        IUnityRequestExecutor unityRequestExecutor)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.sceneTreeLiteAccessService = sceneTreeLiteAccessService ?? throw new ArgumentNullException(nameof(sceneTreeLiteAccessService));
        this.unityRequestExecutor = unityRequestExecutor ?? throw new ArgumentNullException(nameof(unityRequestExecutor));
    }

    /// <inheritdoc />
    public async ValueTask<ResolveServiceResult> Execute (
        ResolveCommandInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Selector);

        var requestId = Guid.NewGuid().ToString("D");
        var projectContextResult = await projectContextResolver.Resolve(input.ProjectPath, cancellationToken).ConfigureAwait(false);
        if (!projectContextResult.IsSuccess)
        {
            return ResolveServiceResultFactory.FromExecutionError(requestId, projectContextResult.Error!);
        }

        var projectContext = projectContextResult.Context!;
        var timeoutResolutionResult = IpcCommandTimeoutResolver.ResolveNormalized(
            input.TimeoutMilliseconds,
            UcliCommandIds.Resolve,
            projectContext.Config);
        if (!timeoutResolutionResult.IsSuccess)
        {
            return ResolveServiceResultFactory.FromExecutionError(requestId, timeoutResolutionResult.Error!);
        }

        var readIndexModeResult = ReadIndexModeResolver.Resolve(input.ReadIndexMode, projectContext.Config);
        if (!readIndexModeResult.IsSuccess)
        {
            return ResolveServiceResultFactory.FromExecutionError(requestId, readIndexModeResult.Error!);
        }

        var executionMode = input.Mode ?? UnityExecutionMode.Auto;
        var timeout = timeoutResolutionResult.Timeout!.Value;
        var readIndexMode = readIndexModeResult.Mode!.Value;
        if (input.Selector is ResolveSceneHierarchySelectorInput sceneHierarchySelector && readIndexMode != ReadIndexMode.Disabled)
        {
            var indexResult = await TryResolveFromSceneTreeLiteIndex(
                    requestId,
                    input,
                    sceneHierarchySelector,
                    projectContext,
                    executionMode,
                    timeout,
                    readIndexMode,
                    cancellationToken)
                .ConfigureAwait(false);
            if (indexResult.CompletedResult != null)
            {
                return indexResult.CompletedResult;
            }

            return await ExecuteResolveInUnity(
                    requestId,
                    input,
                    projectContext,
                    executionMode,
                    timeout,
                    indexResult.FallbackReason,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var fallbackReason = ResolveFallbackReason(input.Selector, readIndexMode);
        return await ExecuteResolveInUnity(
                requestId,
                input,
                projectContext,
                executionMode,
                timeout,
                fallbackReason,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<(ResolveServiceResult? CompletedResult, string FallbackReason)> TryResolveFromSceneTreeLiteIndex (
        string requestId,
        ResolveCommandInput input,
        ResolveSceneHierarchySelectorInput selector,
        ProjectContext projectContext,
        UnityExecutionMode executionMode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        CancellationToken cancellationToken)
    {
        var readResult = await sceneTreeLiteAccessService.Read(
                projectContext.UnityProject,
                projectContext.Config,
                UcliCommandIds.Resolve,
                executionMode,
                timeout,
                readIndexMode,
                selector.Scene,
                depth: null,
                failFast: input.FailFast,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!readResult.IsSuccess)
        {
            if (string.Equals(readResult.ErrorCode, IpcErrorCodes.InvalidArgument, StringComparison.Ordinal))
            {
                return (
                    ResolveServiceResultFactory.FromIpcError(
                        requestId,
                        new IpcError(readResult.ErrorCode!, readResult.Message, null),
                        ResolveReadIndexInfoFactory.Unity(readResult.Message)),
                    readResult.Message);
            }

            return (null, readResult.Message);
        }

        var output = readResult.Output!;
        if (output.AccessInfo.Source != SceneTreeLiteSource.Index)
        {
            return (null, output.AccessInfo.FallbackReason ?? "scene-tree-lite readIndex was not used.");
        }

        var resolveResult = SceneTreeLiteHierarchyPathResolver.Resolve(output.Roots, selector.HierarchyPath);
        if (!resolveResult.IsSuccess)
        {
            return (null, resolveResult.ErrorMessage!);
        }

        return (
            ResolveServiceResultFactory.Success(
                requestId,
                [
                    CreateResolveOperationResult(resolveResult.GlobalObjectId!),
                ],
                ResolveReadIndexInfoFactory.FromSceneTreeLiteAccess(output.AccessInfo)),
            string.Empty);
    }

    private async ValueTask<ResolveServiceResult> ExecuteResolveInUnity (
        string requestId,
        ResolveCommandInput input,
        ProjectContext projectContext,
        UnityExecutionMode executionMode,
        TimeSpan timeout,
        string fallbackReason,
        CancellationToken cancellationToken)
    {
        var readIndex = ResolveReadIndexInfoFactory.Unity(fallbackReason);
        var executionResult = await unityRequestExecutor.Execute(
                UcliCommandIds.Resolve,
                executionMode,
                timeout,
                projectContext.Config,
                projectContext.UnityProject,
                IpcMethodNames.Execute,
                CreateExecuteRequestPayload(input.Selector, requestId, input.FailFast),
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            var errorCode = ResolveErrorCode(executionResult.ErrorCode);
            return ResolveServiceResultFactory.Create(
                requestId,
                [],
                [
                    new IpcError(errorCode, executionResult.Message, null),
                ],
                ExecuteResponseConverter.ResolveExitCode(errorCode),
                readIndex);
        }

        var convertedResponse = ExecuteResponseConverter.Convert(executionResult.Response!);
        return ResolveServiceResultFactory.Create(
            requestId,
            convertedResponse.OpResults,
            convertedResponse.Errors,
            convertedResponse.ExitCode,
            readIndex);
    }

    private static JsonElement CreateExecuteRequestPayload (
        ResolveSelectorInput selector,
        string requestId,
        bool failFast)
    {
        return ExecuteRequestPayloadFactory.CreateSingleOperation(
            UcliCommandIds.Resolve,
            requestId,
            ResolveOperationId,
            UcliPrimitiveOperationNames.Resolve,
            ResolveSelectorOperationArgsFactory.Create(selector),
            failFast);
    }

    private static IpcExecuteOperationResult CreateResolveOperationResult (string globalObjectId)
    {
        return IpcExecuteOperationResultFactory.CreatePlanResult(
            opId: ResolveOperationId,
            op: UcliPrimitiveOperationNames.Resolve,
            applied: false,
            changed: false,
            touched: [],
            result: IpcPayloadCodec.SerializeToElement(new IpcResolveOperationResult(globalObjectId)));
    }

    private static string ResolveFallbackReason (
        ResolveSelectorInput selector,
        ReadIndexMode readIndexMode)
    {
        if (selector is ResolveSceneHierarchySelectorInput && readIndexMode == ReadIndexMode.Disabled)
        {
            return "readIndex disabled by mode.";
        }

        return "selector requires live Unity resolution.";
    }

    private static string ResolveErrorCode (string? errorCode)
    {
        return string.IsNullOrWhiteSpace(errorCode)
            ? IpcErrorCodes.InternalError
            : errorCode;
    }

}
