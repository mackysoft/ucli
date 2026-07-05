using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingOperationCatalogDiscoveryService : IOperationCatalogDiscoveryService
{
    private readonly IReadOnlyList<UcliOperationDescriptor> operations;

    private readonly List<Invocation> invocations = [];

    public RecordingOperationCatalogDiscoveryService (IReadOnlyList<UcliOperationDescriptor> operations)
    {
        this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<IReadOnlyList<UcliOperationDescriptor>> DiscoverAsync (
        ResolvedUnityProjectContext unityProject,
        UcliConfig config,
        UnityExecutionMode mode = UnityExecutionMode.Auto,
        TimeSpan? timeout = null,
        bool failFast = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(
            unityProject,
            config,
            mode,
            timeout,
            failFast,
            cancellationToken));
        return ValueTask.FromResult(operations);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        UcliConfig Config,
        UnityExecutionMode Mode,
        TimeSpan? Timeout,
        bool FailFast,
        CancellationToken CancellationToken);
}
