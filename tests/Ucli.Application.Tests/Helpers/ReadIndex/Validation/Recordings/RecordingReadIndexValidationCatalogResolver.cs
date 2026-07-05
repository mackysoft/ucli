using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingReadIndexValidationCatalogResolver : IReadIndexValidationCatalogResolver
{
    private readonly List<Invocation> invocations = [];

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ReadIndexValidationCatalogResolutionResult? Result { get; set; }

    public Exception? Exception { get; set; }

    public ValueTask<ReadIndexValidationCatalogResolutionResult> ResolveAsync (
        ResolvedUnityProjectContext unityProject,
        ReadIndexMode readIndexMode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(
            unityProject,
            readIndexMode,
            cancellationToken));

        if (Exception != null)
        {
            throw Exception;
        }

        if (Result is null)
        {
            throw new InvalidOperationException("Read-index validation catalog resolution result is not configured.");
        }

        return ValueTask.FromResult(Result);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        ReadIndexMode ReadIndexMode,
        CancellationToken CancellationToken);
}
