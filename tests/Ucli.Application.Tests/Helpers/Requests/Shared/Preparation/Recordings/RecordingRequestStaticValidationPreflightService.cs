using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingRequestStaticValidationPreflightService : IRequestStaticValidationPreflightService
{
    private readonly List<Invocation> invocations = [];

    public IReadOnlyList<Invocation> Invocations => invocations;

    public RequestStaticValidationPreflightResult? Result { get; set; }

    public Exception? Exception { get; set; }

    public Action<CancellationToken>? OnPrepare { get; set; }

    public ValueTask<RequestStaticValidationPreflightResult> PrepareAsync (
        PreparedRequestContext preparedRequest,
        ReadIndexMode? readIndexMode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(
            preparedRequest,
            readIndexMode,
            cancellationToken));
        OnPrepare?.Invoke(cancellationToken);

        if (Exception != null)
        {
            throw Exception;
        }

        if (Result is null)
        {
            throw new InvalidOperationException("Static validation preflight result is not configured.");
        }

        return ValueTask.FromResult(Result);
    }

    internal readonly record struct Invocation (
        PreparedRequestContext PreparedRequest,
        ReadIndexMode? ReadIndexMode,
        CancellationToken CancellationToken);
}
