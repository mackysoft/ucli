using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines stable project mutation change-kind literals. </summary>
public enum IpcBuildProjectMutationChangeKind
{
    /// <summary> A project file was added. </summary>
    [UcliContractLiteral("added")]
    Added = 0,

    /// <summary> A project file changed content. </summary>
    [UcliContractLiteral("modified")]
    Modified = 1,

    /// <summary> A project file was deleted. </summary>
    [UcliContractLiteral("deleted")]
    Deleted = 2,
}
