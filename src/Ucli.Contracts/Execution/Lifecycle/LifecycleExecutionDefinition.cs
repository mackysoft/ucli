using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Execution.Lifecycle;

/// <summary> Represents the immutable typed definition of one Lifecycle Execution. </summary>
public sealed record LifecycleExecutionDefinition
{
    /// <summary> Initializes the definition for one supported lifecycle action. </summary>
    /// <param name="kind"> The action whose result-affecting inputs are fixed by this definition. </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="kind" /> is not a defined Lifecycle Execution kind.
    /// </exception>
    [JsonConstructor]
    public LifecycleExecutionDefinition (LifecycleExecutionKind kind)
    {
        if (!TextVocabulary.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Lifecycle Execution kind must be defined.");
        }

        Kind = kind;
    }

    /// <summary> Gets the action fixed by this definition. </summary>
    [JsonInclude]
    [JsonRequired]
    public LifecycleExecutionKind Kind { get; private init; }

    /// <summary> Gets the open common execution-kind value corresponding to <see cref="Kind" />. </summary>
    [JsonIgnore]
    public ExecutionKind ExecutionKind => new(TextVocabulary.GetText(Kind));
}
