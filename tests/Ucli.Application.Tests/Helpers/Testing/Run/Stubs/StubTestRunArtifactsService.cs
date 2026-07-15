using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StubTestRunArtifactsService : ITestRunArtifactsService
{
    private readonly Func<ResolvedTestRunConfiguration, ArtifactsPreparationResult> prepare;

    private readonly Func<ResolvedTestRunConfiguration, ArtifactsSession, UnityExecutionTarget, ArtifactsCompletionResult> complete;

    private ArtifactsSession? preparedSession;

    public StubTestRunArtifactsService (
        Func<ResolvedTestRunConfiguration, ArtifactsPreparationResult> prepare,
        Func<ResolvedTestRunConfiguration, ArtifactsSession, UnityExecutionTarget, ArtifactsCompletionResult> complete)
    {
        this.prepare = prepare;
        this.complete = complete;
    }

    public ValueTask<ArtifactsPreparationResult> PrepareAsync (
        ResolvedTestRunConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = prepare(configuration);
        preparedSession = result.Session;
        return ValueTask.FromResult(result);
    }

    public ArtifactPaths GetPreparedPaths (Guid runId)
    {
        if (preparedSession is null || preparedSession.RunId != runId)
        {
            throw new InvalidOperationException("No matching test-run artifact session was prepared.");
        }

        return preparedSession.Paths;
    }

    public ValueTask<ArtifactsCompletionResult> CompleteAsync (
        ResolvedTestRunConfiguration configuration,
        ArtifactsSession session,
        UnityExecutionTarget target,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(complete(configuration, session, target));
    }
}
