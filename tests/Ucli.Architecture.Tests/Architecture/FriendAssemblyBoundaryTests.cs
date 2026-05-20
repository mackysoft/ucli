namespace MackySoft.Ucli.Architecture.Tests.Architecture;

public sealed class FriendAssemblyBoundaryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void InternalsVisibleTo_lists_are_boundary_explicit ()
    {
        var ownedAssemblyInfoRoots = new[]
        {
            "src/Ucli.Application",
            "src/Ucli.Contracts",
            "src/Ucli.Infrastructure",
            "src/Ucli/Hosting",
            "src/Ucli.Unity/Assets/MackySoft/MackySoft.Ucli.Unity",
            "tests/Tests.Helper",
        };
        var expectedFriendsByAssemblyInfo = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["src/Ucli.Application/AssemblyInfo.cs"] =
            [
                "MackySoft.Ucli",
                "MackySoft.Ucli.Application.Tests",
                "MackySoft.Ucli.Tests",
            ],
            ["src/Ucli.Contracts/AssemblyInfo.cs"] =
            [
                "MackySoft.Ucli",
                "MackySoft.Ucli.Application",
                "MackySoft.Ucli.Application.Tests",
                "MackySoft.Ucli.Contracts.Tests",
                "MackySoft.Ucli.Infrastructure",
                "MackySoft.Ucli.Infrastructure.Tests",
                "MackySoft.Ucli.Tests",
                "MackySoft.Ucli.Unity.Editor",
                "MackySoft.Ucli.Unity.Tests.Editor",
            ],
            ["src/Ucli.Infrastructure/AssemblyInfo.cs"] =
            [
                "MackySoft.Ucli",
                "MackySoft.Ucli.Infrastructure.Tests",
                "MackySoft.Ucli.Tests",
                "MackySoft.Ucli.Unity.Editor",
                "MackySoft.Ucli.Unity.Tests.Editor",
            ],
            ["src/Ucli/Hosting/AssemblyInfo.cs"] =
            [
                "MackySoft.Ucli.Tests",
            ],
            ["src/Ucli.Unity/Assets/MackySoft/MackySoft.Ucli.Unity/Editor/AssemblyInfo.cs"] =
            [
                "MackySoft.Ucli.Unity.Tests.Editor",
            ],
            ["tests/Tests.Helper/AssemblyInfo.cs"] =
            [
                "MackySoft.Ucli.Application.Tests",
                "MackySoft.Ucli.Contracts.Tests",
                "MackySoft.Ucli.Infrastructure.Tests",
                "MackySoft.Ucli.Tests",
            ],
        };

        BoundaryAssertions.AssertAllowedItemsByPath(
            expectedFriendsByAssemblyInfo,
            ArchitectureTestRepository.EnumerateAssemblyInfoFiles(ownedAssemblyInfoRoots),
            InternalsVisibleToAssemblyReader.ReadAssemblyNames);
    }
}
