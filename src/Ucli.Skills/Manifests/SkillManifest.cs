namespace MackySoft.Ucli.Skills.Manifests;

/// <summary> Represents the canonical <c>ucli-skill.json</c> manifest. </summary>
/// <param name="SchemaVersion"> The manifest schema version. </param>
/// <param name="SkillName"> The skill name. </param>
/// <param name="DisplayName"> The display name shown by SKILL listing commands. </param>
/// <param name="Description"> The host-independent SKILL description. </param>
/// <param name="ContentDigest"> The host-independent content digest. </param>
/// <param name="HostArtifacts"> The host-specific artifact digests. </param>
public sealed record SkillManifest (
    int SchemaVersion,
    string SkillName,
    string DisplayName,
    string Description,
    string ContentDigest,
    IReadOnlyList<SkillHostArtifactManifest> HostArtifacts)
{
    /// <summary> Gets the current manifest schema version. </summary>
    public const int CurrentSchemaVersion = 1;
}
