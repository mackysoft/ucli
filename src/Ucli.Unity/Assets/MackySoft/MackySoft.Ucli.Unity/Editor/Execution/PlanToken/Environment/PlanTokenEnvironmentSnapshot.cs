#nullable enable

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
    /// <summary> Represents one captured runtime snapshot used by plan-token workflows. </summary>
    /// <param name="ProjectRoot"> The Unity project root path. </param>
    /// <param name="RepositoryRoot"> The repository root path. </param>
    /// <param name="ProjectFingerprint"> The deterministic project fingerprint. </param>
    /// <param name="UnityVersion"> The current Unity version. </param>
    /// <param name="CompileState"> The current compile state. </param>
    /// <param name="DomainReloadGeneration"> The current domain-reload generation marker. </param>
    internal sealed record PlanTokenEnvironmentSnapshot (
        string ProjectRoot,
        string RepositoryRoot,
        string ProjectFingerprint,
        string UnityVersion,
        string CompileState,
        string DomainReloadGeneration);
}