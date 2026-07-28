using System.Text.Json.Serialization.Metadata;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.OperationReports.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Projects the shared Agent Skills list report into uCLI's emitted payload contract. </summary>
internal static class SkillsListCommandPayloadFactory
{
    /// <summary> Gets the serializer contract used by <c>skills list</c> payloads. </summary>
    public static JsonTypeInfo TypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(SkillsListCommandPayload));

    /// <summary> Projects one successful list report into its emitted payload. </summary>
    /// <param name="report"> The Agent Skills list report. </param>
    /// <returns> The payload serialized by the command result writer. </returns>
    public static SkillsListCommandPayload Create (SkillListReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new SkillsListCommandPayload(
            report.Categories.Select(ParseOfficialCategory).ToArray(),
            report.SkillNames,
            report.AvailableCategories.Select(CreateCategory).ToArray(),
            report.Skills.Select(CreateSkill).ToArray(),
            report.SupportedHosts.Select(CreateHost).ToArray());
    }

    private static SkillsListCategoryCommandPayload CreateCategory (SkillListCategoryReport category)
    {
        return new SkillsListCategoryCommandPayload(
            ParseOfficialCategory(category.Category),
            category.SkillCount);
    }

    private static SkillsListSkillCommandPayload CreateSkill (SkillListSkillReport skill)
    {
        return new SkillsListSkillCommandPayload(
            skill.SkillName,
            skill.DisplayName,
            skill.Description,
            skill.Dependencies,
            ParseOfficialCategory(skill.Category),
            skill.CatalogId,
            skill.SkillBundleVersion,
            Sha256Digest.Parse(skill.ContentDigest.ToString()),
            skill.HostArtifacts.Select(CreateHostArtifact).ToArray());
    }

    private static SkillsListHostArtifactCommandPayload CreateHostArtifact (SkillHostArtifactReport artifact)
    {
        return new SkillsListHostArtifactCommandPayload(
            UcliSkillCommandVocabularyMapper.Map<SkillHostKind, UcliOfficialSkillHost>(artifact.Host),
            artifact.Path,
            artifact.Digest is null
                ? null
                : Sha256Digest.Parse(artifact.Digest.ToString()),
            Sha256Digest.Parse(artifact.MaterializedFrontmatterDigest.ToString()));
    }

    private static SkillsListHostCommandPayload CreateHost (SkillHostReport host)
    {
        return new SkillsListHostCommandPayload(
            UcliSkillCommandVocabularyMapper.Map<SkillHostKind, UcliOfficialSkillHost>(host.Host),
            host.ProjectDefaultTargetPath,
            host.UserDefaultTargetPath,
            host.ReloadGuidance);
    }

    private static UcliOfficialSkillCategory ParseOfficialCategory (string category)
    {
        if (TextVocabulary.TryGetValue<UcliOfficialSkillCategory>(category, out var value))
        {
            return value;
        }

        throw new InvalidOperationException($"Agent Skills returned an unsupported official category: {category}.");
    }

}
