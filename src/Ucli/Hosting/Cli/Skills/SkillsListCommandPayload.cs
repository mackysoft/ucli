using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Represents the payload emitted by <c>skills list</c>. </summary>
internal sealed record SkillsListCommandPayload (
    IReadOnlyList<UcliOfficialSkillCategory> Categories,
    IReadOnlyList<string> SkillNames,
    IReadOnlyList<SkillsListCategoryCommandPayload> AvailableCategories,
    IReadOnlyList<SkillsListSkillCommandPayload> Skills,
    IReadOnlyList<SkillsListHostCommandPayload> SupportedHosts);

/// <summary> Represents one available official SKILL category. </summary>
internal sealed record SkillsListCategoryCommandPayload (
    UcliOfficialSkillCategory Category,
    int SkillCount);

/// <summary> Represents one official SKILL package in a list result. </summary>
internal sealed record SkillsListSkillCommandPayload (
    string SkillName,
    string DisplayName,
    string Description,
    IReadOnlyList<string> Dependencies,
    UcliOfficialSkillCategory Category,
    string CatalogId,
    int SkillBundleVersion,
    Sha256Digest ContentDigest,
    IReadOnlyList<SkillsListHostArtifactCommandPayload> HostArtifacts);

/// <summary> Represents one host artifact included in an official SKILL package. </summary>
internal sealed record SkillsListHostArtifactCommandPayload (
    UcliOfficialSkillHost Host,
    string? Path,
    Sha256Digest? Digest,
    Sha256Digest MaterializedFrontmatterDigest);

/// <summary> Represents one host supported by official SKILL packages. </summary>
internal sealed record SkillsListHostCommandPayload (
    UcliOfficialSkillHost Host,
    string ProjectTargetDirectory,
    string UserTargetDirectory,
    string ReloadGuidance);
