using System.Text.Json;
using System.Xml.Linq;

namespace MackySoft.Ucli.Tests.Packaging;

public sealed class PackageMetadataManifestTests
{
    private const string JsonCanonicalizationPackageVersion = "0.1.0";

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
        Assert.Equal(JsonCanonicalizationPackageVersion, packageConfigVersions["MackySoft.Json.Canonicalization"]);
        Assert.Equal(centralProperties["Version"], packageConfigVersions["MackySoft.Ucli.Contracts"]);
        Assert.Equal(centralProperties["Version"], packageConfigVersions["MackySoft.Ucli.Infrastructure"]);
        Assert.Equal($"[{JsonCanonicalizationPackageVersion}]", nuspecDependencyVersions["MackySoft.Json.Canonicalization"]);
        Assert.Equal(packageConfigVersions["MackySoft.Ucli.Contracts"], nuspecDependencyVersions["MackySoft.Ucli.Contracts"]);
        Assert.Equal(packageConfigVersions["MackySoft.Ucli.Infrastructure"], nuspecDependencyVersions["MackySoft.Ucli.Infrastructure"]);
        Assert.DoesNotContain("es6numberserializer", packageConfigVersions.Keys);
        Assert.DoesNotContain("es6numberserializer", nuspecDependencyVersions.Keys);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Json_canonicalization_package_references_are_exact ()
    {
        XDocument applicationProject = XDocument.Load(
            TestRepositoryPaths.GetFullPath("src/Ucli.Application/Ucli.Application.csproj"));
        XDocument cliProject = XDocument.Load(
            TestRepositoryPaths.GetFullPath("src/Ucli/Ucli.csproj"));
        string versionSyncScript = File.ReadAllText(
            TestRepositoryPaths.GetFullPath("scripts/sync-package-version.sh"));

        XElement applicationReference = applicationProject
            .Descendants("PackageReference")
            .Single(static element => element.Attribute("Include")?.Value == "MackySoft.Json.Canonicalization");
        XElement cliReference = cliProject
            .Descendants("PackageReference")
            .Single(static element => element.Attribute("Include")?.Value == "MackySoft.Json.Canonicalization");

        Assert.Equal($"[{JsonCanonicalizationPackageVersion}]", applicationReference.Attribute("Version")?.Value);
        Assert.Equal($"[{JsonCanonicalizationPackageVersion}]", cliReference.Attribute("Version")?.Value);
        Assert.Equal("true", cliReference.Attribute("GeneratePathProperty")?.Value);
        foreach (XDocument project in new[] { applicationProject, cliProject })
        {
            Assert.DoesNotContain(
                project.Descendants("ProjectReference"),
                static element => element.Attribute("Include")?.Value?.Contains(
                    "MackySoft.Json.Canonicalization",
                    StringComparison.Ordinal) == true);
        }
        Assert.DoesNotContain("MackySoft.Json.Canonicalization", versionSyncScript, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Cli_tool_redistributes_provider_notice_material_from_restored_package ()
    {
        XDocument cliProject = XDocument.Load(
            TestRepositoryPaths.GetFullPath("src/Ucli/Ucli.csproj"));
        IReadOnlyDictionary<string, string> expectedLinks = new Dictionary<string, string>
        {
            ["$(PkgMackySoft_Json_Canonicalization)/LICENSE"] =
                $"third-party/MackySoft.Json.Canonicalization/{JsonCanonicalizationPackageVersion}/LICENSE",
            ["$(PkgMackySoft_Json_Canonicalization)/THIRD-PARTY-NOTICES.md"] =
                $"third-party/MackySoft.Json.Canonicalization/{JsonCanonicalizationPackageVersion}/THIRD-PARTY-NOTICES.md",
            ["$(PkgMackySoft_Json_Canonicalization)/licenses/Apache-2.0.txt"] =
                $"third-party/MackySoft.Json.Canonicalization/{JsonCanonicalizationPackageVersion}/licenses/Apache-2.0.txt",
            ["$(PkgMackySoft_Json_Canonicalization)/licenses/MPL-2.0.txt"] =
                $"third-party/MackySoft.Json.Canonicalization/{JsonCanonicalizationPackageVersion}/licenses/MPL-2.0.txt",
        };

        foreach ((string include, string link) in expectedLinks)
        {
            XElement noticeItem = cliProject
                .Descendants("None")
                .Single(element => element.Attribute("Include")?.Value == include);

            Assert.Equal(link, noticeItem.Attribute("Link")?.Value);
            Assert.Equal("PreserveNewest", noticeItem.Element("CopyToOutputDirectory")?.Value);
            Assert.Equal("PreserveNewest", noticeItem.Element("CopyToPublishDirectory")?.Value);
        }

        string cliNotice = File.ReadAllText(
            TestRepositoryPaths.GetFullPath("src/Ucli/THIRD-PARTY-NOTICES"));
        Assert.Contains(
            $"MackySoft.Json.Canonicalization {JsonCanonicalizationPackageVersion}",
            cliNotice,
            StringComparison.Ordinal);
        Assert.Contains(
            $"third-party/MackySoft.Json.Canonicalization/{JsonCanonicalizationPackageVersion}/",
            cliNotice,
            StringComparison.Ordinal);
    }
}
