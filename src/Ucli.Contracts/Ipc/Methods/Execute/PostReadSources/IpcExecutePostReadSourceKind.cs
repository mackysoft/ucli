using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies the mutation source represented by an <c>execute</c> post-read fact. </summary>
public enum IpcExecutePostReadSourceKind
{
    /// <summary> Indicates a public edit step source. </summary>
    [UcliContractLiteral("edit")]
    Edit = 1,

    /// <summary> Indicates a public raw-operation step source. </summary>
    [UcliContractLiteral("operation")]
    Operation = 2,

    /// <summary> Indicates a project-refresh source. </summary>
    [UcliContractLiteral("refresh")]
    Refresh = 3,
}
