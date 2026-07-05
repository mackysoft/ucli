using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingOperationExecuteService : IOperationExecuteService
{
    private readonly Queue<OperationExecuteResult> results;
    private readonly List<Invocation> invocations = [];

    public RecordingOperationExecuteService (params OperationExecuteResult[] results)
    {
        if (results.Length == 0)
        {
            throw new ArgumentException("At least one result is required.", nameof(results));
        }

        this.results = new Queue<OperationExecuteResult>(results);
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<OperationExecuteResult> ExecuteAsync (
        OperationExecuteDefinition definition,
        OperationExecuteInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(
            definition,
            input,
            cancellationToken));

        return ValueTask.FromResult(results.Count == 1 ? results.Peek() : results.Dequeue());
    }

    internal readonly record struct Invocation (
        OperationExecuteDefinition Definition,
        OperationExecuteInput Input,
        CancellationToken CancellationToken);
}
