using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Configuration;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingRequestStaticValidator : IRequestStaticValidator
{
    private readonly List<Invocation> invocations = [];

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValidationResult? Result { get; set; }

    public Exception? Exception { get; set; }

    public Action<CancellationToken>? OnValidate { get; set; }

    public ValueTask<ValidationResult> ValidateAsync (
        ValidateRequest request,
        RequestStaticValidationCatalog catalog,
        UcliConfig config,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(
            request,
            catalog,
            config,
            cancellationToken));
        OnValidate?.Invoke(cancellationToken);

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
        RequestStaticValidationCatalog Catalog,
        UcliConfig Config,
        CancellationToken CancellationToken);
}
