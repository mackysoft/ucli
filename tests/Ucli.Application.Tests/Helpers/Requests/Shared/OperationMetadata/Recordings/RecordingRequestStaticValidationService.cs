using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Context;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingRequestStaticValidationService : IRequestStaticValidationService
{
    private readonly List<Invocation> invocations = [];

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValidationResult? Result { get; set; }

    public Exception? Exception { get; set; }

    public ValueTask<ValidationResult> ValidateAsync (
        ValidateRequest request,
        ProjectContext projectContext,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(
            request,
            projectContext,
            cancellationToken));

        if (Exception != null)
        {
            throw Exception;
        }

        if (Result is null)
        {
            throw new InvalidOperationException("Static validation result is not configured.");
        }

        return ValueTask.FromResult(Result);
    }

    internal readonly record struct Invocation (
        ValidateRequest Request,
        ProjectContext ProjectContext,
        CancellationToken CancellationToken);
}
