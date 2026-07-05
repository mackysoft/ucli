using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Configuration;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingOperationAuthorizationService : IOperationAuthorizationService
{
    private readonly OperationAuthorizationResult result;
    private readonly List<Invocation> invocations = [];

    public RecordingOperationAuthorizationService (OperationAuthorizationResult result)
    {
        this.result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<OperationAuthorizationResult> AuthorizeAsync (
        UcliOperationDescriptor operation,
        UcliConfig config,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(
            operation,
            config,
            cancellationToken));

        return ValueTask.FromResult(result);
    }

    internal readonly record struct Invocation (
        UcliOperationDescriptor Operation,
        UcliConfig Config,
        CancellationToken CancellationToken);
}
