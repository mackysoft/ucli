namespace MackySoft.Ucli.Architecture.Tests.Architecture;

public sealed class PackageReferenceBoundaryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ProductionProjects_use_only_allowed_packages ()
    {
        var expectedPackagesByProject = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["src/Ucli.Application/Ucli.Application.csproj"] =
            [
                "Microsoft.Extensions.DependencyInjection.Abstractions",
            ],
            ["src/Ucli.Contracts/Ucli.Contracts.csproj"] =
            [
                "System.Text.Json",
            ],
            ["src/Ucli.Infrastructure/Ucli.Infrastructure.csproj"] = [],
            ["src/Ucli.Skills/Ucli.Skills.csproj"] = [],
            ["src/Ucli/Ucli.csproj"] =
            [
                "ConsoleAppFramework",
                "Microsoft.Extensions.DependencyInjection",
            ],
        };

        BoundaryAssertions.AssertAllowedItemsByPath(
            expectedPackagesByProject,
            ArchitectureTestRepository.EnumerateProductionProjectFiles(),
            ProjectFileReferenceReader.ReadPackageReferences);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TestProjects_use_only_allowed_packages ()
    {
        string[] expectedTestPackages =
        [
            "coverlet.collector",
            "Microsoft.NET.Test.Sdk",
            "xunit",
            "xunit.runner.visualstudio",
        ];
        var expectedPackagesByProject = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["tests/Ucli.Architecture.Tests/Ucli.Architecture.Tests.csproj"] = expectedTestPackages,
            ["tests/Tests.Helper/Tests.Helper.csproj"] = expectedTestPackages,
            ["tests/Ucli.Application.Tests/Ucli.Application.Tests.csproj"] = expectedTestPackages,
            ["tests/Ucli.Contracts.Tests/Ucli.Contracts.Tests.csproj"] = expectedTestPackages,
            ["tests/Ucli.Infrastructure.Tests/Ucli.Infrastructure.Tests.csproj"] = expectedTestPackages,
            ["tests/Ucli.Skills.Tests/Ucli.Skills.Tests.csproj"] = expectedTestPackages,
            ["tests/Ucli.Tests/Ucli.Tests.csproj"] = expectedTestPackages,
        };

        BoundaryAssertions.AssertAllowedItemsByPath(
            expectedPackagesByProject,
            ArchitectureTestRepository.EnumerateTestProjectFiles(),
            ProjectFileReferenceReader.ReadPackageReferences);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Unity_packages_config_contains_only_allowed_ucli_packages ()
    {
        var expectedPackageIds = new[]
        {
            "MackySoft.Ucli.Contracts",
            "MackySoft.Ucli.Infrastructure",
        };
        var actualPackageIds = ArchitectureTestRepository
            .ReadUnityPackageIds("src/Ucli.Unity/Assets/packages.config")
            .Where(static packageId => packageId.StartsWith("MackySoft.Ucli", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(
            expectedPackageIds.OrderBy(static value => value, StringComparer.Ordinal),
            actualPackageIds.OrderBy(static value => value, StringComparer.Ordinal));
    }
}
