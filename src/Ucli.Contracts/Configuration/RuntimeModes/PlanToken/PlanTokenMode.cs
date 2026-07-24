
namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Defines plan-token requirements configured in <c>.ucli/config.json</c>. </summary>
[VocabularyDefinition]
public enum PlanTokenMode
{
    /// <summary> Allows command execution with or without a plan token. </summary>
    [VocabularyText("optional")]
    Optional = 0,

    /// <summary> Requires command execution to include a plan token. </summary>
    [VocabularyText("required")]
    Required = 1,
}
