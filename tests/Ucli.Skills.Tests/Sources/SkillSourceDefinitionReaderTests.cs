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

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_Fails_WhenSkillNameIsNotSafeIdentifier ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "unsafe-skill-name");
        var skillDirectory = scope.CreateDirectory("SampleSkill");
        scope.WriteFile(
            "SampleSkill/skill.json",
            """
            {
              "schemaVersion": 1,
              "skillName": "SampleSkill",
              "displayName": "Sample Skill",
              "description": "Use when testing source validation.",
              "references": [
                "reference.md"
              ]
            }
            """);
        scope.WriteFile("SampleSkill/SKILL.md.template", "# Sample\n");
        scope.WriteFile("SampleSkill/references/reference.md.template", "# Reference\n");
        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_Fails_WhenSkillTemplateSymlinkEscapesSkillDirectory ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("ucli-skills", "template-symlink");
        using var outsideScope = TestDirectories.CreateTempScope("ucli-skills", "template-symlink-outside");
        var skillDirectory = WriteMinimalDefinition(scope, writeSkillTemplate: false);
        var outsideTemplate = outsideScope.WriteFile("outside-template.md", "# Outside\n");
        try
        {
            File.CreateSymbolicLink(Path.Combine(skillDirectory, "SKILL.md.template"), outsideTemplate);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOneAsync_Fails_WhenReferenceTemplateSymlinkEscapesSkillDirectory ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("ucli-skills", "reference-symlink");
        using var outsideScope = TestDirectories.CreateTempScope("ucli-skills", "reference-symlink-outside");
        var skillDirectory = WriteMinimalDefinition(scope, writeReferenceTemplate: false);
        var outsideTemplate = outsideScope.WriteFile("outside-reference.md", "# Outside\n");
        Directory.CreateDirectory(Path.Combine(skillDirectory, "references"));
        try
        {
            File.CreateSymbolicLink(Path.Combine(skillDirectory, "references", "reference.md.template"), outsideTemplate);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        var reader = new SkillSourceDefinitionReader();

        var result = await reader.ReadOneAsync(skillDirectory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    private static string WriteMinimalDefinition (
        TestDirectoryScope scope,
        string extraJsonProperty = "",
        bool writeSkillTemplate = true,
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
        if (writeSkillTemplate)
        {
            scope.WriteFile("sample-skill/SKILL.md.template", "# Sample\n");
        }

        if (writeReferenceTemplate)
        {
            scope.WriteFile("sample-skill/references/reference.md.template", "# Reference\n");
        }

        return skillDirectory;
    }
}
