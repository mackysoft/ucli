using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class RecordingSupervisorProcessLauncher : ISupervisorProcessLauncher
{
    private readonly List<Invocation> invocations = [];

    public Func<string, CancellationToken, ValueTask<ExecutionError?>>? LaunchHandler { get; set; }

    public ExecutionError? LaunchError { get; set; }

    public TaskCompletionSource? LaunchStarted { get; set; }

    public bool ObservedCancellation { get; private set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public async ValueTask<ExecutionError?> LaunchAsync (
        string storageRoot,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            invocations.Add(new Invocation(storageRoot, cancellationToken));
            LaunchStarted?.TrySetResult();

            if (LaunchHandler is not null)
            {
                return await LaunchHandler(storageRoot, cancellationToken).ConfigureAwait(false);
            }

            if (LaunchError is not null)
            {
                return LaunchError;
            }

            throw new InvalidOperationException("Supervisor launch should not be used by this test.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ObservedCancellation = true;
            throw;
        }
    }

    internal readonly record struct Invocation (
        string StorageRoot,
        CancellationToken CancellationToken);
}
