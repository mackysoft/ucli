namespace MackySoft.Ucli.Skills.Shared;

/// <summary> Defines machine-readable failure codes for SKILL library operations. </summary>
public static class SkillFailureCodes
{
    /// <summary> Gets the code emitted when the requested host is not supported by the global host adapter set. </summary>
    public static readonly SkillFailureCode HostUnsupported = new("SKILL_HOST_UNSUPPORTED");

    /// <summary> Gets the code emitted when a source definition is missing or invalid. </summary>
    public static readonly SkillFailureCode SourceInvalid = new("SKILL_SOURCE_INVALID");

    /// <summary> Gets the code emitted when a canonical manifest is missing or invalid. </summary>
    public static readonly SkillFailureCode ManifestInvalid = new("SKILL_MANIFEST_INVALID");

    /// <summary> Gets the code emitted when a requested path escapes the allowed target boundary. </summary>
    public static readonly SkillFailureCode PathUnsafe = new("SKILL_PATH_UNSAFE");

    /// <summary> Gets the code emitted when a user-scope target root cannot be resolved from the current environment. </summary>
    public static readonly SkillFailureCode UserTargetUnavailable = new("SKILL_USER_TARGET_UNAVAILABLE");

    /// <summary> Gets the code emitted when the target directory is not managed by a canonical uCLI SKILL manifest. </summary>
    public static readonly SkillFailureCode InstallTargetUnmanaged = new("SKILL_INSTALL_TARGET_UNMANAGED");

    /// <summary> Gets the code emitted when the target directory contains different SKILL content. </summary>
    public static readonly SkillFailureCode InstallTargetDigestMismatch = new("SKILL_INSTALL_TARGET_DIGEST_MISMATCH");

    /// <summary> Gets the code emitted when installed host-independent SKILL content drifted. </summary>
    public static readonly SkillFailureCode InstallTargetContentDigestMismatch = new("SKILL_INSTALL_TARGET_CONTENT_DIGEST_MISMATCH");

    /// <summary> Gets the code emitted when installed SKILL frontmatter drifted. </summary>
    public static readonly SkillFailureCode InstallTargetFrontmatterDigestMismatch = new("SKILL_INSTALL_TARGET_FRONTMATTER_DIGEST_MISMATCH");

    /// <summary> Gets the code emitted when an installed host artifact drifted. </summary>
    public static readonly SkillFailureCode InstallTargetHostArtifactDigestMismatch = new("SKILL_INSTALL_TARGET_HOST_ARTIFACT_DIGEST_MISMATCH");

    /// <summary> Gets the code emitted when the installed file set drifted. </summary>
    public static readonly SkillFailureCode InstallTargetFileSetMismatch = new("SKILL_INSTALL_TARGET_FILE_SET_MISMATCH");

    /// <summary> Gets the code emitted when the installed package is clean but older than the bundled official package. </summary>
    public static readonly SkillFailureCode InstallTargetOutdated = new("SKILL_INSTALL_TARGET_OUTDATED");

    /// <summary> Gets the code emitted when the target root appears to contain materialized output for another host. </summary>
    public static readonly SkillFailureCode InstallTargetHostConflict = new("SKILL_INSTALL_TARGET_HOST_CONFLICT");

    /// <summary> Gets the code emitted when the target directory could not be read for planning. </summary>
    public static readonly SkillFailureCode InstallTargetReadFailed = new("SKILL_INSTALL_TARGET_READ_FAILED");

    /// <summary> Gets the code emitted when the target directory could not be written atomically. </summary>
    public static readonly SkillFailureCode InstallTargetWriteFailed = new("SKILL_INSTALL_TARGET_WRITE_FAILED");
}
