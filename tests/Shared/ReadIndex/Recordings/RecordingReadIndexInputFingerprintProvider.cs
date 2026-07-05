namespace MackySoft.Ucli.TestSupport;

internal sealed class RecordingReadIndexInputFingerprintProvider : IReadIndexInputFingerprintProvider
{
    private readonly Queue<ReadIndexCoreInputHashSnapshot?> coreSnapshots = new();
    private readonly Queue<ReadIndexInputHashSnapshot?> snapshots = new();

    private readonly List<Invocation> invocations = [];
    private readonly List<CoreInvocation> coreInvocations = [];
    private readonly List<FullInvocation> fullInvocations = [];

    private RecordingReadIndexInputFingerprintProvider (bool requireConfiguredSnapshot)
    {
        RequireConfiguredSnapshot = requireConfiguredSnapshot;
    }

    public RecordingReadIndexInputFingerprintProvider ()
    {
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public IReadOnlyList<CoreInvocation> CoreInvocations => coreInvocations;

    public IReadOnlyList<FullInvocation> FullInvocations => fullInvocations;

    public ReadIndexCoreInputHashSnapshot? CoreSnapshot { get; set; }

    public ReadIndexInputHashSnapshot? Snapshot { get; set; }

    public bool RequireConfiguredCoreSnapshot { get; set; }

    public bool RequireConfiguredSnapshot { get; set; }

    public bool ThrowOnTryComputeCore { get; set; }

    public bool ThrowOnTryCompute { get; set; }

    public static RecordingReadIndexInputFingerprintProvider ForFullSnapshotsOnly ()
    {
        return new RecordingReadIndexInputFingerprintProvider(requireConfiguredSnapshot: true)
        {
            ThrowOnTryComputeCore = true,
        };
    }

    public void EnqueueCore (ReadIndexCoreInputHashSnapshot? snapshot)
    {
        coreSnapshots.Enqueue(snapshot);
    }

    public void Enqueue (ReadIndexInputHashSnapshot? snapshot)
    {
        snapshots.Enqueue(snapshot);
    }

    public ValueTask<ReadIndexCoreInputHashSnapshot?> TryComputeCoreAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        cancellationToken.ThrowIfCancellationRequested();

        var coreInvocation = new CoreInvocation(unityProject, cancellationToken);
        coreInvocations.Add(coreInvocation);
        invocations.Add(new Invocation(
            InvocationKind.Core,
            unityProject,
            cancellationToken));

        if (ThrowOnTryComputeCore)
        {
            throw new InvalidOperationException("Core snapshot should not be computed by this test.");
        }

        if (coreSnapshots.TryDequeue(out var snapshot))
        {
            return ValueTask.FromResult(snapshot);
        }

        if (RequireConfiguredCoreSnapshot)
        {
            throw new InvalidOperationException("Core input fingerprint snapshot is not configured.");
        }

        return ValueTask.FromResult(CoreSnapshot);
    }

    public ValueTask<ReadIndexInputHashSnapshot?> TryComputeAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        cancellationToken.ThrowIfCancellationRequested();

        var fullInvocation = new FullInvocation(unityProject, cancellationToken);
        fullInvocations.Add(fullInvocation);
        invocations.Add(new Invocation(
            InvocationKind.Full,
            unityProject,
            cancellationToken));

        if (ThrowOnTryCompute)
        {
            throw new InvalidOperationException("Full snapshot should not be computed by this test.");
        }

        if (snapshots.TryDequeue(out var snapshot))
        {
            return ValueTask.FromResult(snapshot);
        }

        if (RequireConfiguredSnapshot)
        {
            throw new InvalidOperationException("Input fingerprint snapshot is not configured.");
        }

        return ValueTask.FromResult(Snapshot);
    }

    internal enum InvocationKind
    {
        Core,
        Full,
    }

    internal readonly record struct Invocation (
        InvocationKind Kind,
        ResolvedUnityProjectContext UnityProject,
        CancellationToken CancellationToken);

    internal readonly record struct CoreInvocation (
        ResolvedUnityProjectContext UnityProject,
        CancellationToken CancellationToken);

    internal readonly record struct FullInvocation (
        ResolvedUnityProjectContext UnityProject,
        CancellationToken CancellationToken);
}
