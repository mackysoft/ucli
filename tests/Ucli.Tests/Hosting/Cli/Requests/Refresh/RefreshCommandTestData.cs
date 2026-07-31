using MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Tests;

internal static class RefreshCommandTestData
{
    public const string RequestId = "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62";
    public const string ExecutionId = "ab0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c63";

    public static readonly Guid RequestGuid = Guid.Parse(RequestId);
    public static readonly Guid ExecutionGuid = Guid.Parse(ExecutionId);
    public static readonly DateTimeOffset StartedAtUtc =
        new(2026, 7, 31, 1, 2, 3, TimeSpan.Zero);

    public static RefreshExecutionResult CreateSuccessResult (
        ExecutionReadPostcondition? readPostcondition = null)
    {
        var refresh = new RefreshLifecycleResult.RefreshEvidence(
            StartedAtUtc,
            StartedAtUtc.AddSeconds(2),
            domainReloadGenerationBefore: 1,
            domainReloadGenerationAfter: 2);
        return RefreshExecutionResult.Success(
            new RefreshExecutionOutput(
                ProjectIdentityInfoTestFactory.Create(),
                RequestGuid,
                CreateTerminalReference(completed: true),
                refresh,
                CreateObservation(domainReloadGeneration: 2),
                readPostcondition));
    }

    public static RefreshExecutionResult CreateFailureResult ()
    {
        return RefreshExecutionResult.Failure(
            ApplicationFailure.FromCode(
                LifecycleExecutionErrorCodes.DeadlineExceeded,
                "Refresh deadline exceeded."),
            new RefreshExecutionErrorOutput(
                ProjectIdentityInfoTestFactory.Create(),
                RequestGuid,
                CreateTerminalReference(completed: false),
                ExecutionApplicationState.Applied,
                Refresh: new RefreshLifecycleStartEvidence(StartedAtUtc, 1),
                ObservedLifecycle: CreateObservation(domainReloadGeneration: 1),
                ReadPostcondition: null));
    }

    public static RefreshExecutionResult CreatePublicationFailureResult ()
    {
        var lifecycle = CreateObservation(domainReloadGeneration: 2);
        return RefreshExecutionResult.Failure(
            ApplicationFailure.FromCode(
                LifecycleExecutionErrorCodes.TerminalPublicationFailed,
                "Refresh terminal record could not be published."),
            new RefreshExecutionErrorOutput(
                ProjectIdentityInfoTestFactory.Create(),
                RequestGuid,
                CreatePublishingReference(),
                ExecutionApplicationState.Applied,
                new RefreshLifecycleStartEvidence(StartedAtUtc, 1),
                lifecycle,
                ReadPostcondition: null));
    }

    private static UnityEditorObservation CreateObservation (
        long domainReloadGeneration)
    {
        return UnityEditorObservationTestFactory.Create(
            projectFingerprint: ProjectIdentityInfoTestFactory.ProjectFingerprint,
            generations: new UnityEditorGenerationSnapshot(0, domainReloadGeneration, 1, 0),
            observedAtUtc: StartedAtUtc.AddSeconds(2));
    }

    private static TerminalExecutionRef CreateTerminalReference (bool completed)
    {
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh);
        return new TerminalExecutionRef(
            definition.ExecutionKind,
            ExecutionGuid,
            LifecycleExecutionDefinitionDigest.Calculate(definition),
            new ExecutionState(TextVocabulary.GetText(
                completed
                    ? LifecycleExecutionState.Completed
                    : LifecycleExecutionState.Failed)),
            statusLocator: null,
            new PathArtifactRef(
                LifecycleExecutionArtifactContract.TerminalRecordKind,
                LifecycleExecutionArtifactContract.TerminalRecordMediaType,
                new ArtifactPath(
                    $".ucli/local/artifacts/lifecycle-execution/refresh/{ExecutionGuid:N}/terminal.json"),
                Sha256Digest.Parse(new string('a', 64)),
                sizeBytes: 123,
                StartedAtUtc.AddSeconds(2)));
    }

    private static RecoveryExecutionRef CreatePublishingReference ()
    {
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.Refresh);
        return new RecoveryExecutionRef(
            definition.ExecutionKind,
            ExecutionGuid,
            LifecycleExecutionDefinitionDigest.Calculate(definition),
            new ExecutionState(TextVocabulary.GetText(
                LifecycleExecutionState.Publishing)),
            new ExecutionStatusLocator(
                $".ucli/local/lifecycle-executions/{ExecutionGuid:N}/execution.json"));
    }
}
