using System.Xml.Linq;

namespace MackySoft.Ucli.Skills.Tests;

public sealed class ProjectBoundaryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void UcliSkillsProject_DoesNotReferenceContracts ()
    {
        var projectPath = Path.GetFullPath(Path.Combine(SkillTestData.GetDefinitionsRoot(), "..", "Ucli.Skills.csproj"));
        var document = XDocument.Load(projectPath);

        var references = document.Descendants("ProjectReference")
            .Select(static element => element.Attribute("Include")?.Value)
            .Where(static value => value is not null)
            .ToArray();

        Assert.Contains("../Ucli.Infrastructure/Ucli.Infrastructure.csproj", references);
        Assert.DoesNotContain(references, static reference => reference!.Contains("Ucli.Contracts", StringComparison.Ordinal));
    }
}
