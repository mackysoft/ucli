using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Defines how an operation is reachable from public request surfaces. </summary>
public enum UcliOperationExposure
{
    /// <summary> Allows public raw <c>kind:"op"</c> requests and edit lowering. </summary>
    [UcliContractLiteral("public")]
    Public = 0,

    /// <summary> Allows only operations produced by edit lowering. </summary>
    [UcliContractLiteral("editLoweringOnly")]
    EditLoweringOnly = 1,
}
