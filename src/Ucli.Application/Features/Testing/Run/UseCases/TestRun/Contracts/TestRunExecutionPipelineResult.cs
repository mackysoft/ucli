using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Results;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Contracts;

/// <summary> Represents one established Test Run execution-pipeline outcome. </summary>
internal abstract record TestRunExecutionPipelineResult
{
    private TestRunExecutionPipelineResult ()
    {
    }

    /// <summary> Creates a pipeline completion backed by normalized result evidence. </summary>
    public static TestRunExecutionPipelineCompleted Completed (
        ArtifactsSession session,
        UnityResultsConversionSuccess conversion)
    {
        return new TestRunExecutionPipelineCompleted(session, conversion);
    }

    /// <summary> Creates a command failure before an artifacts session existed. </summary>
    public static TestRunExecutionPipelineFailureBeforeArtifacts FailedBeforeArtifacts (
        ExecutionError error)
    {
        return new TestRunExecutionPipelineFailureBeforeArtifacts(error);
    }

    /// <summary> Creates a primary command failure after an artifacts session existed. </summary>
    public static TestRunExecutionPipelineFailureAfterArtifacts FailedAfterArtifacts (
        ArtifactsSession session,
        ApplicationFailure primaryFailure)
    {
        return new TestRunExecutionPipelineFailureAfterArtifacts(
            session,
            primaryFailure);
    }

    /// <summary>
    /// Creates a primary command failure followed by a distinct artifacts-finalization failure.
    /// </summary>
    public static TestRunExecutionPipelineFailureWithFinalizationFailure
        FailedAfterArtifactsWithFinalizationFailure (
            ArtifactsSession session,
            ApplicationFailure primaryFailure,
            ApplicationFailure finalizationFailure)
    {
        return new TestRunExecutionPipelineFailureWithFinalizationFailure(
            session,
            primaryFailure,
            finalizationFailure);
    }

    /// <summary> Represents a completed pipeline with normalized result evidence. </summary>
    internal sealed record TestRunExecutionPipelineCompleted : TestRunExecutionPipelineResult
    {
        internal TestRunExecutionPipelineCompleted (
            ArtifactsSession session,
            UnityResultsConversionSuccess conversion)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Conversion = conversion ?? throw new ArgumentNullException(nameof(conversion));
        }

        public ArtifactsSession Session { get; }

        public UnityResultsConversionSuccess Conversion { get; }
    }

    /// <summary> Represents a pipeline failure before an artifacts session existed. </summary>
    internal sealed record TestRunExecutionPipelineFailureBeforeArtifacts : TestRunExecutionPipelineResult
    {
        internal TestRunExecutionPipelineFailureBeforeArtifacts (ExecutionError error)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
        }

        public ExecutionError Error { get; }
    }

    /// <summary> Represents a primary pipeline failure after an artifacts session existed. </summary>
    internal record TestRunExecutionPipelineFailureAfterArtifacts : TestRunExecutionPipelineResult
    {
        internal TestRunExecutionPipelineFailureAfterArtifacts (
            ArtifactsSession session,
            ApplicationFailure primaryFailure)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            PrimaryFailure = primaryFailure ?? throw new ArgumentNullException(nameof(primaryFailure));
        }

        public ArtifactsSession Session { get; }

        public ApplicationFailure PrimaryFailure { get; }
    }

    /// <summary>
    /// Represents a primary pipeline failure plus a later artifacts-finalization failure.
    /// </summary>
    internal sealed record TestRunExecutionPipelineFailureWithFinalizationFailure
        : TestRunExecutionPipelineFailureAfterArtifacts
    {
        internal TestRunExecutionPipelineFailureWithFinalizationFailure (
            ArtifactsSession session,
            ApplicationFailure primaryFailure,
            ApplicationFailure finalizationFailure)
            : base(session, primaryFailure)
        {
            FinalizationFailure = finalizationFailure
                ?? throw new ArgumentNullException(nameof(finalizationFailure));
        }

        public ApplicationFailure FinalizationFailure { get; }
    }
}
