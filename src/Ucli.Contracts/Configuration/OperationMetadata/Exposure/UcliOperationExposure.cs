
namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Defines how an operation is reachable from public request surfaces. </summary>
[VocabularyDefinition]
public enum UcliOperationExposure
{
    /// <summary> Allows public raw <c>kind:"op"</c> requests and edit lowering. </summary>
    [VocabularyText("public")]
    Public = 0,

    /// <summary> Allows only operations produced by edit lowering. </summary>
    [VocabularyText("editLoweringOnly")]
    EditLoweringOnly = 1,
}
