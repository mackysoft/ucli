
namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Defines allowed operation safety levels configured in <c>.ucli/config.json</c>. </summary>
[VocabularyDefinition]
public enum OperationPolicy
{
    /// <summary> Allows only safe operations. </summary>
    [VocabularyText("safe")]
    Safe = 0,

    /// <summary> Allows safe and advanced operations. </summary>
    [VocabularyText("advanced")]
    Advanced = 1,

    /// <summary> Allows safe, advanced, and dangerous operations. </summary>
    [VocabularyText("dangerous")]
    Dangerous = 2,
}
