namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Vocabulary;

/// <summary> Defines assurance claim codes emitted by the <c>compile</c> command. </summary>
internal static class CompileClaimCodes
{
    public static readonly UcliCode UnityCompileNoErrors = new("UNITY_COMPILE_NO_ERRORS");

    public static readonly UcliCode UnityDomainReloadSettled = new("UNITY_DOMAIN_RELOAD_SETTLED");

    public static readonly UcliCode UnityLifecycleReadyAfterCompile = new("UNITY_LIFECYCLE_READY_AFTER_COMPILE");

    public static IReadOnlyList<UcliCode> All { get; } =
    [
        UnityCompileNoErrors,
        UnityDomainReloadSettled,
        UnityLifecycleReadyAfterCompile,
    ];

    public static IReadOnlyList<string> AllValues { get; } =
    [
        UnityCompileNoErrors.Value,
        UnityDomainReloadSettled.Value,
        UnityLifecycleReadyAfterCompile.Value,
    ];
}
