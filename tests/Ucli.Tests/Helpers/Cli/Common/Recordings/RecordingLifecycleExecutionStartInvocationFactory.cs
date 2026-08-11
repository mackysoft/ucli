using MackySoft.Ucli.Application.Features.Assurance.Compile.Execution;
using MackySoft.Ucli.Application.Features.Play.Common;
using MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Tests;

internal sealed class RecordingLifecycleExecutionStartInvocationFactory :
    ICompileLifecycleExecutionStartInvocationFactory,
    IRefreshLifecycleExecutionStartInvocationFactory,
    IPlayLifecycleExecutionStartInvocationFactory
{
    private readonly LifecycleExecutionStartInvocationPreparation resolution;

    private readonly RecordingHostBinding hostBinding;

    public RecordingLifecycleExecutionStartInvocationFactory ()
    {
        var project = ResolvedUnityProjectContext.Create(
            AbsolutePath.Parse(ProjectPathTestValues.RepositoryUnityProject),
            AbsolutePath.Parse(ProjectPathTestValues.RepositoryRoot),
            new ProjectFingerprint(new string('a', 64)),
            UnityProjectPathSource.CommandOption,
            ProjectPathTestValues.RepositoryUnityProject,
            "6000.1.4f1");
        var context = new ProjectContext(project, UcliConfig.CreateDefault(), ConfigSource.Default);
        hostBinding = new RecordingHostBinding(project);
        var executionDeadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(10), TimeProvider.System);
        resolution = LifecycleExecutionStartInvocationPreparation.Success(
            new LifecycleExecutionStartInvocation(
                new LifecycleExecutionFixedContext(context, UnityExecutionMode.Auto, hostBinding),
                executionDeadline,
                executionDeadline,
                NullLifecycleExecutionStartObserver.Instance));
    }

    public List<RefreshStartRequest> RefreshRequests { get; } = [];

    public List<CompileStartRequest> CompileRequests { get; } = [];

    public List<PlayStartRequest> PlayEnterRequests { get; } = [];

    public List<PlayStartRequest> PlayExitRequests { get; } = [];

    public int DisposeCount => hostBinding.DisposeCount;

    ValueTask<LifecycleExecutionStartInvocationPreparation>
        IRefreshLifecycleExecutionStartInvocationFactory.CreateAsync (
        AbsolutePath? projectPath,
        UnityExecutionMode requestedMode,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        RefreshRequests.Add(new RefreshStartRequest(projectPath, requestedMode, timeoutMilliseconds, cancellationToken));
        return ValueTask.FromResult(resolution);
    }

    ValueTask<LifecycleExecutionStartInvocationPreparation>
        ICompileLifecycleExecutionStartInvocationFactory.CreateAsync (
        AbsolutePath? projectPath,
        UnityExecutionMode requestedMode,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        CompileRequests.Add(new CompileStartRequest(projectPath, requestedMode, timeoutMilliseconds, cancellationToken));
        return ValueTask.FromResult(resolution);
    }

    ValueTask<LifecycleExecutionStartInvocationPreparation>
        IPlayLifecycleExecutionStartInvocationFactory.CreateEnterAsync (
        AbsolutePath? projectPath,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        PlayEnterRequests.Add(new PlayStartRequest(projectPath, UnityExecutionMode.Daemon, timeoutMilliseconds, cancellationToken));
        return ValueTask.FromResult(resolution);
    }

    ValueTask<LifecycleExecutionStartInvocationPreparation>
        IPlayLifecycleExecutionStartInvocationFactory.CreateExitAsync (
        AbsolutePath? projectPath,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        PlayExitRequests.Add(new PlayStartRequest(projectPath, UnityExecutionMode.Daemon, timeoutMilliseconds, cancellationToken));
        return ValueTask.FromResult(resolution);
    }

    internal sealed record RefreshStartRequest (
        AbsolutePath? ProjectPath,
        UnityExecutionMode RequestedMode,
        int? TimeoutMilliseconds,
        CancellationToken CancellationToken);

    internal sealed record CompileStartRequest (
        AbsolutePath? ProjectPath,
        UnityExecutionMode RequestedMode,
        int? TimeoutMilliseconds,
        CancellationToken CancellationToken);

    internal sealed record PlayStartRequest (
        AbsolutePath? ProjectPath,
        UnityExecutionMode RequestedMode,
        int? TimeoutMilliseconds,
        CancellationToken CancellationToken);

    private sealed class RecordingHostBinding : IUnityExecutionHostBinding
    {
        public RecordingHostBinding (ResolvedUnityProjectContext project)
        {
            Project = project;
        }

        public ResolvedUnityProjectContext Project { get; }

        public UnityExecutionTarget Target => UnityExecutionTarget.Daemon;

        public int DisposeCount { get; private set; }

        public ValueTask<UnityRequestExecutionResult> StartAsync (
            UcliCommand command,
            UnityRequestPayload payload,
            LifecycleExecutionStartInvocation invocation,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Test command services do not dispatch through the host binding.");
        }

        public ValueTask<UnityRequestExecutionResult> ReconnectAsync (
            UcliCommand command,
            UnityRequestPayload payload,
            LifecycleExecutionReconnectInvocation invocation,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Test command services do not dispatch through the host binding.");
        }

        public ValueTask DisposeAsync ()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
