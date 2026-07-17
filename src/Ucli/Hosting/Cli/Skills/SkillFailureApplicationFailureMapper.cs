using MackySoft.AgentSkills.Shared;
using MackySoft.Ucli.Application.Shared.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Maps SKILL-domain failures to the shared CLI failure model. </summary>
internal static class SkillFailureApplicationFailureMapper
{
    private const string SkillCategoryMismatchMessageFragment = "does not match selected categories";
    private const string EmptyHostMessage = "SKILL host literal must not be empty.";
    private const string EmptyScopeMessage = "SKILL scope literal must not be empty.";
    private const string UnsupportedScopeLiteralPrefix = "Unsupported SKILL scope literal: ";

    /// <summary> Maps one SKILL-domain failure to a classified application failure. </summary>
    public static ApplicationFailure Map (SkillFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        var message = NormalizeMessage(failure.Message);
        var code = new UcliCode(failure.Code.Value);
        return ShouldUseDefaultInvalidArgumentCode(failure, message)
            ? ApplicationFailure.InvalidInput(message)
            : IsInvalidArgumentFailureCode(failure.Code)
            ? ApplicationFailure.InvalidInput(message, code)
            : ApplicationFailure.InternalError(message, code);
    }

    private static bool ShouldUseDefaultInvalidArgumentCode (
        SkillFailure failure,
        string message)
    {
        if (failure.Code == SkillFailureCodes.ScopeUnsupported)
        {
            return true;
        }

        return failure.Code == SkillFailureCodes.InputInvalid
            && !message.Contains(SkillCategoryMismatchMessageFragment, StringComparison.Ordinal);
    }

    private static bool IsInvalidArgumentFailureCode (SkillFailureCode code)
    {
        return code == SkillFailureCodes.HostUnsupported
            || code == SkillFailureCodes.InputInvalid
            || code == SkillFailureCodes.ScopeUnsupported
            || code == SkillFailureCodes.PathUnsafe;
    }

    private static string NormalizeMessage (string message)
    {
        return NormalizeRepositoryRootOptionName(NormalizeScopeMessage(NormalizeRequiredOptionMessage(message)));
    }

    private static string NormalizeRequiredOptionMessage (string message)
    {
        return message switch
        {
            EmptyHostMessage => "Option '--host' is required.",
            EmptyScopeMessage => "Option '--scope' is required.",
            _ => message,
        };
    }

    private static string NormalizeScopeMessage (string message)
    {
        if (!message.StartsWith(UnsupportedScopeLiteralPrefix, StringComparison.Ordinal))
        {
            return message;
        }

        var literalStartIndex = UnsupportedScopeLiteralPrefix.Length;
        var literalEndIndex = message.IndexOf('.', literalStartIndex);
        if (literalEndIndex <= literalStartIndex)
        {
            return message;
        }

        var literal = message[literalStartIndex..literalEndIndex];
        return $"Unsupported SKILL scope: {literal}.";
    }

    private static string NormalizeRepositoryRootOptionName (string message)
    {
        return message.Replace("--repositoryRoot", "--repoRoot", StringComparison.Ordinal);
    }
}
