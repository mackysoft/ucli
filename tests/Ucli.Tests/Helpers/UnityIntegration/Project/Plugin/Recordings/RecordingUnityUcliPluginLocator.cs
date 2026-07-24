using MackySoft.FileSystem;

namespace MackySoft.Ucli.Tests.Helpers.Unity;

internal sealed class RecordingUnityUcliPluginLocator : IUnityUcliPluginLocator
{
    private readonly List<Invocation> invocations = [];

    public Func<CancellationToken, ValueTask<UnityUcliPluginLocateResult>>? Handler { get; set; }

    public bool ObservedCancellation { get; private set; }

    public UnityUcliPluginLocateResult Result { get; set; }
        = UnityUcliPluginLocateResult.Found(
            AbsolutePath.Resolve(
                AbsolutePath.Parse(Environment.CurrentDirectory),
                "ucli-plugin.json"),
            UnityUcliPluginMarkerContract.ExpectedProtocolVersion);

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<UnityUcliPluginLocateResult> LocateAsync (
        AbsolutePath unityProjectRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(unityProjectRoot, cancellationToken));
        if (Handler == null)
        {
            return ValueTask.FromResult(Result);
        }

        return LocateCoreAsync(cancellationToken);
    }

    private async ValueTask<UnityUcliPluginLocateResult> LocateCoreAsync (CancellationToken cancellationToken)
    {
        try
        {
            return await Handler!(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ObservedCancellation = true;
            throw;
        }
    }

    internal readonly record struct Invocation (
        AbsolutePath UnityProjectRoot,
        CancellationToken CancellationToken);
}
