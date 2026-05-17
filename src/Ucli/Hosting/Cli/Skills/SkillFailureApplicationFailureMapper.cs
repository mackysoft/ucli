using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Maps SKILL-domain failures to the shared CLI failure model. </summary>
internal static class SkillFailureApplicationFailureMapper
{
    /// <summary> Maps one SKILL-domain failure to a classified application failure. </summary>
    public static ApplicationFailure Map (SkillFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        var code = new UcliCode(failure.Code.Value);
        return IsInvalidArgumentFailureCode(failure.Code)
            ? ApplicationFailure.InvalidInput(failure.Message, code)
            : ApplicationFailure.InternalError(failure.Message, code);
    }

    private static bool IsInvalidArgumentFailureCode (SkillFailureCode code)
    {
        return code == SkillFailureCodes.HostUnsupported || code == SkillFailureCodes.PathUnsafe;
    }
}
