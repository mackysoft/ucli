using MackySoft.Ucli.Skills.Sources;

namespace MackySoft.Ucli.Skills.Hosts;

/// <summary> Materializes SKILL files for GitHub Copilot CLI. </summary>
public sealed class CopilotSkillHostAdapter : ISkillHostAdapter
{
    /// <summary> The canonical GitHub Copilot CLI host key. </summary>
    public const string HostKey = "copilot";

    /// <inheritdoc />
    public SkillHostDescriptor Descriptor { get; } = new(HostKey, ".github/skills");

    /// <inheritdoc />
    public string? MetadataArtifactPath => null;

    /// <inheritdoc />
    public SkillHostArtifactSet BuildArtifacts (SkillSourceMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var frontmatter = new SkillYamlBuilder()
            .DocumentMarker()
            .Mapping("name", metadata.SkillName)
            .Mapping("description", metadata.Description)
            .Mapping("user-invocable", true)
            .DocumentMarker()
            .Build();

        return new SkillHostArtifactSet(frontmatter, []);
    }
}
