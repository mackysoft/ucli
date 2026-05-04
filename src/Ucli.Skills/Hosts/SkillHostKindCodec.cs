using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Hosts;

/// <summary> Converts SKILL host values between enum and canonical literal forms. </summary>
public static class SkillHostKindCodec
{
    /// <summary> Converts one host enum value to the canonical literal. </summary>
    /// <param name="host"> The host value. </param>
    /// <returns> The canonical literal. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="host" /> is unsupported. </exception>
    public static string ToValue (SkillHostKind host)
    {
        return host switch
        {
            SkillHostKind.Claude => SkillHostKindValues.Claude,
            SkillHostKind.Copilot => SkillHostKindValues.Copilot,
            SkillHostKind.OpenAi => SkillHostKindValues.OpenAi,
            _ => throw new ArgumentOutOfRangeException(nameof(host), host, "Unsupported SKILL host."),
        };
    }

    /// <summary> Tries to parse one host literal. </summary>
    /// <param name="value"> The host literal. </param>
    /// <param name="host"> The parsed host. </param>
    /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out SkillHostKind host)
    {
        if (string.Equals(value, SkillHostKindValues.Claude, StringComparison.OrdinalIgnoreCase))
        {
            host = SkillHostKind.Claude;
            return true;
        }

        if (string.Equals(value, SkillHostKindValues.Copilot, StringComparison.OrdinalIgnoreCase))
        {
            host = SkillHostKind.Copilot;
            return true;
        }

        if (string.Equals(value, SkillHostKindValues.OpenAi, StringComparison.OrdinalIgnoreCase))
        {
            host = SkillHostKind.OpenAi;
            return true;
        }

        host = default;
        return false;
    }

    /// <summary> Parses one host literal into a result value. </summary>
    /// <param name="value"> The host literal. </param>
    /// <returns> The parsed host or unsupported-host failure. </returns>
    public static SkillOperationResult<SkillHostKind> Parse (string? value)
    {
        return TryParse(value, out var host)
            ? SkillOperationResult<SkillHostKind>.Success(host)
            : SkillOperationResult<SkillHostKind>.FailureResult(
                SkillFailureCodes.HostUnsupported,
                $"Unsupported SKILL host: {value ?? "(null)"}");
    }
}
