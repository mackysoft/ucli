using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Identifies how compile assurance obtained refresh evidence. </summary>
public enum CompileRefreshOrigin
{
    /// <summary> Compile assurance explicitly requested an AssetDatabase refresh. </summary>
    [UcliContractLiteral("assetDatabaseRefresh")]
    AssetDatabaseRefresh = 1,

    /// <summary> Compile assurance recovered evidence by reading persisted diagnostics. </summary>
    [UcliContractLiteral("diagnosticsRead")]
    DiagnosticsRead = 2,
}
