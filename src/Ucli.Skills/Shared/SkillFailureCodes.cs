namespace MackySoft.Ucli.Skills.Shared;

/// <summary> Defines machine-readable failure codes for SKILL library operations. </summary>
public static class SkillFailureCodes
{
    /// <summary> The requested host is not supported by the global host adapter set. </summary>
    public const string HostUnsupported = "SKILL_HOST_UNSUPPORTED";

    /// <summary> A source definition is missing or invalid. </summary>
    public const string SourceInvalid = "SKILL_SOURCE_INVALID";

    /// <summary> A canonical manifest is missing or invalid. </summary>
    public const string ManifestInvalid = "SKILL_MANIFEST_INVALID";

    /// <summary> A requested path escapes the allowed target boundary. </summary>
    public const string PathUnsafe = "SKILL_PATH_UNSAFE";

    /// <summary> The target directory is not managed by a canonical uCLI SKILL manifest. </summary>
    public const string InstallTargetUnmanaged = "SKILL_INSTALL_TARGET_UNMANAGED";

    /// <summary> The target directory contains different SKILL content. </summary>
    public const string InstallTargetDigestMismatch = "SKILL_INSTALL_TARGET_DIGEST_MISMATCH";

    /// <summary> The target root appears to contain materialized output for another host. </summary>
    public const string InstallTargetHostConflict = "SKILL_INSTALL_TARGET_HOST_CONFLICT";

    /// <summary> The target directory could not be read for planning. </summary>
    public const string InstallTargetReadFailed = "SKILL_INSTALL_TARGET_READ_FAILED";

    /// <summary> The target directory could not be written atomically. </summary>
    public const string InstallTargetWriteFailed = "SKILL_INSTALL_TARGET_WRITE_FAILED";
}
