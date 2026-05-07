using MackySoft.Ucli.Skills.Hosts.Contracts;
using MackySoft.Ucli.Skills.Serialization.Yaml;

namespace MackySoft.Ucli.Skills.Hosts.Copilot;

/// <summary> Materializes SKILL files for GitHub Copilot CLI. </summary>
public sealed class CopilotSkillHostAdapter : ISkillHostAdapter
{
    /// <summary> The canonical GitHub Copilot CLI host key. </summary>
    public const string HostKey = "copilot";

    /// <inheritdoc />
    public SkillHostDescriptor Descriptor { get; } = new(
        HostKey,
        ".github/skills",
        "~/.copilot/skills",
        new SkillUserTargetRootPolicy(null, null, ".copilot/skills"),
        "Run /skills reload in GitHub Copilot CLI to load newly installed or updated skills.");

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
            .Mapping("user-invocable", true)
            .DocumentMarker()
            .Build();

        return new SkillHostArtifactSet(frontmatter, null);
    }
}
