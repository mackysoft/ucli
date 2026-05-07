namespace MackySoft.Ucli.Skills.Shared;

/// <summary> Represents one machine-readable SKILL operation failure. </summary>
/// <param name="Code"> The failure code. </param>
/// <param name="Message"> The user-facing failure message. </param>
public sealed record SkillFailure (
    SkillFailureCode Code,
    string Message)
{
    /// <summary> Creates one SKILL failure. </summary>
    /// <param name="code"> The failure code. </param>
    /// <param name="message"> The user-facing failure message. </param>
    /// <returns> The created failure. </returns>
    public static SkillFailure Create (
        SkillFailureCode code,
        string message)
    {
        if (!code.IsValid)
        {
            throw new ArgumentException("Failure code must be valid.", nameof(code));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new SkillFailure(code, message);
    }
}
