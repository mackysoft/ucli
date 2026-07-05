using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingOperationCatalog : IOperationCatalog
{
    private readonly List<LookupInvocation> lookupInvocations = [];

    private readonly List<GetAllInvocation> getAllInvocations = [];

    private readonly List<ProjectGetAllInvocation> projectGetAllInvocations = [];

    public IReadOnlyList<LookupInvocation> LookupInvocations => lookupInvocations;

    public IReadOnlyList<GetAllInvocation> GetAllInvocations => getAllInvocations;

    public IReadOnlyList<ProjectGetAllInvocation> ProjectGetAllInvocations => projectGetAllInvocations;

    public IReadOnlyList<UcliOperationDescriptor>? Operations { get; set; }

    public UcliOperationDescriptor? LookupResult { get; set; }

    public Exception? LookupException { get; set; }

    public Exception? GetAllException { get; set; }

    public Exception? ProjectGetAllException { get; set; }

    public ValueTask<UcliOperationDescriptor?> GetAsync (
        string name,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lookupInvocations.Add(new LookupInvocation(name, cancellationToken));

        if (LookupException != null)
        {
            throw LookupException;
        }

        return ValueTask.FromResult(LookupResult);
    }

    public ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetAllAsync (
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        getAllInvocations.Add(new GetAllInvocation(cancellationToken));

        if (GetAllException != null)
        {
            throw GetAllException;
        }

        return ValueTask.FromResult(GetConfiguredOperations());
    }

    public ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetAllAsync (
        ResolvedUnityProjectContext unityProject,
        UcliConfig config,
        UnityExecutionMode mode = UnityExecutionMode.Auto,
        TimeSpan? timeout = null,
        bool failFast = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        projectGetAllInvocations.Add(new ProjectGetAllInvocation(
            unityProject,
            config,
            mode,
            timeout,
            failFast,
            cancellationToken));

        if (ProjectGetAllException != null)
        {
            throw ProjectGetAllException;
        }

        return ValueTask.FromResult(GetConfiguredOperations());
    }

    private IReadOnlyList<UcliOperationDescriptor> GetConfiguredOperations ()
    {
        if (Operations is null)
        {
            throw new InvalidOperationException("Operation catalog descriptors are not configured.");
        }

        return Operations;
    }

    internal readonly record struct LookupInvocation (
        string Name,
        CancellationToken CancellationToken);

    internal readonly record struct GetAllInvocation (CancellationToken CancellationToken);

    internal readonly record struct ProjectGetAllInvocation (
        ResolvedUnityProjectContext UnityProject,
        UcliConfig Config,
        UnityExecutionMode Mode,
        TimeSpan? Timeout,
        bool FailFast,
        CancellationToken CancellationToken);
}
