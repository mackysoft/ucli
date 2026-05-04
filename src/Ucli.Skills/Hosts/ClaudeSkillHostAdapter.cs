using MackySoft.Ucli.Skills.Sources;

namespace MackySoft.Ucli.Skills.Hosts;

/// <summary> Materializes SKILL files for Claude Code. </summary>
public sealed class ClaudeSkillHostAdapter : ISkillHostAdapter
{
    /// <summary> The canonical Claude Code host key. </summary>
    public const string HostKey = "claude";

    /// <inheritdoc />
    public SkillHostDescriptor Descriptor { get; } = new(HostKey, ".claude/skills");

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
            .Mapping("disable-model-invocation", false)
            .DocumentMarker()
            .Build();

        return new SkillHostArtifactSet(frontmatter, []);
    }
}
