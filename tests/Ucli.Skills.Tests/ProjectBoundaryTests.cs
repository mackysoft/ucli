using System.Xml.Linq;

namespace MackySoft.Ucli.Skills.Tests;

public sealed class ProjectBoundaryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void UcliSkillsProject_DoesNotReferenceInfrastructureOrContracts ()
    {
        var projectPath = Path.GetFullPath(Path.Combine(SkillTestData.GetDefinitionsRoot(), "..", "Ucli.Skills.csproj"));
        var document = XDocument.Load(projectPath);

        var references = document.Descendants("ProjectReference")
            .Select(static element => element.Attribute("Include")?.Value)
            .Where(static value => value is not null)
            .ToArray();

        Assert.DoesNotContain(references, static reference => reference!.Contains("Ucli.Infrastructure", StringComparison.Ordinal));
        Assert.DoesNotContain(references, static reference => reference!.Contains("Ucli.Contracts", StringComparison.Ordinal));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("Distribution", "MackySoft.Ucli.Skills.Installation")]
    [InlineData("Materialization", "MackySoft.Ucli.Skills.Installation")]
    [InlineData("Packaging", "MackySoft.Ucli.Skills.Installation")]
    [InlineData("Packaging", "MackySoft.Ucli.Skills.Materialization")]
    [InlineData("Packaging", "MackySoft.Ucli.Skills.Distribution")]
    [InlineData("Packaging", "MackySoft.Ucli.Skills.Doctor")]
    public void Directory_DoesNotReferenceForbiddenNamespace (
        string directoryName,
        string forbiddenNamespace)
    {
        var sourceRoot = GetSourceRoot();
        var directoryPath = Path.Combine(sourceRoot, directoryName);

        var offenders = Directory.EnumerateFiles(directoryPath, "*.cs", SearchOption.AllDirectories)
            .Where(filePath => File.ReadAllText(filePath).Contains(forbiddenNamespace, StringComparison.Ordinal))
            .Select(filePath => Path.GetRelativePath(sourceRoot, filePath).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("Generation", "agents/openai.yaml")]
    [InlineData("Manifests", "agents/openai.yaml")]
    [InlineData("Materialization", "agents/openai.yaml")]
    [InlineData("Generation", "OpenAiSkillHostAdapter.HostKey")]
    [InlineData("Manifests", "OpenAiSkillHostAdapter.HostKey")]
    [InlineData("Materialization", "OpenAiSkillHostAdapter.HostKey")]
    public void NonHostDirectory_DoesNotReferenceConcreteHostArtifacts (
        string directoryName,
        string concreteHostArtifact)
    {
        var sourceRoot = GetSourceRoot();
        var directoryPath = Path.Combine(sourceRoot, directoryName);

        var offenders = Directory.EnumerateFiles(directoryPath, "*.cs", SearchOption.AllDirectories)
            .Where(filePath => File.ReadAllText(filePath).Contains(concreteHostArtifact, StringComparison.Ordinal))
            .Select(filePath => Path.GetRelativePath(sourceRoot, filePath).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    private static string GetSourceRoot ()
    {
        return Path.GetFullPath(Path.Combine(SkillTestData.GetDefinitionsRoot(), ".."));
    }
}
