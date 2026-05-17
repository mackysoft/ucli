namespace MackySoft.Ucli.Application.Features.Assurance.Compile;

/// <summary> Defines compile assurance effect literals. </summary>
internal static class CompileEffectValues
{
    public const string AssetDatabaseRefresh = "assetDatabaseRefresh";

    public const string ScriptCompilation = "scriptCompilation";

    public const string DomainReload = "domainReload";

    public static IReadOnlyList<string> All { get; } =
    [
        AssetDatabaseRefresh,
        ScriptCompilation,
        DomainReload,
    ];
}
