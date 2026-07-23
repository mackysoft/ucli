
namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Identifies how compile assurance obtained refresh evidence. </summary>
[VocabularyDefinition]
public enum CompileRefreshOrigin
{
    /// <summary> Compile assurance explicitly requested an AssetDatabase refresh. </summary>
    [VocabularyText("assetDatabaseRefresh")]
    AssetDatabaseRefresh = 1,

    /// <summary> Compile assurance recovered evidence by reading persisted diagnostics. </summary>
    [VocabularyText("diagnosticsRead")]
    DiagnosticsRead = 2,
}
