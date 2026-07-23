using System.Xml.Linq;

namespace MackySoft.Ucli.Tests.Packaging;

public sealed class PackageMetadataCentralProjectTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void Production_projects_pin_external_filesystem_package_to_fixed_version ()
    {
        var projectPaths = new[]
        {
            "src/Ucli/Ucli.csproj",
            "src/Ucli.Application/Ucli.Application.csproj",
            "src/Ucli.Infrastructure/Ucli.Infrastructure.csproj",
        };

        foreach (string projectPath in projectPaths)
        {
            XDocument document = XDocument.Load(TestRepositoryPaths.GetFullPath(projectPath));
            XElement packageReference = document
                .Descendants("PackageReference")
                .SingleOrDefault(static element => string.Equals(
                    element.Attribute("Include")?.Value,
                    "MackySoft.FileSystem",
                    StringComparison.Ordinal))
                ?? throw new InvalidOperationException(
                    $"{projectPath} must reference the MackySoft.FileSystem package.");

            Assert.Equal("[0.1.0]", packageReference.Attribute("Version")?.Value);
            if (string.Equals(projectPath, "src/Ucli/Ucli.csproj", StringComparison.Ordinal))
            {
                Assert.Equal("true", packageReference.Attribute("GeneratePathProperty")?.Value);
            }
            Assert.DoesNotContain(
                document.Descendants("ProjectReference"),
                static element => string.Equals(
                    Path.GetFileName(element.Attribute("Include")?.Value),
                    "MackySoft.FileSystem.csproj",
                    StringComparison.Ordinal));
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Source_projects_do_not_redefine_central_package_metadata ()
    {
        var sourceProjectPaths = new[]
        {
            "src/Ucli/Ucli.csproj",
            "src/Ucli.Application/Ucli.Application.csproj",
            "src/Ucli.Contracts/Ucli.Contracts.csproj",
            "src/Ucli.Infrastructure/Ucli.Infrastructure.csproj",
        };
        var violations = new List<string>();

        foreach (string projectPath in sourceProjectPaths)
        {
            XDocument document = XDocument.Load(TestRepositoryPaths.GetFullPath(projectPath));
            foreach (string propertyName in PackageMetadataTestSupport.CentralPackageMetadataProperties.Append("PackageVersion"))
            {
                if (document.Descendants(propertyName).Any())
                {
                    violations.Add($"{projectPath} redefines {propertyName}.");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Package metadata must be defined in Directory.Build.props only: " + string.Join(", ", violations));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Packable_projects_evaluate_expected_packaging_metadata ()
    {
        IReadOnlyDictionary<string, string> centralProperties = PackageMetadataTestSupport.ReadDirectoryBuildProperties();
        var expectedMetadataByProject = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal)
        {
            ["src/Ucli/Ucli.csproj"] = new(StringComparer.Ordinal)
            {
                ["PackageId"] = "MackySoft.Ucli",
                ["Description"] = "CLI workflow for Unity automation.",
                ["PackageTags"] = "ucli;unity;cli;automation",
                ["PackAsTool"] = "true",
                ["ToolCommandName"] = "ucli",
            },
            ["src/Ucli.Contracts/Ucli.Contracts.csproj"] = new(StringComparer.Ordinal)
            {
                ["PackageId"] = "MackySoft.Ucli.Contracts",
                ["Description"] = "Shared contract types for uCLI IPC protocol.",
                ["PackageTags"] = "ucli;unity;ipc",
            },
            ["src/Ucli.Infrastructure/Ucli.Infrastructure.csproj"] = new(StringComparer.Ordinal)
            {
                ["PackageId"] = "MackySoft.Ucli.Infrastructure",
                ["Description"] = "Shared infrastructure services for uCLI runtime components.",
                ["PackageTags"] = "ucli;unity;infrastructure",
            },
        };

        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> propertiesByProject =
            await PackageMetadataTestSupport.ReadEvaluatedPropertiesByProjectAsync(
                expectedMetadataByProject.Keys,
                PackageMetadataTestSupport.EvaluatedPackageMetadataProperties);

        foreach ((string projectPath, IReadOnlyDictionary<string, string> projectMetadata) in expectedMetadataByProject)
        {
            IReadOnlyDictionary<string, string> properties = propertiesByProject[projectPath];
            AssertEvaluatedProperty(properties, projectPath, "Version", centralProperties["Version"]);
            AssertEvaluatedProperty(properties, projectPath, "PackageVersion", centralProperties["Version"]);
            AssertEvaluatedProperty(properties, projectPath, "Authors", centralProperties["Authors"]);
            AssertEvaluatedProperty(properties, projectPath, "Company", centralProperties["Company"]);
            AssertEvaluatedProperty(properties, projectPath, "RepositoryUrl", centralProperties["RepositoryUrl"]);
            AssertEvaluatedProperty(properties, projectPath, "RepositoryType", centralProperties["RepositoryType"]);
            AssertEvaluatedProperty(properties, projectPath, "PackageLicenseFile", centralProperties["PackageLicenseFile"]);
            AssertEvaluatedProperty(properties, projectPath, "PackageReadmeFile", centralProperties["PackageReadmeFile"]);
            AssertEvaluatedProperty(properties, projectPath, "Copyright", centralProperties["Copyright"]);

            foreach ((string propertyName, string expectedValue) in projectMetadata)
            {
                AssertEvaluatedProperty(properties, projectPath, propertyName, expectedValue);
            }
        }
    }

    private static void AssertEvaluatedProperty (
        IReadOnlyDictionary<string, string> properties,
        string projectPath,
        string propertyName,
        string expectedValue)
    {
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        Assert.True(
            properties.TryGetValue(propertyName, out string? actualValue),
            $"{projectPath} did not evaluate {propertyName}.");
        Assert.Equal(expectedValue, actualValue);
    }
}
