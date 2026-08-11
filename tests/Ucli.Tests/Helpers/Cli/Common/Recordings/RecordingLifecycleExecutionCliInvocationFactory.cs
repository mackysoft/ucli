using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Tests;

internal sealed class RecordingLifecycleExecutionCliInvocationFactory : ILifecycleExecutionCliInvocationFactory
{
    private readonly LifecycleExecutionCliInvocationResolution resolution;

    public RecordingLifecycleExecutionCliInvocationFactory ()
    {
        var project = ResolvedUnityProjectContext.Create(
            AbsolutePath.Parse("/repo/UnityProject"),
            AbsolutePath.Parse("/repo"),
            new ProjectFingerprint(new string('a', 64)),
            UnityProjectPathSource.CommandOption,
            "/repo/UnityProject",
            "6000.1.4f1");
        var context = new ProjectContext(project, UcliConfig.CreateDefault(), ConfigSource.Default);
        var hostBinding = new RecordingHostBinding(project);
        var executionDeadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(10), TimeProvider.System);
        resolution = LifecycleExecutionCliInvocationResolution.Success(
            new LifecycleExecutionStartInvocation(
                new LifecycleExecutionFixedContext(context, UnityExecutionMode.Auto, hostBinding),
                executionDeadline,
                executionDeadline.CreateCompletionDeadline(LifecycleExecutionTiming.ResponseDeliveryGrace),
                NullLifecycleExecutionStartObserver.Instance));
    }

    public List<RefreshStartRequest> RefreshRequests { get; } = [];

    public List<CompileStartRequest> CompileRequests { get; } = [];

    public List<PlayStartRequest> PlayEnterRequests { get; } = [];

    public List<PlayStartRequest> PlayExitRequests { get; } = [];

    public ValueTask<LifecycleExecutionCliInvocationResolution> CreateRefreshStartAsync (
        string? projectPath,
        UnityExecutionMode requestedMode,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default)
    {
        RefreshRequests.Add(new RefreshStartRequest(projectPath, requestedMode, timeoutMilliseconds, cancellationToken));
        return ValueTask.FromResult(resolution);
    }

    public ValueTask<LifecycleExecutionCliInvocationResolution> CreateCompileStartAsync (
        string? projectPath,
        UnityExecutionMode requestedMode,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default)
    {
        CompileRequests.Add(new CompileStartRequest(projectPath, requestedMode, timeoutMilliseconds, cancellationToken));
        return ValueTask.FromResult(resolution);
    }

    public ValueTask<LifecycleExecutionCliInvocationResolution> CreatePlayEnterStartAsync (
        string? projectPath,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default)
    {
        PlayEnterRequests.Add(new PlayStartRequest(projectPath, timeoutMilliseconds, cancellationToken));
        return ValueTask.FromResult(resolution);
    }

    public ValueTask<LifecycleExecutionCliInvocationResolution> CreatePlayExitStartAsync (
        string? projectPath,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default)
    {
        PlayExitRequests.Add(new PlayStartRequest(projectPath, timeoutMilliseconds, cancellationToken));
        return ValueTask.FromResult(resolution);
    }

    internal sealed record RefreshStartRequest (
        string? ProjectPath,
        UnityExecutionMode RequestedMode,
        int? TimeoutMilliseconds,
        CancellationToken CancellationToken);

    internal sealed record CompileStartRequest (
        string? ProjectPath,
        UnityExecutionMode RequestedMode,
        int? TimeoutMilliseconds,
        CancellationToken CancellationToken);

    internal sealed record PlayStartRequest (
        string? ProjectPath,
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
    }
}
