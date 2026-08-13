namespace MackySoft.Ucli.Application.Shared.Configuration;

/// <summary> Represents serializable Work Close completion policy values. </summary>
internal sealed record UcliWorkCompletionDocument (string[] RequiredProgramPresets);

/// <summary> Represents the additional Program Presets required to complete Work Close. </summary>
internal sealed record UcliWorkCompletion (IReadOnlyList<string> RequiredProgramPresets)
{
    /// <summary> Gets the policy with no additional required Program Presets. </summary>
    public static UcliWorkCompletion Empty { get; } = new(Array.Empty<string>());
}
