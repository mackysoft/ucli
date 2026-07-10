namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingUnityEditorInstanceMarkerReader : IUnityEditorInstanceMarkerReader
{
    private readonly List<Invocation> invocations = [];

    public UnityEditorInstanceMarkerReadResult ReadResult { get; set; } =
        UnityEditorInstanceMarkerReadResult.Success(null);

    public Action? OnRead { get; set; }

    public Func<ResolvedUnityProjectContext, CancellationToken, ValueTask<UnityEditorInstanceMarkerReadResult>>? ReadAsyncHandler { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<UnityEditorInstanceMarkerReadResult> ReadAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OnRead?.Invoke();
        invocations.Add(new Invocation(unityProject, cancellationToken));
        if (ReadAsyncHandler is not null)
        {
            return ReadAsyncHandler(unityProject, cancellationToken);
        }

        return ValueTask.FromResult(ReadResult);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        CancellationToken CancellationToken);
}
