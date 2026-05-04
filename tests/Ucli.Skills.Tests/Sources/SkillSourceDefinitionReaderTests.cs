using MackySoft.Tests;
using MackySoft.Ucli.Skills.Shared;
using MackySoft.Ucli.Skills.Sources;

namespace MackySoft.Ucli.Skills.Tests.Sources;

public sealed class SkillSourceDefinitionReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_ReadsOfficialDefinitions ()
    {
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadAllAsync(SkillTestData.GetDefinitionsRoot(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(SkillTestData.ExpectedSkillNames, result.Value!.Select(static definition => definition.Metadata.SkillName).ToArray());
        Assert.All(result.Value!, static definition =>
        {
            Assert.DoesNotContain("---", definition.SkillTemplate.TrimStart().Split('\n')[0], StringComparison.Ordinal);
            Assert.NotEmpty(definition.References);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_Fails_WhenSkillJsonHasUnknownProperty ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "unknown-property");
        var skillDirectory = WriteMinimalDefinition(scope, extraJsonProperty: ",\n  \"hostAllowlist\": [\"openai\"]");
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_Fails_WhenSkillJsonIsMalformed ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "malformed-json");
        var skillDirectory = scope.CreateDirectory("sample-skill");
        scope.WriteFile("sample-skill/skill.json", "{");
        scope.WriteFile("sample-skill/SKILL.md.template", "# Sample\n");
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_Fails_WhenReferenceEscapesDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "reference-traversal");
        var skillDirectory = scope.CreateDirectory("sample-skill");
        scope.WriteFile(
            "sample-skill/skill.json",
            """
            {
              "schemaVersion": 1,
              "skillName": "sample-skill",
              "displayName": "Sample Skill",
              "description": "Use when testing source validation.",
              "references": [
                "../escape.md"
              ]
            }
            """);
        scope.WriteFile("sample-skill/SKILL.md.template", "# Sample\n");
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_Fails_WhenReferenceTemplateIsMissing ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "missing-reference");
        var skillDirectory = WriteMinimalDefinition(scope, writeReferenceTemplate: false);
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    private static string WriteMinimalDefinition (
        TestDirectoryScope scope,
        string extraJsonProperty = "",
        bool writeReferenceTemplate = true)
    {
        var skillDirectory = scope.CreateDirectory("sample-skill");
        scope.WriteFile(
            "sample-skill/skill.json",
            $$"""
            {
              "schemaVersion": 1,
              "skillName": "sample-skill",
              "displayName": "Sample Skill",
              "description": "Use when testing source validation.",
              "references": [
                "reference.md"
              ]{{extraJsonProperty}}
            }
            """);
        scope.WriteFile("sample-skill/SKILL.md.template", "# Sample\n");
        if (writeReferenceTemplate)
        {
            scope.WriteFile("sample-skill/references/reference.md.template", "# Reference\n");
        }

        return skillDirectory;
    }
}
