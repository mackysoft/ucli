using MackySoft.Ucli.Skills.Sources;

namespace MackySoft.Ucli.Skills.Hosts;

/// <summary> Materializes SKILL files for Claude Code. </summary>
public sealed class ClaudeSkillHostAdapter : ISkillHostAdapter
{
    /// <inheritdoc />
    public SkillHostDescriptor Descriptor { get; } = new(SkillHostKind.Claude, SkillHostKindValues.Claude, ".claude/skills");

    /// <inheritdoc />
    public SkillHostArtifactSet BuildArtifacts (SkillSourceMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var frontmatter = string.Join(
            "\n",
            "---",
            $"name: {SkillYamlScalarFormatter.DoubleQuoted(metadata.SkillName)}",
            $"description: {SkillYamlScalarFormatter.DoubleQuoted(metadata.Description)}",
            "disable-model-invocation: false",
            "---",
            string.Empty);

        return new SkillHostArtifactSet(frontmatter, []);
    }
}
