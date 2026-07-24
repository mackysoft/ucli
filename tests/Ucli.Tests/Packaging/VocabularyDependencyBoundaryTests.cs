using System.Xml.Linq;

namespace MackySoft.Ucli.Tests.Packaging;

public sealed class VocabularyDependencyBoundaryTests
{
    private const string VocabularyPackageVersionRange = "[0.1.0]";

    private static readonly IReadOnlyDictionary<string, string[]> ExpectedVocabularyPackagesByProject =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["src/Ucli/Ucli.csproj"] =
            [
                "MackySoft.Text.Vocabularies",
                "MackySoft.Text.Vocabularies.Json",
            ],
            ["src/Ucli.Application/Ucli.Application.csproj"] =
            [
                "MackySoft.Text.Vocabularies",
            ],
            ["src/Ucli.Contracts/Ucli.Contracts.csproj"] =
            [
                "MackySoft.Text.Vocabularies",
                "MackySoft.Text.Vocabularies.Json",
            ],
            ["src/Ucli.Infrastructure/Ucli.Infrastructure.csproj"] =
            [
                "MackySoft.Text.Vocabularies.Json",
            ],
            ["tests/Ucli.Application.Tests/Ucli.Application.Tests.csproj"] =
            [
                "MackySoft.Text.Vocabularies",
                "MackySoft.Text.Vocabularies.Json",
            ],
            ["tests/Ucli.Contracts.Tests/Ucli.Contracts.Tests.csproj"] =
            [
                "MackySoft.Text.Vocabularies",
            ],
            ["tests/Ucli.Tests/Ucli.Tests.csproj"] =
            [
                "MackySoft.Text.Vocabularies",
                "MackySoft.Text.Vocabularies.Json",
            ],
            ["tools/Ucli.SchemaGenerator/Ucli.SchemaGenerator.csproj"] =
            [
                "MackySoft.Text.Vocabularies",
            ],
        };

    [Fact]
    [Trait("Size", "Medium")]
    public void RepositoryProjects_UseExactExternalVocabularyPackageRange ()
    {
        foreach ((string projectPath, string[] expectedPackageIds) in ExpectedVocabularyPackagesByProject)
        {
            XDocument project = XDocument.Load(TestRepositoryPaths.GetFullPath(projectPath));
            IReadOnlyDictionary<string, string?> vocabularyReferences = project
                .Descendants("PackageReference")
                .Where(static element => element.Attribute("Include")?.Value.StartsWith(
                    "MackySoft.Text.Vocabularies",
                    StringComparison.Ordinal) == true)
                .ToDictionary(
                    static element => element.Attribute("Include")!.Value,
                    static element => element.Attribute("Version")?.Value,
                    StringComparer.Ordinal);

            Assert.Equal(
                expectedPackageIds.Order(StringComparer.Ordinal),
                vocabularyReferences.Keys.Order(StringComparer.Ordinal));
            Assert.All(
                vocabularyReferences,
                reference => Assert.Equal(VocabularyPackageVersionRange, reference.Value));
            Assert.DoesNotContain(
                project.Descendants("ProjectReference"),
                static reference => reference.Attribute("Include")?.Value.Contains(
                    "MackySoft.Text.Vocabularies",
                    StringComparison.Ordinal) == true);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void UnityNuGetConfig_RestoresExternalVocabulariesOnlyFromNugetOrg ()
    {
        XDocument configuration = XDocument.Load(TestRepositoryPaths.GetFullPath(
            "src/Ucli.Unity/Assets/NuGet.config"));
        XElement sourceMapping = configuration.Descendants("packageSourceMapping").Single();
        IReadOnlyDictionary<string, string[]> patternsBySource = sourceMapping
            .Elements("packageSource")
            .ToDictionary(
                static source => source.Attribute("key")!.Value,
                static source => source
                    .Elements("package")
                    .Select(static package => package.Attribute("pattern")!.Value)
                    .ToArray(),
                StringComparer.Ordinal);

        Assert.DoesNotContain("MackySoft.Text.Vocabularies", patternsBySource["LocalNuGet"]);
        Assert.DoesNotContain("MackySoft.Text.Vocabularies.Json", patternsBySource["LocalNuGet"]);
        Assert.Contains("MackySoft.Text.Vocabularies", patternsBySource["nuget.org"]);
        Assert.Contains("MackySoft.Text.Vocabularies.Json", patternsBySource["nuget.org"]);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void UcliRepository_DoesNotOwnVocabularyPackageBuildOrReleaseArtifacts ()
    {
        Assert.False(File.Exists(TestRepositoryPaths.GetFullPath(
            "src/MackySoft.Text.Vocabularies/MackySoft.Text.Vocabularies.csproj")));
        Assert.False(File.Exists(TestRepositoryPaths.GetFullPath(
            "src/MackySoft.Text.Vocabularies.Json/MackySoft.Text.Vocabularies.Json.csproj")));
        Assert.False(File.Exists(TestRepositoryPaths.GetFullPath(
            "tests/MackySoft.Text.Vocabularies.Tests/MackySoft.Text.Vocabularies.Tests.csproj")));
        Assert.False(File.Exists(TestRepositoryPaths.GetFullPath(
            "tests/MackySoft.Text.Vocabularies.Json.Tests/MackySoft.Text.Vocabularies.Json.Tests.csproj")));

        string ownedBuildInputs = string.Join(
            "\n",
            new[]
            {
                "Ucli.slnx",
                ".github/workflows/package-publish.yaml",
                ".github/workflows/verify.yaml",
                "scripts/verify-release-package-artifacts.sh",
            }.Select(path => File.ReadAllText(TestRepositoryPaths.GetFullPath(path))));

        Assert.DoesNotContain("MackySoft.Text.Vocabularies", ownedBuildInputs, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void UcliReleaseVersionSync_DoesNotRewriteExternalVocabularyVersions ()
    {
        string versionSyncScript = File.ReadAllText(TestRepositoryPaths.GetFullPath(
            "scripts/sync-package-version.sh"));

        Assert.DoesNotContain("MackySoft.Text.Vocabularies", versionSyncScript, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void UcliProductSource_DoesNotRetainLegacyVocabularyFoundation ()
    {
        string repositoryRoot = TestRepositoryPaths.GetFullPath(".");
        string source = string.Join(
            "\n",
            new[]
            {
                "src/Ucli",
                "src/Ucli.Application",
                "src/Ucli.Contracts",
                "src/Ucli.Infrastructure",
            }
                .Select(path => Path.Combine(repositoryRoot, path))
                .SelectMany(path => Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));

        Assert.DoesNotContain("ContractLiteralCodec", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ContractLiteralInputParser", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CliStreamEntryFormatCodec", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LiteralCodecUtilities", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UcliContractLiteral", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UnityExecutionModeCodec", source, StringComparison.Ordinal);
    }
}
