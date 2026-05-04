using System.Xml.Linq;

namespace MackySoft.Ucli.Skills.Tests;

public sealed class ProjectBoundaryTests
{
    private static readonly string[] ConcreteHostReferences =
    [
        "MackySoft.Ucli.Skills.Hosts.Claude",
        "MackySoft.Ucli.Skills.Hosts.Copilot",
        "MackySoft.Ucli.Skills.Hosts.OpenAi",
        "ClaudeSkillHostAdapter",
        "CopilotSkillHostAdapter",
        "OpenAiSkillHostAdapter",
    ];

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

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("Distribution")]
    [InlineData("Doctor")]
    [InlineData("Generation")]
    [InlineData("Installation")]
    [InlineData("Manifests")]
    [InlineData("Materialization")]
    public void NonHostDirectory_DoesNotReferenceConcreteHostImplementations (string directoryName)
    {
        var sourceRoot = GetSourceRoot();
        var directoryPath = Path.Combine(sourceRoot, directoryName);

        AssertDirectoryDoesNotContainAny(sourceRoot, directoryPath, ConcreteHostReferences);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("Contracts")]
    [InlineData("Registration")]
    [InlineData("Yaml")]
    public void HostInfrastructureDirectory_DoesNotReferenceConcreteHostImplementations (string directoryName)
    {
        var sourceRoot = GetSourceRoot();
        var directoryPath = Path.Combine(sourceRoot, "Hosts", directoryName);

        AssertDirectoryDoesNotContainAny(sourceRoot, directoryPath, ConcreteHostReferences);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("Claude", "ClaudeSkillHostAdapter.cs")]
    [InlineData("Copilot", "CopilotSkillHostAdapter.cs")]
    [InlineData("OpenAi", "OpenAiSkillHostAdapter.cs")]
    public void ConcreteHostImplementation_IsLocatedUnderConcreteHostDirectory (
        string hostDirectoryName,
        string fileName)
    {
        var sourceRoot = GetSourceRoot();
        var hostsRoot = Path.Combine(sourceRoot, "Hosts");
        var expectedPath = Path.GetFullPath(Path.Combine(hostsRoot, hostDirectoryName, fileName));

        Assert.True(File.Exists(expectedPath), $"Expected concrete host implementation file: {expectedPath}");

        var misplacedFiles = Directory.EnumerateFiles(hostsRoot, fileName, SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .Where(filePath => !string.Equals(filePath, expectedPath, StringComparison.Ordinal))
            .Select(filePath => Path.GetRelativePath(sourceRoot, filePath).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(misplacedFiles);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("Claude", "Copilot")]
    [InlineData("Claude", "OpenAi")]
    [InlineData("Copilot", "Claude")]
    [InlineData("Copilot", "OpenAi")]
    [InlineData("OpenAi", "Claude")]
    [InlineData("OpenAi", "Copilot")]
    public void ConcreteHostDirectory_DoesNotReferenceSiblingConcreteHostImplementation (
        string hostDirectoryName,
        string siblingHostDirectoryName)
    {
        var sourceRoot = GetSourceRoot();
        var directoryPath = Path.Combine(sourceRoot, "Hosts", hostDirectoryName);

        AssertDirectoryDoesNotContainAny(
            sourceRoot,
            directoryPath,
            [$"MackySoft.Ucli.Skills.Hosts.{siblingHostDirectoryName}", $"{siblingHostDirectoryName}SkillHostAdapter"]);
    }

    private static string GetSourceRoot ()
    {
        return Path.GetFullPath(Path.Combine(SkillTestData.GetDefinitionsRoot(), ".."));
    }

    private static void AssertDirectoryDoesNotContainAny (
        string sourceRoot,
        string directoryPath,
        IReadOnlyList<string> forbiddenTexts)
    {
        var offenders = Directory.EnumerateFiles(directoryPath, "*.cs", SearchOption.AllDirectories)
            .SelectMany(filePath => forbiddenTexts
                .Where(forbiddenText => File.ReadAllText(filePath).Contains(forbiddenText, StringComparison.Ordinal))
                .Select(forbiddenText => $"{Path.GetRelativePath(sourceRoot, filePath).Replace(Path.DirectorySeparatorChar, '/')} contains {forbiddenText}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }
}
