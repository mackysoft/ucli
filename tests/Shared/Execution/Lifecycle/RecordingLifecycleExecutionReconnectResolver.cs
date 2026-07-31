using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.TestSupport;

internal sealed class RecordingLifecycleExecutionReconnectResolver :
    ILifecycleExecutionReconnectResolver
{
    private readonly IReadOnlyList<LifecycleExecutionReconnectResolution>
        resolutions;
    private readonly List<Invocation> invocations = [];

    public RecordingLifecycleExecutionReconnectResolver (
        params LifecycleExecutionReconnectResolution[] resolutions)
    {
        ArgumentNullException.ThrowIfNull(resolutions);
        if (resolutions.Length == 0
            || resolutions.Any(static resolution => resolution is null))
        {
            throw new ArgumentException(
                "At least one non-null reconnection resolution is required.",
                nameof(resolutions));
        }

        this.resolutions = Array.AsReadOnly(resolutions.ToArray());
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public Action<Invocation>? OnResolve { get; init; }

    public ValueTask<LifecycleExecutionReconnectResolution> ResolveAsync (
        ResolvedUnityProjectContext project,
        LifecycleExecutionDefinition expectedDefinition,
        ExecutionRef executionRef,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var invocation = new Invocation(
            project,
            expectedDefinition,
            executionRef,
            cancellationToken);
        invocations.Add(invocation);
        OnResolve?.Invoke(invocation);
        if (invocations.Count > resolutions.Count)
        {
            throw new InvalidOperationException(
                "No configured Lifecycle Execution reconnection resolution remains.");
        }

        return ValueTask.FromResult(resolutions[invocations.Count - 1]);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext Project,
        LifecycleExecutionDefinition ExpectedDefinition,
        ExecutionRef ExecutionRef,
        CancellationToken CancellationToken);
}
