namespace MackySoft.Ucli.Tests.Helpers.Process;

internal sealed class RecordingProcessRunner : IProcessRunner
{
    private readonly List<Invocation> invocations = [];
    private readonly Queue<ProcessRunResult> results = [];

    public RecordingProcessRunner (params ProcessRunResult[] results)
    {
        foreach (var result in results)
        {
            this.results.Enqueue(result);
        }
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public Task<ProcessRunResult> RunAsync (
        ProcessRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        invocations.Add(new Invocation(request, cancellationToken));
        if (!results.TryDequeue(out var result))
        {
            throw new InvalidOperationException("Process run result is not configured.");
        }

        return Task.FromResult(result);
    }

    internal readonly record struct Invocation (
        ProcessRunRequest Request,
        CancellationToken CancellationToken);
}
