
namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines build profile project-mutation mode literals. </summary>
[VocabularyDefinition]
public enum BuildProfileProjectMutationMode
{
    /// <summary> Forbids project mutations during the build run. </summary>
    [VocabularyText("forbid")]
    Forbid = 1,

    /// <summary> Audits project mutations without blocking the build verdict. </summary>
    [VocabularyText("audit")]
    Audit = 2,

    /// <summary> Allows project mutations and records an audit trail. </summary>
    [VocabularyText("allowWithAudit")]
    AllowWithAudit = 3,
}
