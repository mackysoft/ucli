
namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Represents runtime policy resolved from a build profile. </summary>
internal sealed class ResolvedBuildRuntimePolicy
{
    /// <summary> Initializes a resolved runtime policy. </summary>
    public ResolvedBuildRuntimePolicy (
        IReadOnlyList<BuildProfileRuntimeExecutionMode> allowedExecutionModes,
        IReadOnlyList<DaemonEditorMode> allowedEditorModes)
    {
        AllowedExecutionModes = CopyDefinedValues(allowedExecutionModes, nameof(allowedExecutionModes));
        AllowedEditorModes = CopyDefinedValues(allowedEditorModes, nameof(allowedEditorModes));
    }

    /// <summary> Gets the allowed execution modes. </summary>
    public IReadOnlyList<BuildProfileRuntimeExecutionMode> AllowedExecutionModes { get; }

    /// <summary> Gets the allowed Unity Editor modes. </summary>
    public IReadOnlyList<DaemonEditorMode> AllowedEditorModes { get; }

    private static IReadOnlyList<TEnum> CopyDefinedValues<TEnum> (
        IReadOnlyList<TEnum> values,
        string parameterName)
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        if (values.Count == 0)
        {
            throw new ArgumentException("Runtime policy must contain at least one value.", parameterName);
        }

        var copiedValues = new TEnum[values.Count];
        var seenValues = new HashSet<TEnum>();
        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i];
            if (!TextVocabulary.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Runtime policy value must be defined.");
            }

            if (!seenValues.Add(value))
            {
                throw new ArgumentException($"Runtime policy contains duplicate value '{value}'.", parameterName);
            }

            copiedValues[i] = value;
        }

        return Array.AsReadOnly(copiedValues);
    }
}
