namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Represents the payload emitted by <c>skills update</c>. </summary>
internal sealed record SkillsUpdateCommandPayload (
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
    IReadOnlyList<SkillsOperationActionCommandPayload<UcliSkillUpdateAction>> Actions,
    int CreatedCount,
    int UpdatedCount,
    int NoOpCount,
    int BlockedCount);
