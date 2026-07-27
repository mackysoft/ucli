namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Represents the payload emitted by <c>skills install</c>. </summary>
internal sealed record SkillsInstallCommandPayload (
    UcliOfficialSkillHost Host,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> SkillNames,
    UcliSkillScope Scope,
    string? RepositoryRoot,
    string TargetRoot,
    bool DryRun,
    bool Force,
    bool PrintDiff,
    string ReloadGuidance,
    IReadOnlyList<SkillsOperationActionCommandPayload<UcliSkillInstallAction>> Actions,
    int CreatedCount,
    int UpdatedCount,
    int NoOpCount,
    int BlockedCount);
