using MackySoft.Ucli.Skills.Hosts.Contracts;
using MackySoft.Ucli.Skills.Hosts.Yaml;
using MackySoft.Ucli.Skills.Shared;
using MackySoft.Ucli.Skills.Sources;

namespace MackySoft.Ucli.Skills.Hosts.OpenAi;

/// <summary> Materializes SKILL files for OpenAI / Codex. </summary>
public sealed class OpenAiSkillHostAdapter : ISkillHostAdapter
{
    /// <summary> The canonical OpenAI / Codex host key. </summary>
    public const string HostKey = "openai";

    /// <inheritdoc />
    public SkillHostDescriptor Descriptor { get; } = new(HostKey, ".agents/skills");

    /// <inheritdoc />
    public string MetadataArtifactPath => "agents/openai.yaml";

    /// <inheritdoc />
    public SkillHostArtifactSet BuildArtifacts (SkillSourceMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var frontmatter = new SkillYamlBuilder()
            .DocumentMarker()
            .Mapping("name", metadata.SkillName)
            .Mapping("description", metadata.Description)
            .DocumentMarker()
            .Build();

        var openAiYaml = new SkillYamlBuilder()
            .Section("interface")
            .Mapping("display_name", metadata.DisplayName, indentationLevel: 1)
            .Mapping("short_description", metadata.Description, indentationLevel: 1)
            .Mapping("default_prompt", $"Use ${metadata.SkillName} to follow the {metadata.DisplayName} workflow.", indentationLevel: 1)
            .BlankLine()
            .Section("policy")
            .Mapping("allow_implicit_invocation", true, indentationLevel: 1)
            .Build();

        return new SkillHostArtifactSet(
            frontmatter,
            [SkillPackageFile.Create(MetadataArtifactPath, openAiYaml)]);
    }
}
