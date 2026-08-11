using MackySoft.Ucli.Application.Features.Play.Common;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using ExecutionMode = MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision.UnityExecutionMode;

namespace MackySoft.Ucli.Hosting.Cli.Common.Execution;

/// <summary>
/// Composes the fixed project, provider binding, and deadlines required by CLI-owned Lifecycle
/// Execution starts before an action service receives its typed invocation.
/// </summary>
internal interface ILifecycleExecutionCliInvocationFactory
{
    ValueTask<LifecycleExecutionCliInvocationResolution> CreateRefreshStartAsync (
        string? projectPath,
        ExecutionMode requestedMode,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default);

    ValueTask<LifecycleExecutionCliInvocationResolution> CreateCompileStartAsync (
        string? projectPath,
        ExecutionMode requestedMode,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default);

    ValueTask<LifecycleExecutionCliInvocationResolution> CreatePlayEnterStartAsync (
        string? projectPath,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default);

    ValueTask<LifecycleExecutionCliInvocationResolution> CreatePlayExitStartAsync (
        string? projectPath,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default);
}

/// <summary> Resolves either a complete fixed invocation or the pre-start facts available to CLI result projection. </summary>
internal sealed record LifecycleExecutionCliInvocationResolution
{
    private LifecycleExecutionCliInvocationResolution (
        LifecycleExecutionStartInvocation? invocation,
        ProjectIdentityInfo? project,
        ApplicationFailure? failure)
    {
        if ((invocation is not null) == (failure is not null))
        {
            throw new ArgumentException("A CLI Lifecycle Execution preparation must either succeed or fail.");
        }

        Invocation = invocation;
        Project = project;
        Failure = failure;
    }

    public LifecycleExecutionStartInvocation? Invocation { get; }

    public ProjectIdentityInfo? Project { get; }

    public ApplicationFailure? Failure { get; }

    public bool IsSuccess => Invocation is not null;

    public static LifecycleExecutionCliInvocationResolution Success (
        LifecycleExecutionStartInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        return new LifecycleExecutionCliInvocationResolution(
            invocation,
            ProjectIdentityInfo.From(invocation.Context.Project.UnityProject),
            failure: null);
    }

    public static LifecycleExecutionCliInvocationResolution Failed (
        ApplicationFailure failure,
        ProjectIdentityInfo? project = null)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new LifecycleExecutionCliInvocationResolution(
            invocation: null,
            project,
            failure);
    }
}

/// <summary> Implements CLI Lifecycle Execution preparation using the same application context and host-binding policies. </summary>
internal sealed class LifecycleExecutionCliInvocationFactory : ILifecycleExecutionCliInvocationFactory
{
    private readonly IProjectContextResolver projectContextResolver;

    private readonly IPlayCommandExecutionContextResolver playContextResolver;

    private readonly IUnityExecutionModeDecisionService executionModeDecisionService;

    private readonly IUnityRequestExecutor unityRequestExecutor;

    private readonly TimeProvider timeProvider;

    public LifecycleExecutionCliInvocationFactory (
        IProjectContextResolver projectContextResolver,
        IPlayCommandExecutionContextResolver playContextResolver,
        IUnityExecutionModeDecisionService executionModeDecisionService,
        IUnityRequestExecutor unityRequestExecutor,
        TimeProvider timeProvider)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.playContextResolver = playContextResolver ?? throw new ArgumentNullException(nameof(playContextResolver));
        this.executionModeDecisionService = executionModeDecisionService ?? throw new ArgumentNullException(nameof(executionModeDecisionService));
        this.unityRequestExecutor = unityRequestExecutor ?? throw new ArgumentNullException(nameof(unityRequestExecutor));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public ValueTask<LifecycleExecutionCliInvocationResolution> CreateRefreshStartAsync (
        string? projectPath,
        ExecutionMode requestedMode,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default)
    {
        return CreateStandardStartAsync(
            projectPath,
            requestedMode,
            timeoutMilliseconds,
            UcliCommandIds.Refresh,
            decideMode: false,
            cancellationToken);
    }

    public ValueTask<LifecycleExecutionCliInvocationResolution> CreateCompileStartAsync (
        string? projectPath,
        UnityExecutionMode requestedMode,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default)
    {
        return CreateStandardStartAsync(
            projectPath,
            requestedMode,
            timeoutMilliseconds,
            UcliCommandIds.Compile,
            decideMode: true,
            cancellationToken);
    }

    public ValueTask<LifecycleExecutionCliInvocationResolution> CreatePlayEnterStartAsync (
        string? projectPath,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default)
    {
        return CreatePlayStartAsync(
            projectPath,
            timeoutMilliseconds,
            UcliCommandIds.PlayEnter,
            "Registered GUI daemon session is not available for Play Mode enter.",
            "Play Mode enter requires a registered GUI daemon session.",
            cancellationToken);
    }

    public ValueTask<LifecycleExecutionCliInvocationResolution> CreatePlayExitStartAsync (
        string? projectPath,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default)
    {
        return CreatePlayStartAsync(
            projectPath,
            timeoutMilliseconds,
            UcliCommandIds.PlayExit,
            "Registered GUI daemon session is not available for Play Mode exit.",
            "Play Mode exit requires a registered GUI daemon session.",
            cancellationToken);
    }

