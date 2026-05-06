namespace MackySoft.Ucli.Architecture.Tests.Architecture;

public sealed class PublicSurfaceDeclarationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ReadPublicSurfaceDeclarations_includes_implicit_public_interface_members ()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("ucli-architecture-tests-");
        var sourceFile = Path.Combine(tempDirectory.FullName, "PublicApi.cs");
        File.WriteAllText(
            sourceFile,
            """
            namespace MackySoft.Ucli.Sample;

            public interface ISample
            {
                MackySoft.Ucli.Hosting.LeakedType Create();
            }
            """);

        try
        {
            var declarations = PublicSurfaceDeclarationExtractor
                .Read(sourceFile)
                .Select(static declaration => declaration.Signature)
                .ToArray();

            Assert.Contains(
                declarations,
                static signature => signature.Contains("MackySoft.Ucli.Hosting.LeakedType", StringComparison.Ordinal));
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadPublicSurfaceDeclarations_includes_types_in_block_scoped_namespace ()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("ucli-architecture-tests-");
        var sourceFile = Path.Combine(tempDirectory.FullName, "PublicApi.cs");
        File.WriteAllText(
            sourceFile,
            """
            namespace MackySoft.Ucli.Sample
            {
                public sealed class SampleApi
                {
                    public MackySoft.Ucli.Hosting.LeakedType Create() => throw new NotSupportedException();
                }
            }
            """);

        try
        {
            var declarations = PublicSurfaceDeclarationExtractor
                .Read(sourceFile)
                .Select(static declaration => declaration.Signature)
                .ToArray();

            Assert.Contains(
                declarations,
                static signature => signature.Contains("public sealed class SampleApi", StringComparison.Ordinal));
            Assert.Contains(
                declarations,
                static signature => signature.Contains("MackySoft.Ucli.Hosting.LeakedType", StringComparison.Ordinal));
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }
}
