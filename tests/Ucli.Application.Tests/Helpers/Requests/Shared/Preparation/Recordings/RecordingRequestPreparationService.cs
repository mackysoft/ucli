using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingRequestPreparationService : IRequestPreparationService
{
    private readonly List<PrepareInvocation> prepareInvocations = [];

    public IReadOnlyList<PrepareInvocation> PrepareInvocations => prepareInvocations;

    public RequestPreparationResult? PrepareResult { get; set; }

    public Exception? PrepareException { get; set; }

    public Action<CancellationToken>? OnPrepare { get; set; }

    public ValueTask<RequestPreparationResult> PrepareAsync (
        string? projectPath,
        string requestJson,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        prepareInvocations.Add(new PrepareInvocation(
            projectPath,
            requestJson,
            cancellationToken));
        OnPrepare?.Invoke(cancellationToken);

        if (PrepareException != null)
        {
            throw PrepareException;
        }

        if (PrepareResult is null)
        {
            throw new InvalidOperationException("Request preparation result is not configured.");
        }

        return ValueTask.FromResult(PrepareResult);
    }

    internal readonly record struct PrepareInvocation (
        string? ProjectPath,
        string RequestJson,
        CancellationToken CancellationToken);
}
