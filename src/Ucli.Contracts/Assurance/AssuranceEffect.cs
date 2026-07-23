
namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Identifies an observable effect computed by an assurance verifier. </summary>
[VocabularyDefinition]
public enum AssuranceEffect
{
    /// <summary> Unity refreshed the AssetDatabase. </summary>
    [VocabularyText("assetDatabaseRefresh")]
    AssetDatabaseRefresh = 1,

    /// <summary> Unity compiled project scripts. </summary>
    [VocabularyText("scriptCompilation")]
    ScriptCompilation = 2,

    /// <summary> Unity completed or evaluated a domain reload. </summary>
    [VocabularyText("domainReload")]
    DomainReload = 3,

    /// <summary> Unity Test Runner executed tests. </summary>
    [VocabularyText("unityTestRunner")]
    UnityTestRunner = 4,

    /// <summary> Reads Unity editor lifecycle telemetry. </summary>
    [VocabularyText("unityLifecycleRead")]
    UnityLifecycleRead = 5,

    /// <summary> Executes Unity BuildPipeline. </summary>
    [VocabularyText("unityBuildPipeline")]
    UnityBuildPipeline = 6,

    /// <summary> Reads the normalized Unity BuildReport. </summary>
    [VocabularyText("unityBuildReportRead")]
    UnityBuildReportRead = 7,

    /// <summary> Reads the Unity editor log window for the build interval. </summary>
    [VocabularyText("unityLogWindowRead")]
    UnityLogWindowRead = 8,

    /// <summary> Writes uCLI local artifacts. </summary>
    [VocabularyText("ucliArtifactWrite")]
    UcliArtifactWrite = 9,

    /// <summary> Writes the build output manifest. </summary>
    [VocabularyText("outputManifestWrite")]
    OutputManifestWrite = 10,

    /// <summary> Captures lifecycle generation snapshots. </summary>
    [VocabularyText("generationSnapshot")]
    GenerationSnapshot = 11,

    /// <summary> Audits project mutations around build runner invocation. </summary>
    [VocabularyText("projectMutationAudit")]
    ProjectMutationAudit = 12,

    /// <summary> Invokes a uCLI executeMethod build runner. </summary>
    [VocabularyText("unityExecuteMethod")]
    UnityExecuteMethod = 13,
}
