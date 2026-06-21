using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines <c>build.run</c> progress phase literals. </summary>
public enum BuildRunProgressPhase
{
    /// <summary> Emitted after build run identity and profile digest are established. </summary>
    [UcliContractLiteral("started")]
    Started = 0,

    /// <summary> Emitted after Unity readiness is confirmed. </summary>
    [UcliContractLiteral("readiness")]
    Readiness = 1,

    /// <summary> Emitted after the build runner is resolved. </summary>
    [UcliContractLiteral("runnerResolution")]
    RunnerResolution = 2,

    /// <summary> Emitted while the build runner is invoked. </summary>
    [UcliContractLiteral("runnerInvocation")]
    RunnerInvocation = 3,

    /// <summary> Emitted after the runner terminal result is observed or normalized. </summary>
    [UcliContractLiteral("runnerResult")]
    RunnerResult = 4,

    /// <summary> Emitted after build artifacts are accounted. </summary>
    [UcliContractLiteral("artifactAccounting")]
    ArtifactAccounting = 5,

    /// <summary> Emitted after the final build payload has been built. </summary>
    [UcliContractLiteral("completed")]
    Completed = 6,
}
