using System.Xml.Linq;

namespace MackySoft.Ucli.Tests.Packaging;

public sealed class ProjectPackabilityTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void Projects_declare_expected_packability ()
    {
        var expectedPackabilityByProject = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["src/Ucli.Application/Ucli.Application.csproj"] = "false",
            ["src/Ucli.Contracts/Ucli.Contracts.csproj"] = "true",
            ["src/Ucli.Infrastructure/Ucli.Infrastructure.csproj"] = "true",
            ["src/Ucli/Ucli.csproj"] = "true",
            ["tests/Tests.Helper/Tests.Helper.csproj"] = "false",
            ["tests/System/ScreenshotFidelity/Oracle.Windows/ScreenshotFidelityOracle.Windows.csproj"] = "false",
            ["tests/Ucli.Application.Tests/Ucli.Application.Tests.csproj"] = "false",
            ["tests/Ucli.Contracts.Tests/Ucli.Contracts.Tests.csproj"] = "false",
            ["tests/Ucli.Infrastructure.Tests/Ucli.Infrastructure.Tests.csproj"] = "false",
            ["tests/Ucli.Tests/Ucli.Tests.csproj"] = "false",
            ["tools/Ucli.SchemaGenerator/Ucli.SchemaGenerator.csproj"] = "false",
        };

        string[] actualProjectPaths = PackageMetadataTestSupport.EnumerateDotNetProjectPaths();
        Assert.Equal(
            expectedPackabilityByProject.Keys.OrderBy(static path => path, StringComparer.Ordinal),
            actualProjectPaths);

        foreach ((string projectPath, string expectedIsPackable) in expectedPackabilityByProject)
        {
            XDocument document = XDocument.Load(TestRepositoryPaths.GetFullPath(projectPath));
            string actualIsPackable = document.Descendants("IsPackable").Single().Value;
            Assert.Equal(expectedIsPackable, actualIsPackable);
        }
    }
}
