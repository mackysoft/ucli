using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Defines high-level operation kinds exposed by the operation catalog. </summary>
public enum UcliOperationKind
{
    /// <summary> Represents read-only operations. </summary>
    [UcliContractLiteral("query")]
    Query = 0,

    /// <summary> Represents state-changing operations that can dirty or persist project content. </summary>
    [UcliContractLiteral("mutation")]
    Mutation = 1,

    /// <summary> Represents editor-state commands that are not content mutations. </summary>
    [UcliContractLiteral("command")]
    Command = 2,
}
