namespace MackySoft.Ucli.Architecture.Tests.Architecture;

public sealed class PublicSurfaceBoundaryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Dotnet_public_surface_does_not_expose_internal_implementation_namespaces ()
    {
        var forbiddenPublicSurfaceMarkers = new[]
        {
            "MackySoft.Ucli.Application.Features",
            "MackySoft.Ucli.Application.Shared",
            "MackySoft.Ucli.Contracts.Ipc.ContractReading",
            "MackySoft.Ucli.Contracts.Ipc.EditSteps",
            "MackySoft.Ucli.Features",
            "MackySoft.Ucli.Hosting",
            "MackySoft.Ucli.Shared",
            "MackySoft.Ucli.UnityIntegration",
        };
        var sourceFiles = new[]
            {
                "src/Ucli",
                "src/Ucli.Application",
                "src/Ucli.Contracts",
                "src/Ucli.Infrastructure",
                "src/Ucli.Skills",
            }
            .SelectMany(ArchitectureTestRepository.EnumerateCSharpSourceFiles);

        var violations = new List<string>();
        foreach (var sourceFile in sourceFiles)
        {
            foreach (var declaration in PublicSurfaceDeclarationReader.Read(sourceFile))
            {
                foreach (var marker in forbiddenPublicSurfaceMarkers)
                {
                    if (declaration.Namespace.StartsWith(marker, StringComparison.Ordinal)
                        || declaration.Signature.Contains(marker, StringComparison.Ordinal))
                    {
                        violations.Add($"{declaration.RelativePath}:{declaration.LineNumber} exposes {marker}.");
                    }
                }
            }
        }

        Assert.Empty(violations);
    }
}
