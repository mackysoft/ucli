namespace MackySoft.Ucli.Application.Features.Assurance.Compile;

/// <summary> Defines assurance claim codes emitted by the <c>compile</c> command. </summary>
internal static class CompileClaimCodes
{
    public const string UnityCompileNoErrors = "UNITY_COMPILE_NO_ERRORS";

    public const string UnityDomainReloadSettled = "UNITY_DOMAIN_RELOAD_SETTLED";

    public const string UnityLifecycleReadyAfterCompile = "UNITY_LIFECYCLE_READY_AFTER_COMPILE";

    public static IReadOnlyList<string> All { get; } =
    [
        UnityCompileNoErrors,
        UnityDomainReloadSettled,
        UnityLifecycleReadyAfterCompile,
    ];
}