    private async ValueTask<LifecycleExecutionCliInvocationResolution> CreateStandardStartAsync (
        string? projectPath,
        UnityExecutionMode requestedMode,
        int? timeoutMilliseconds,
        UcliCommand command,
        bool decideMode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var contextResult = await projectContextResolver.ResolveAsync(projectPath, cancellationToken).ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return LifecycleExecutionCliInvocationResolution.Failed(
                ApplicationFailure.FromExecutionError(contextResult.Error!));
        }

        var context = contextResult.Context!;
        var project = ProjectIdentityInfo.From(context.UnityProject);
        var timeoutResult = IpcCommandTimeoutResolver.ResolveNormalized(
            timeoutMilliseconds,
            command,
            context.Config);
        if (!timeoutResult.IsSuccess)
        {
            return LifecycleExecutionCliInvocationResolution.Failed(
                ApplicationFailure.FromExecutionError(timeoutResult.Error!),
                project);
        }

        var executionDeadline = ExecutionDeadline.Start(timeoutResult.Timeout!.Value, timeProvider);
        var bindingMode = requestedMode;
        if (decideMode)
        {
            if (!executionDeadline.TryGetRemainingTimeout(out var modeDecisionTimeout))
            {
                return LifecycleExecutionCliInvocationResolution.Failed(
                    ApplicationFailure.Timeout(
                        "Compile execution deadline elapsed before Unity execution mode was decided.",
                        LifecycleExecutionErrorCodes.DeadlineExceeded),
                    project);
            }

            var modeDecisionResult = await executionModeDecisionService.DecideAsync(
                    requestedMode,
                    context.UnityProject,
                    modeDecisionTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!modeDecisionResult.IsSuccess)
            {
                var failure = modeDecisionResult.HasContractError
                    ? ApplicationFailure.EnvironmentError(
                        modeDecisionResult.ContractError!.Message,
                        modeDecisionResult.ContractError.Code)
                    : ApplicationFailure.FromExecutionError(modeDecisionResult.Error!);
                return LifecycleExecutionCliInvocationResolution.Failed(failure, project);
            }

            bindingMode = UnityExecutionTargetModeMapper.ToExplicitMode(
                modeDecisionResult.Decision!.Target);
        }

        var bindingResult = await unityRequestExecutor.BindAsync(
                bindingMode,
                context.UnityProject,
                executionDeadline,
                cancellationToken)
            .ConfigureAwait(false);
        if (!bindingResult.IsSuccess)
        {
            var failure = bindingResult.Failure is null
                ? ApplicationFailure.Timeout(
                    $"{command.Name} execution deadline elapsed before the Unity host binding was fixed.",
                    LifecycleExecutionErrorCodes.DeadlineExceeded)
                : ApplicationFailure.FromCode(
                    bindingResult.Failure.Code,
                    bindingResult.Failure.Message);
            return LifecycleExecutionCliInvocationResolution.Failed(failure, project);
        }

        return LifecycleExecutionCliInvocationResolution.Success(
            CreateInvocation(context, requestedMode, bindingResult.Binding!, executionDeadline));
    }

    private async ValueTask<LifecycleExecutionCliInvocationResolution> CreatePlayStartAsync (
        string? projectPath,
        int? timeoutMilliseconds,
        UcliCommand command,
        string sessionNotAvailableMessage,
        string requiresGuiEditorMessage,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var contextResult = await playContextResolver.ResolveAsync(
                projectPath,
                timeoutMilliseconds,
                command,
                sessionNotAvailableMessage,
                requiresGuiEditorMessage,
                cancellationToken)
            .ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return LifecycleExecutionCliInvocationResolution.Failed(
                ApplicationFailure.FromExecutionError(contextResult.Error!));
        }

        var context = contextResult.Context!;
        var executionDeadline = ExecutionDeadline.Start(context.Timeout, timeProvider);
        var bindingResult = await unityRequestExecutor.BindAsync(
                ExecutionMode.Daemon,
                context.ProjectContext.UnityProject,
                executionDeadline,
                cancellationToken)
            .ConfigureAwait(false);
        if (!bindingResult.IsSuccess)
        {
            var failure = bindingResult.Failure is null
                ? ApplicationFailure.Timeout(
                    "Play transition deadline elapsed before the Unity host binding was fixed.",
                    LifecycleExecutionErrorCodes.DeadlineExceeded)
                : ApplicationFailure.FromCode(
                    bindingResult.Failure.Code,
                    bindingResult.Failure.Message);
            return LifecycleExecutionCliInvocationResolution.Failed(failure, context.Project);
        }

        return LifecycleExecutionCliInvocationResolution.Success(
            CreateInvocation(
                context.ProjectContext,
                ExecutionMode.Daemon,
                bindingResult.Binding!,
                executionDeadline));
    }

    private static LifecycleExecutionStartInvocation CreateInvocation (
        ProjectContext context,
        ExecutionMode requestedMode,
        IUnityExecutionHostBinding hostBinding,
        ExecutionDeadline executionDeadline)
    {
        return new LifecycleExecutionStartInvocation(
            new LifecycleExecutionFixedContext(context, requestedMode, hostBinding),
            executionDeadline,
            executionDeadline.CreateCompletionDeadline(
                LifecycleExecutionTiming.ResponseDeliveryGrace),
            NullLifecycleExecutionStartObserver.Instance);
    }
}
