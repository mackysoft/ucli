using System.Text.Json;
using System.Xml.Linq;

namespace MackySoft.Ucli.Tests.Packaging;

public sealed class PackageMetadataManifestTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void Cli_tool_project_includes_generated_schema_artifacts ()
    {
        XDocument document = XDocument.Load(TestRepositoryPaths.GetFullPath("src/Ucli/Ucli.csproj"));
        var schemaItem = document
            .Descendants("None")
            .SingleOrDefault(static element => string.Equals(
                element.Attribute("Include")?.Value,
                "../../schemas/**/*",
                StringComparison.Ordinal));

        Assert.NotNull(schemaItem);
        Assert.Equal("schemas", schemaItem!.Attribute("LinkBase")?.Value);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Agent_skills_cli_tool_manifest_pins_expected_package ()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(TestRepositoryPaths.GetFullPath(".config", "dotnet-tools.json")));
        JsonElement tool = document.RootElement
            .GetProperty("tools")
            .GetProperty("mackysoft.agentskills.cli");

        Assert.Equal("1.0.0", tool.GetProperty("version").GetString());
        Assert.Contains(
            "agent-skills",
            tool.GetProperty("commands").EnumerateArray().Select(static command => command.GetString()));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Unity_nuspec_metadata_matches_central_package_metadata ()
    {
        IReadOnlyDictionary<string, string> centralProperties = PackageMetadataTestSupport.ReadDirectoryBuildProperties();
        XNamespace nuspecNamespace = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd";
        XDocument nuspecDocument = XDocument.Load(TestRepositoryPaths.GetFullPath("src/Ucli.Unity/MackySoft.Ucli.Unity.nuspec"));
        XElement metadata = nuspecDocument.Root?.Element(nuspecNamespace + "metadata")
            ?? throw new InvalidOperationException("Unity nuspec metadata element was not found.");

        Assert.Equal(centralProperties["Authors"], PackageMetadataTestSupport.ReadRequiredElementValue(metadata, nuspecNamespace, "authors"));
        Assert.Equal(centralProperties["Company"], PackageMetadataTestSupport.ReadRequiredElementValue(metadata, nuspecNamespace, "owners"));
        Assert.Equal("file", PackageMetadataTestSupport.ReadRequiredElement(metadata, nuspecNamespace, "license").Attribute("type")?.Value);
        Assert.Equal(centralProperties["PackageLicenseFile"], PackageMetadataTestSupport.ReadRequiredElementValue(metadata, nuspecNamespace, "license"));
        Assert.Equal(centralProperties["PackageReadmeFile"], PackageMetadataTestSupport.ReadRequiredElementValue(metadata, nuspecNamespace, "readme"));
        Assert.Equal(centralProperties["RepositoryType"], PackageMetadataTestSupport.ReadRequiredElement(metadata, nuspecNamespace, "repository").Attribute("type")?.Value);
        Assert.Equal(centralProperties["RepositoryUrl"], PackageMetadataTestSupport.ReadRequiredElement(metadata, nuspecNamespace, "repository").Attribute("url")?.Value);

        IReadOnlyDictionary<string, string> packageConfigVersions = PackageMetadataTestSupport.ReadUnityPackageConfigVersions();
        IReadOnlyDictionary<string, string> nuspecDependencyVersions = PackageMetadataTestSupport.ReadNuspecDependencyVersions(metadata, nuspecNamespace);
        Assert.Equal(centralProperties["Version"], packageConfigVersions["MackySoft.Ucli.Contracts"]);
        Assert.Equal(centralProperties["Version"], packageConfigVersions["MackySoft.Ucli.Infrastructure"]);
        Assert.Equal(packageConfigVersions["MackySoft.Ucli.Contracts"], nuspecDependencyVersions["MackySoft.Ucli.Contracts"]);
        Assert.Equal(packageConfigVersions["MackySoft.Ucli.Infrastructure"], nuspecDependencyVersions["MackySoft.Ucli.Infrastructure"]);
    }
}
