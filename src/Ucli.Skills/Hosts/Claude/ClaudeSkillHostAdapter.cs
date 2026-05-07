using MackySoft.Ucli.Skills.Hosts.Contracts;
using MackySoft.Ucli.Skills.Serialization.Yaml;

namespace MackySoft.Ucli.Skills.Hosts.Claude;

/// <summary> Materializes SKILL files for Claude Code. </summary>
public sealed class ClaudeSkillHostAdapter : ISkillHostAdapter
{
    /// <summary> The canonical Claude Code host key. </summary>
    public const string HostKey = "claude";

    /// <inheritdoc />
    public SkillHostDescriptor Descriptor { get; } = new(
        HostKey,
        ".claude/skills",
        "~/.claude/skills",
        new SkillUserTargetRootPolicy(null, null, ".claude/skills"),
        "Claude Code watches existing skill directories. Restart Claude Code if the top-level skills directory was created after the session started.");

    /// <inheritdoc />
    public string? MetadataArtifactPath => null;

    /// <inheritdoc />
    public SkillHostArtifactSet BuildArtifacts (SkillHostMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var frontmatter = new DeterministicYamlBuilder()
            .DocumentMarker()
            .Mapping("name", metadata.SkillName)
            .Mapping("description", metadata.Description)
            .Mapping("disable-model-invocation", false)
            .DocumentMarker()
            .Build();

        return new SkillHostArtifactSet(frontmatter, null);
    }
}
