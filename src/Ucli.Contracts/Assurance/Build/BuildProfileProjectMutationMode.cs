using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines build profile project-mutation mode literals. </summary>
public enum BuildProfileProjectMutationMode
{
    /// <summary> Forbids project mutations during the build run. </summary>
    [UcliContractLiteral("forbid")]
    Forbid = 0,

    /// <summary> Audits project mutations without blocking the build verdict. </summary>
    [UcliContractLiteral("audit")]
    Audit = 1,

    /// <summary> Allows project mutations and records an audit trail. </summary>
    [UcliContractLiteral("allowWithAudit")]
    AllowWithAudit = 2,
}
