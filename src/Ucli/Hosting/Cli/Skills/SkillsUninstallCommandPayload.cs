namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Represents the payload emitted by <c>skills uninstall</c>. </summary>
internal sealed record SkillsUninstallCommandPayload (
    UcliOfficialSkillHost Host,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> SkillNames,
    UcliSkillScope Scope,
    string? RepositoryRoot,
    string TargetRoot,
    bool DryRun,
    bool Force,
    string ReloadGuidance,
    IReadOnlyList<SkillsOperationActionCommandPayload<UcliSkillUninstallAction>> Actions,
    int DeletedCount,
    int NoOpCount,
    int SkippedUnmanagedCount,
    int BlockedCount);
