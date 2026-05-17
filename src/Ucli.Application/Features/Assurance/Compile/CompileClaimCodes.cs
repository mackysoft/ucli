namespace MackySoft.Ucli.Application.Features.Assurance.Compile;

/// <summary> Defines assurance claim codes emitted by the <c>compile</c> command. </summary>
internal static class CompileClaimCodes
{
    public static readonly UcliCodeValue UnityCompileNoErrors = new("UNITY_COMPILE_NO_ERRORS");

    public static readonly UcliCodeValue UnityDomainReloadSettled = new("UNITY_DOMAIN_RELOAD_SETTLED");

    public static readonly UcliCodeValue UnityLifecycleReadyAfterCompile = new("UNITY_LIFECYCLE_READY_AFTER_COMPILE");

    public static IReadOnlyList<UcliCodeValue> All { get; } =
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
