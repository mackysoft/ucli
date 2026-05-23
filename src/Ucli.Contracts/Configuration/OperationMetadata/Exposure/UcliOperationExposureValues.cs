namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Defines literal values for operation exposure contract facts. </summary>
public static class UcliOperationExposureValues
{
    /// <summary> Gets the value that allows public raw <c>kind:"op"</c> requests and edit lowering. </summary>
    public const string Public = "public";

    /// <summary> Gets the value that allows only operations produced by edit lowering. </summary>
    public const string EditLoweringOnly = "editLoweringOnly";
}
