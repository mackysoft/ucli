namespace MackySoft.Ucli.Architecture.Tests.Architecture;

public sealed class ProjectReferenceBoundaryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ProductionProjects_reference_only_allowed_projects ()
    {
        var expectedReferencesByProject = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["src/Ucli.Application/Ucli.Application.csproj"] =
            [
                "src/Ucli.Contracts/Ucli.Contracts.csproj",
            ],
            ["src/Ucli.Contracts/Ucli.Contracts.csproj"] = [],
            ["src/Ucli.Infrastructure/Ucli.Infrastructure.csproj"] =
            [
                "src/Ucli.Contracts/Ucli.Contracts.csproj",
            ],
            ["src/Ucli.Skills/Ucli.Skills.csproj"] = [],
            ["src/Ucli/Ucli.csproj"] =
            [
                "src/Ucli.Application/Ucli.Application.csproj",
                "src/Ucli.Contracts/Ucli.Contracts.csproj",
                "src/Ucli.Infrastructure/Ucli.Infrastructure.csproj",
                "src/Ucli.Skills/Ucli.Skills.csproj",
            ],
        };

        var actualProjectPaths = ArchitectureTestRepository
            .EnumerateProductionProjectFiles()
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            expectedReferencesByProject.Keys.OrderBy(static value => value, StringComparer.Ordinal),
            actualProjectPaths);

        foreach (var (projectPath, expectedReferences) in expectedReferencesByProject)
        {
            var actualReferences = ArchitectureTestRepository.ReadProjectReferences(projectPath);
            Assert.Equal(
                expectedReferences.OrderBy(static value => value, StringComparer.Ordinal),
                actualReferences.OrderBy(static value => value, StringComparer.Ordinal));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TestProjects_reference_only_allowed_projects ()
    {
        var expectedReferencesByProject = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["tests/Ucli.Architecture.Tests/Ucli.Architecture.Tests.csproj"] = [],
            ["tests/Tests.Helper/Tests.Helper.csproj"] = [],
            ["tests/Ucli.Application.Tests/Ucli.Application.Tests.csproj"] =
            [
                "src/Ucli.Application/Ucli.Application.csproj",
                "tests/Tests.Helper/Tests.Helper.csproj",
            ],
            ["tests/Ucli.Contracts.Tests/Ucli.Contracts.Tests.csproj"] =
            [
                "src/Ucli.Contracts/Ucli.Contracts.csproj",
                "tests/Tests.Helper/Tests.Helper.csproj",
            ],
            ["tests/Ucli.Infrastructure.Tests/Ucli.Infrastructure.Tests.csproj"] =
            [
                "src/Ucli.Contracts/Ucli.Contracts.csproj",
                "src/Ucli.Infrastructure/Ucli.Infrastructure.csproj",
                "tests/Tests.Helper/Tests.Helper.csproj",
            ],
            ["tests/Ucli.Skills.Tests/Ucli.Skills.Tests.csproj"] =
            [
                "src/Ucli.Skills/Ucli.Skills.csproj",
                "tests/Tests.Helper/Tests.Helper.csproj",
            ],
            ["tests/Ucli.Tests/Ucli.Tests.csproj"] =
            [
                "src/Ucli.Application/Ucli.Application.csproj",
                "src/Ucli.Contracts/Ucli.Contracts.csproj",
                "src/Ucli.Infrastructure/Ucli.Infrastructure.csproj",
                "src/Ucli/Ucli.csproj",
                "tests/Tests.Helper/Tests.Helper.csproj",
            ],
        };

        var actualProjectPaths = ArchitectureTestRepository
            .EnumerateTestProjectFiles()
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            expectedReferencesByProject.Keys.OrderBy(static value => value, StringComparer.Ordinal),
            actualProjectPaths);

        foreach (var (projectPath, expectedReferences) in expectedReferencesByProject)
        {
            var actualReferences = ArchitectureTestRepository.ReadProjectReferences(projectPath);
            Assert.Equal(
                expectedReferences.OrderBy(static value => value, StringComparer.Ordinal),
                actualReferences.OrderBy(static value => value, StringComparer.Ordinal));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Unity_editor_asmdef_references_only_allowed_ucli_assemblies ()
    {
        var expectedReferencesByAsmdef = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["src/Ucli.Unity/Assets/MackySoft/MackySoft.Ucli.Unity/Editor/MackySoft.Ucli.Unity.Editor.asmdef"] =
            [
                "MackySoft.Ucli.Contracts",
                "MackySoft.Ucli.Infrastructure",
            ],
        };

        var actualAsmdefPaths = Directory
            .EnumerateFiles(
                ArchitectureTestRepository.ToFullPath("src/Ucli.Unity/Assets/MackySoft/MackySoft.Ucli.Unity"),
                "*.asmdef",
                SearchOption.AllDirectories)
            .Select(ArchitectureTestRepository.NormalizeRepositoryRelativePath)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            expectedReferencesByAsmdef.Keys.OrderBy(static value => value, StringComparer.Ordinal),
            actualAsmdefPaths);

        foreach (var (asmdefPath, expectedReferences) in expectedReferencesByAsmdef)
        {
            var actualReferences = ArchitectureTestRepository
                .ReadUnityAsmdefReferences(asmdefPath)
                .Where(static reference => reference.StartsWith("MackySoft.Ucli", StringComparison.Ordinal))
                .ToArray();
            Assert.Equal(
                expectedReferences.OrderBy(static value => value, StringComparer.Ordinal),
                actualReferences.OrderBy(static value => value, StringComparer.Ordinal));
        }
    }

}
