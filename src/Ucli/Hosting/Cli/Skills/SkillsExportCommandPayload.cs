namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Represents the payload emitted by <c>skills export</c>. </summary>
internal sealed record SkillsExportCommandPayload (
    UcliOfficialSkillHost Host,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> SkillNames,
    UcliSkillExportFormat Format,
    string OutputRoot,
    IReadOnlyList<string> Skills,
    int SkillCount,
    string ReloadGuidance);
