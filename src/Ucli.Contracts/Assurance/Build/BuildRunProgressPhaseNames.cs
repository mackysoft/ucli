using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines the closed <c>build.run</c> progress phase set. </summary>
public static class BuildRunProgressPhaseNames
{
    /// <summary> Gets the phase emitted after build run identity and profile digest are established. </summary>
    public static string Started => ContractLiteralCodec.ToValue(BuildRunProgressPhase.Started);

    /// <summary> Gets the phase emitted after Unity readiness is confirmed. </summary>
    public static string Readiness => ContractLiteralCodec.ToValue(BuildRunProgressPhase.Readiness);

    /// <summary> Gets the phase emitted after the build runner is resolved. </summary>
    public static string RunnerResolution => ContractLiteralCodec.ToValue(BuildRunProgressPhase.RunnerResolution);

    /// <summary> Gets the phase emitted while the build runner is invoked. </summary>
    public static string RunnerInvocation => ContractLiteralCodec.ToValue(BuildRunProgressPhase.RunnerInvocation);

    /// <summary> Gets the phase emitted after the runner terminal result is observed or normalized. </summary>
    public static string RunnerResult => ContractLiteralCodec.ToValue(BuildRunProgressPhase.RunnerResult);

    /// <summary> Gets the phase emitted after build artifacts are accounted. </summary>
    public static string ArtifactAccounting => ContractLiteralCodec.ToValue(BuildRunProgressPhase.ArtifactAccounting);

    /// <summary> Gets the phase emitted after the final build payload has been built. </summary>
    public static string Completed => ContractLiteralCodec.ToValue(BuildRunProgressPhase.Completed);

    /// <summary> Gets the complete closed progress phase set. </summary>
    public static IReadOnlyList<string> All => ContractLiteralCodec.GetLiterals<BuildRunProgressPhase>();
}
