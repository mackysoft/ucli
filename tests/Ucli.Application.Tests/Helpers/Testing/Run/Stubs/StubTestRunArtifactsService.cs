using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StubTestRunArtifactsService : ITestRunArtifactsService
{
    private readonly Func<ResolvedTestRunConfiguration, ArtifactsPreparationResult> prepare;

    private readonly Func<ResolvedTestRunConfiguration, ArtifactsSession, ArtifactsCompletionResult> complete;

    public StubTestRunArtifactsService (
        Func<ResolvedTestRunConfiguration, ArtifactsPreparationResult> prepare,
        Func<ResolvedTestRunConfiguration, ArtifactsSession, ArtifactsCompletionResult> complete)
    {
        this.prepare = prepare;
        this.complete = complete;
    }

    public ValueTask<ArtifactsPreparationResult> PrepareAsync (
        ResolvedTestRunConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(prepare(configuration));
    }

    public ValueTask<ArtifactsCompletionResult> CompleteAsync (
        ResolvedTestRunConfiguration configuration,
        ArtifactsSession session,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(complete(configuration, session));
    }
}
