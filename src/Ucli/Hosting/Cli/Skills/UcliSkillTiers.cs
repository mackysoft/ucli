namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Defines product-owned uCLI SKILL tier literals. </summary>
internal static class UcliSkillTiers
{
    /// <summary> Gets all supported uCLI SKILL tier literals. </summary>
    public static IReadOnlyList<string> All { get; } = ["basic", "advanced", "developer"];
}
