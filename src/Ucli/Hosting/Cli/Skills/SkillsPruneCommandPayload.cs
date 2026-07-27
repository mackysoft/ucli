namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Represents the payload emitted by <c>skills prune</c>. </summary>
internal sealed record SkillsPruneCommandPayload (
    UcliOfficialSkillHost Host,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> SkillNames,
    UcliSkillScope Scope,
    string? RepositoryRoot,
    string TargetRoot,
    bool DryRun,
    bool Force,
    string ReloadGuidance,
    IReadOnlyList<SkillsOperationActionCommandPayload<UcliSkillPruneAction>> Actions,
    int DeletedCount,
    int SkippedCurrentCount,
    int SkippedForeignCatalogCount,
    int SkippedUnmanagedCount,
    int BlockedCount);
