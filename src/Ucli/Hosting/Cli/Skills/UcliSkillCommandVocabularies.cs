namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Identifies the target scope emitted by uCLI SKILL lifecycle commands. </summary>
[VocabularyDefinition]
internal enum UcliSkillScope
{
    [VocabularyText("project")]
    Project = 0,

    [VocabularyText("user")]
    User = 1,
}

/// <summary> Identifies the artifact format emitted by <c>skills export</c>. </summary>
[VocabularyDefinition]
internal enum UcliSkillExportFormat
{
    [VocabularyText("directory")]
    Directory = 0,

    [VocabularyText("zip")]
    Zip = 1,
}

/// <summary> Identifies an install action emitted by <c>skills install</c>. </summary>
[VocabularyDefinition]
internal enum UcliSkillInstallAction
{
    [VocabularyText("created")]
    Created = 0,

    [VocabularyText("updated")]
    Updated = 1,

    [VocabularyText("noOp")]
    NoOp = 2,

    [VocabularyText("blockedManagedOverwrite")]
    BlockedManagedOverwrite = 3,

    [VocabularyText("blockedLocalModification")]
    BlockedLocalModification = 4,

    [VocabularyText("blockedUnmanaged")]
    BlockedUnmanaged = 5,
}

/// <summary> Identifies an update action emitted by <c>skills update</c>. </summary>
[VocabularyDefinition]
internal enum UcliSkillUpdateAction
{
    [VocabularyText("created")]
    Created = 0,

    [VocabularyText("updated")]
    Updated = 1,

    [VocabularyText("noOp")]
    NoOp = 2,

    [VocabularyText("blockedLocalModification")]
    BlockedLocalModification = 3,

    [VocabularyText("blockedUnmanaged")]
    BlockedUnmanaged = 4,

    [VocabularyText("blockedVersionAhead")]
    BlockedVersionAhead = 5,
}

/// <summary> Identifies an uninstall action emitted by <c>skills uninstall</c>. </summary>
[VocabularyDefinition]
internal enum UcliSkillUninstallAction
{
    [VocabularyText("deleted")]
    Deleted = 0,

    [VocabularyText("noOp")]
    NoOp = 1,

    [VocabularyText("skippedUnmanaged")]
    SkippedUnmanaged = 2,

    [VocabularyText("blockedLocalModification")]
    BlockedLocalModification = 3,
}

/// <summary> Identifies a prune action emitted by <c>skills prune</c>. </summary>
[VocabularyDefinition]
internal enum UcliSkillPruneAction
{
    [VocabularyText("deleted")]
    Deleted = 0,

    [VocabularyText("skippedCurrent")]
    SkippedCurrent = 1,

    [VocabularyText("skippedForeignCatalog")]
    SkippedForeignCatalog = 2,

    [VocabularyText("skippedUnmanaged")]
    SkippedUnmanaged = 3,

    [VocabularyText("blockedLocalModification")]
    BlockedLocalModification = 4,

    [VocabularyText("blockedManifestInvalid")]
    BlockedManifestInvalid = 5,

    [VocabularyText("blockedNameCollision")]
    BlockedNameCollision = 6,

    [VocabularyText("blockedHostConflict")]
    BlockedHostConflict = 7,
}

/// <summary> Identifies why one SKILL lifecycle action was blocked. </summary>
[VocabularyDefinition]
internal enum UcliSkillBlockedReason
{
    [VocabularyText("managedOverwriteRequiresForce")]
    ManagedOverwriteRequiresForce = 0,

    [VocabularyText("localModificationRequiresForce")]
    LocalModificationRequiresForce = 1,

    [VocabularyText("unmanagedTarget")]
    UnmanagedTarget = 2,

    [VocabularyText("installedVersionAhead")]
    InstalledVersionAhead = 3,
}

/// <summary> Identifies a file change emitted in one SKILL operation diff. </summary>
[VocabularyDefinition]
internal enum UcliSkillDiffChangeKind
{
    [VocabularyText("added")]
    Added = 0,

    [VocabularyText("modified")]
    Modified = 1,

    [VocabularyText("deleted")]
    Deleted = 2,
}

/// <summary> Identifies the severity emitted by <c>skills doctor</c>. </summary>
[VocabularyDefinition]
internal enum UcliSkillDoctorSeverity
{
    [VocabularyText("info")]
    Info = 0,

    [VocabularyText("error")]
    Error = 1,
}
