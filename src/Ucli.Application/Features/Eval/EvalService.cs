using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Application.Features.Eval;

/// <summary> Executes the dedicated two-phase C# evaluation protocol. </summary>
internal interface IEvalService
{
    ValueTask<EvalServiceResult> ExecuteAsync (Guid requestId, EvalCommandInput input, CancellationToken cancellationToken = default);
}

/// <summary> Owns the project admission and plan/call transition for one explicit eval command. </summary>
internal sealed class EvalService : IEvalService
{
    private readonly IProjectContextResolver projectContextResolver;
    private readonly IUnityEvalClient unityEvalClient;

    public EvalService (IProjectContextResolver projectContextResolver, IUnityEvalClient unityEvalClient)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.unityEvalClient = unityEvalClient ?? throw new ArgumentNullException(nameof(unityEvalClient));
    }

    public async ValueTask<EvalServiceResult> ExecuteAsync (Guid requestId, EvalCommandInput input, CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Request ID must not be empty.", nameof(requestId));
        }

        ArgumentNullException.ThrowIfNull(input);
        var contextResult = await projectContextResolver.ResolveAsync(input.ProjectPath, cancellationToken).ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return EvalServiceResult.Failure(contextResult.Error!);
        }

        var context = contextResult.Context!;
        var project = new UnityProjectIdentity(
            context.UnityProject.UnityProjectRoot.Value,
            context.UnityProject.ProjectFingerprint,
            context.UnityProject.UnityVersion);
        if (!context.Config.EvalEnabled)
        {
            return EvalServiceResult.Failure(requestId, project, ExecutionError.InvalidArgument("C# eval is disabled by config evalEnabled=false."));
        }

        if (!input.AllowDangerous)
        {
            return EvalServiceResult.Failure(requestId, project, ExecutionError.InvalidArgument("C# eval requires --allowDangerous."));
        }

        var timeoutResult = IpcCommandTimeoutResolver.ResolveNormalized(input.TimeoutMilliseconds, UcliCommandIds.Eval, context.Config);
        if (!timeoutResult.IsSuccess)
        {
            return EvalServiceResult.Failure(requestId, project, timeoutResult.Error!);
        }

        var request = new IpcEvalPlanRequest(input.Source, input.SourceKind, input.AllowDangerous, input.AllowPlayMode);
        var result = await unityEvalClient.ExecuteAsync(
                input.Mode ?? UnityExecutionMode.Auto,
                timeoutResult.Timeout!.Value,
                context.UnityProject,
                request,
                input.FailFast,
                cancellationToken)
            .ConfigureAwait(false);
        return EvalServiceResult.FromUnityResult(requestId, project, result);
    }
}

internal sealed record EvalCommandInput (AbsolutePath? ProjectPath, UnityExecutionMode? Mode, int? TimeoutMilliseconds, bool AllowDangerous, bool AllowPlayMode, bool FailFast, string Source, CsEvalSourceKind SourceKind);

internal sealed record EvalServiceResult (
    Guid? RequestId,
    UnityProjectIdentity? Project,
    IpcEvalResponse? Plan,
    IpcEvalResponse? Call,
    IpcEvalErrorResponse? ErrorResponse,
    ExecutionError? Error,
    bool CallWasSent)
{
    public bool IsSuccess => Call is not null && Error is null;

    public static EvalServiceResult Failure (ExecutionError error) =>
        new(null, null, null, null, null, error ?? throw new ArgumentNullException(nameof(error)), false);

    public static EvalServiceResult Failure (Guid requestId, UnityProjectIdentity project, ExecutionError error)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Request ID must not be empty.", nameof(requestId));
        }

        return new EvalServiceResult(
            requestId,
            project ?? throw new ArgumentNullException(nameof(project)),
            null,
            null,
            null,
            error ?? throw new ArgumentNullException(nameof(error)),
            false);
    }

    public static EvalServiceResult FromUnityResult (
        Guid requestId,
        UnityProjectIdentity resolvedProject,
        UnityEvalExecutionResult result)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Request ID must not be empty.", nameof(requestId));
        }

        ArgumentNullException.ThrowIfNull(resolvedProject);
        ArgumentNullException.ThrowIfNull(result);
        return new EvalServiceResult(
            requestId,
            result.ErrorResponse?.Project ?? result.Call?.Project ?? result.Plan?.Project ?? resolvedProject,
            result.Plan,
            result.Call,
            result.ErrorResponse,
            result.Error,
            result.CallWasSent);
    }
}
