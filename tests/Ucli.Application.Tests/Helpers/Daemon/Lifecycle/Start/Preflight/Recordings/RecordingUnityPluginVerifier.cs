namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingUnityPluginVerifier : IUnityPluginVerifier
{
    private readonly List<Invocation> invocations = [];

    public IReadOnlyList<Invocation> Invocations => invocations;

    public Func<string, CancellationToken, ValueTask<UnityPluginVerificationResult>>? Handler { get; set; }

    public bool ObservedCancellation { get; private set; }

    public UnityPluginVerificationResult Result { get; set; } = UnityPluginVerificationResult.Success();

    public TaskCompletionSource? Started { get; set; }

    public ValueTask<UnityPluginVerificationResult> VerifyAsync (
        string unityProjectRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(
            unityProjectRoot,
            cancellationToken));

        if (Handler == null)
        {
            return ValueTask.FromResult(Result);
        }

        return VerifyCoreAsync(unityProjectRoot, cancellationToken);
    }

    private async ValueTask<UnityPluginVerificationResult> VerifyCoreAsync (
        string unityProjectRoot,
        CancellationToken cancellationToken)
    {
        try
        {
            Started?.TrySetResult();
            return await Handler!(unityProjectRoot, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ObservedCancellation = true;
            throw;
        }
    }

    internal readonly record struct Invocation (
        string UnityProjectRoot,
        CancellationToken CancellationToken);
}
