namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;

/// <summary> Defines verify verifier effect values computed by uCLI. </summary>
internal static class VerifyEffectValues
{
    public const string AssetDatabaseRefresh = "assetDatabaseRefresh";
    public const string ScriptCompilation = "scriptCompilation";
    public const string DomainReload = "domainReload";
    public const string UnityTestRunner = "unityTestRunner";

    public static IReadOnlyList<string> Compile { get; } =
    [
        AssetDatabaseRefresh,
        ScriptCompilation,
        DomainReload,
    ];

    public static IReadOnlyList<string> Test { get; } = [UnityTestRunner];

    public static IReadOnlyList<string> None { get; } = [];
}
