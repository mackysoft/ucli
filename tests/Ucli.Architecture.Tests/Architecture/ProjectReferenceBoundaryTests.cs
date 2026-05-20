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
            ["src/Ucli/Ucli.csproj"] =
            [
                "src/Ucli.Application/Ucli.Application.csproj",
                "src/Ucli.Contracts/Ucli.Contracts.csproj",
                "src/Ucli.Infrastructure/Ucli.Infrastructure.csproj",
            ],
        };

        BoundaryAssertions.AssertAllowedItemsByPath(
            expectedReferencesByProject,
            ArchitectureTestRepository.EnumerateProductionProjectFiles(),
            ProjectFileReferenceReader.ReadProjectReferences);
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
            ["tests/Ucli.Tests/Ucli.Tests.csproj"] =
            [
                "src/Ucli.Application/Ucli.Application.csproj",
                "src/Ucli.Contracts/Ucli.Contracts.csproj",
                "src/Ucli.Infrastructure/Ucli.Infrastructure.csproj",
                "src/Ucli/Ucli.csproj",
                "tests/Tests.Helper/Tests.Helper.csproj",
            ],
        };

        BoundaryAssertions.AssertAllowedItemsByPath(
            expectedReferencesByProject,
            ArchitectureTestRepository.EnumerateTestProjectFiles(),
            ProjectFileReferenceReader.ReadProjectReferences);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ToolProjects_reference_only_allowed_projects ()
    {
        var expectedReferencesByProject = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["tools/Ucli.SchemaGenerator/Ucli.SchemaGenerator.csproj"] =
            [
                "src/Ucli.Contracts/Ucli.Contracts.csproj",
            ],
        };

        BoundaryAssertions.AssertAllowedItemsByPath(
            expectedReferencesByProject,
            ArchitectureTestRepository.EnumerateToolProjectFiles(),
            ProjectFileReferenceReader.ReadProjectReferences);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MsBuildImportFiles_do_not_define_project_references ()
    {
        var violations = ArchitectureTestRepository
            .EnumerateMsBuildImportFiles()
            .Where(static importFile => ProjectFileReferenceReader.ReadProjectReferences(importFile).Length > 0)
            .ToArray();

        Assert.Empty(violations);
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

        BoundaryAssertions.AssertAllowedItemsByPath(
            expectedReferencesByAsmdef,
            Directory
                .EnumerateFiles(
                    ArchitectureTestRepository.ToFullPath("src/Ucli.Unity/Assets/MackySoft/MackySoft.Ucli.Unity"),
                    "*.asmdef",
                    SearchOption.AllDirectories)
                .Select(ArchitectureTestRepository.NormalizeRepositoryRelativePath),
            static asmdefPath => UnityAsmdefReferenceReader
                .Read(asmdefPath)
                .Where(static reference => reference.StartsWith("MackySoft.Ucli", StringComparison.Ordinal))
                .ToArray());
    }
}
