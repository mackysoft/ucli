using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;

/// <summary> Defines build verifier effect literals. </summary>
internal enum BuildEffect
{
    /// <summary> Reads Unity editor lifecycle telemetry. </summary>
    [UcliContractLiteral("unityLifecycleRead")]
    UnityLifecycleRead = 0,

    /// <summary> Executes Unity BuildPipeline. </summary>
    [UcliContractLiteral("unityBuildPipeline")]
    UnityBuildPipeline = 1,

    /// <summary> Reads the normalized Unity BuildReport. </summary>
    [UcliContractLiteral("unityBuildReportRead")]
    UnityBuildReportRead = 2,

    /// <summary> Reads the Unity editor log window for the build interval. </summary>
    [UcliContractLiteral("unityLogWindowRead")]
    UnityLogWindowRead = 3,

    /// <summary> Writes uCLI local artifacts. </summary>
    [UcliContractLiteral("ucliArtifactWrite")]
    UcliArtifactWrite = 4,

    /// <summary> Writes the build output manifest. </summary>
    [UcliContractLiteral("outputManifestWrite")]
    OutputManifestWrite = 5,

    /// <summary> Captures lifecycle generation snapshots. </summary>
    [UcliContractLiteral("generationSnapshot")]
    GenerationSnapshot = 6,

    /// <summary> Audits project mutations around build runner invocation. </summary>
    [UcliContractLiteral("projectMutationAudit")]
    ProjectMutationAudit = 7,

    /// <summary> Invokes a uCLI executeMethod build runner. </summary>
    [UcliContractLiteral("unityExecuteMethod")]
    UnityExecuteMethod = 8,
}
