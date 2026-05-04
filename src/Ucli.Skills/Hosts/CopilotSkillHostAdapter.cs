using MackySoft.Ucli.Skills.Sources;

namespace MackySoft.Ucli.Skills.Hosts;

/// <summary> Materializes SKILL files for GitHub Copilot CLI. </summary>
public sealed class CopilotSkillHostAdapter : ISkillHostAdapter
{
    /// <inheritdoc />
    public SkillHostDescriptor Descriptor { get; } = new(SkillHostKind.Copilot, SkillHostKindValues.Copilot, ".github/skills");

    /// <inheritdoc />
    public SkillHostArtifactSet BuildArtifacts (SkillSourceMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var frontmatter = string.Join(
            "\n",
            "---",
            $"name: {SkillYamlScalarFormatter.DoubleQuoted(metadata.SkillName)}",
            $"description: {SkillYamlScalarFormatter.DoubleQuoted(metadata.Description)}",
            "user-invocable: true",
            "---",
            string.Empty);

        return new SkillHostArtifactSet(frontmatter, []);
    }
}
