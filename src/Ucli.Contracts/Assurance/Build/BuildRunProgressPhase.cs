using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines <c>build.run</c> progress phase literals. </summary>
public enum BuildRunProgressPhase
{
    /// <summary> Emitted after build run identity and profile digest are established. </summary>
    [UcliContractLiteral("started")]
    Started = 1,

    /// <summary> Emitted after Unity readiness is confirmed. </summary>
    [UcliContractLiteral("readiness")]
    Readiness = 2,

    /// <summary> Emitted after the build runner is resolved. </summary>
    [UcliContractLiteral("runnerResolution")]
    RunnerResolution = 3,

    /// <summary> Emitted while the build runner is invoked. </summary>
    [UcliContractLiteral("runnerInvocation")]
    RunnerInvocation = 4,

    /// <summary> Emitted after the runner terminal result is observed or normalized. </summary>
    [UcliContractLiteral("runnerResult")]
    RunnerResult = 5,

    /// <summary> Emitted after build artifacts are accounted. </summary>
    [UcliContractLiteral("artifactAccounting")]
    ArtifactAccounting = 6,

    /// <summary> Emitted after the final build payload has been built. </summary>
    [UcliContractLiteral("completed")]
    Completed = 7,
}
