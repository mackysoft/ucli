namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Defines whether an operation is reachable from public request surfaces. </summary>
public enum UcliOperationExposure
{
    /// <summary> Allows public raw <c>kind:"op"</c> requests and edit lowering. </summary>
    Public = 0,

    /// <summary> Allows only operations produced by edit lowering. </summary>
    EditLoweringOnly = 1,

    /// <summary> Allows only internal execution paths outside public requests. </summary>
    Internal = 2,
}
