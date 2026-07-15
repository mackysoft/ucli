using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Identifies the action required to clear a daemon lifecycle blocker. </summary>
public enum DaemonDiagnosisActionRequired
{
    /// <summary> Script compilation errors must be fixed. </summary>
    [UcliContractLiteral("fixCompileErrors")]
    FixCompileErrors = 1,

    /// <summary> Unity package resolution errors must be resolved. </summary>
    [UcliContractLiteral("resolvePackages")]
    ResolvePackages = 2,

    /// <summary> A blocking Unity dialog must be resolved. </summary>
    [UcliContractLiteral("resolveUnityDialog")]
    ResolveUnityDialog = 3,

    /// <summary> The Unity log must be inspected to determine the required recovery action. </summary>
    [UcliContractLiteral("inspectUnityLog")]
    InspectUnityLog = 4,
}
