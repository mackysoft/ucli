
namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines <c>build.run</c> progress phase literals. </summary>
[VocabularyDefinition]
public enum BuildRunProgressPhase
{
    /// <summary> Emitted after build run identity and profile digest are established. </summary>
    [VocabularyText("started")]
    Started = 1,

    /// <summary> Emitted after Unity readiness is confirmed. </summary>
    [VocabularyText("readiness")]
    Readiness = 2,

    /// <summary> Emitted after the build runner is resolved. </summary>
    [VocabularyText("runnerResolution")]
    RunnerResolution = 3,

    /// <summary> Emitted while the build runner is invoked. </summary>
    [VocabularyText("runnerInvocation")]
    RunnerInvocation = 4,

    /// <summary> Emitted after the runner terminal result is observed or normalized. </summary>
    [VocabularyText("runnerResult")]
    RunnerResult = 5,

    /// <summary> Emitted after build artifacts are accounted. </summary>
    [VocabularyText("artifactAccounting")]
    ArtifactAccounting = 6,

    /// <summary> Emitted after the final build payload has been built. </summary>
    [VocabularyText("completed")]
    Completed = 7,
}
