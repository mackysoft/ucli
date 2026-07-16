using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Identifies an observable effect computed by an assurance verifier. </summary>
public enum AssuranceEffect
{
    /// <summary> Unity refreshed the AssetDatabase. </summary>
    [UcliContractLiteral("assetDatabaseRefresh")]
    AssetDatabaseRefresh = 1,

    /// <summary> Unity compiled project scripts. </summary>
    [UcliContractLiteral("scriptCompilation")]
    ScriptCompilation = 2,

    /// <summary> Unity completed or evaluated a domain reload. </summary>
    [UcliContractLiteral("domainReload")]
    DomainReload = 3,

    /// <summary> Unity Test Runner executed tests. </summary>
    [UcliContractLiteral("unityTestRunner")]
    UnityTestRunner = 4,

    /// <summary> Reads Unity editor lifecycle telemetry. </summary>
    [UcliContractLiteral("unityLifecycleRead")]
    UnityLifecycleRead = 5,

    /// <summary> Executes Unity BuildPipeline. </summary>
    [UcliContractLiteral("unityBuildPipeline")]
    UnityBuildPipeline = 6,

    /// <summary> Reads the normalized Unity BuildReport. </summary>
    [UcliContractLiteral("unityBuildReportRead")]
    UnityBuildReportRead = 7,

    /// <summary> Reads the Unity editor log window for the build interval. </summary>
    [UcliContractLiteral("unityLogWindowRead")]
    UnityLogWindowRead = 8,

    /// <summary> Writes uCLI local artifacts. </summary>
    [UcliContractLiteral("ucliArtifactWrite")]
    UcliArtifactWrite = 9,

    /// <summary> Writes the build output manifest. </summary>
    [UcliContractLiteral("outputManifestWrite")]
    OutputManifestWrite = 10,

    /// <summary> Captures lifecycle generation snapshots. </summary>
    [UcliContractLiteral("generationSnapshot")]
    GenerationSnapshot = 11,

    /// <summary> Audits project mutations around build runner invocation. </summary>
    [UcliContractLiteral("projectMutationAudit")]
    ProjectMutationAudit = 12,

    /// <summary> Invokes a uCLI executeMethod build runner. </summary>
    [UcliContractLiteral("unityExecuteMethod")]
    UnityExecuteMethod = 13,
}
