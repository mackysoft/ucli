using MackySoft.Ucli.Skills.Shared;
using MackySoft.Ucli.Skills.Sources;

namespace MackySoft.Ucli.Skills.Hosts;

/// <summary> Materializes SKILL files for OpenAI / Codex. </summary>
public sealed class OpenAiSkillHostAdapter : ISkillHostAdapter
{
    /// <inheritdoc />
    public SkillHostDescriptor Descriptor { get; } = new(SkillHostKind.OpenAi, SkillHostKindValues.OpenAi, ".agents/skills");

    /// <inheritdoc />
    public string MetadataArtifactPath => "agents/openai.yaml";

    /// <inheritdoc />
    public SkillHostArtifactSet BuildArtifacts (SkillSourceMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var frontmatter = string.Join(
            "\n",
            "---",
            $"name: {SkillYamlScalarFormatter.DoubleQuoted(metadata.SkillName)}",
            $"description: {SkillYamlScalarFormatter.DoubleQuoted(metadata.Description)}",
            "---",
            string.Empty);

        var openAiYaml = string.Join(
            "\n",
            "interface:",
            $"  display_name: {SkillYamlScalarFormatter.DoubleQuoted(metadata.DisplayName)}",
            $"  short_description: {SkillYamlScalarFormatter.DoubleQuoted(metadata.Description)}",
            $"  default_prompt: {SkillYamlScalarFormatter.DoubleQuoted($"Use ${metadata.SkillName} to follow the {metadata.DisplayName} workflow.")}",
            string.Empty,
            "policy:",
            "  allow_implicit_invocation: true",
            string.Empty);

        return new SkillHostArtifactSet(
            frontmatter,
            [SkillPackageFile.Create(MetadataArtifactPath, openAiYaml)]);
    }
}
