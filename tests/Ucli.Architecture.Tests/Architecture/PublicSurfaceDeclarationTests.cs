namespace MackySoft.Ucli.Architecture.Tests.Architecture;

public sealed class PublicSurfaceDeclarationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ReadPublicSurfaceDeclarations_includes_implicit_public_interface_members ()
    {
        var declarations = PublicSurfaceDeclarationExtractor
            .Read(
                """
                namespace MackySoft.Ucli.Sample;

                public interface ISample
                {
                    MackySoft.Ucli.Hosting.LeakedType Create();
                }
                """,
                "PublicApi.cs")
            .Select(static declaration => declaration.Signature)
            .ToArray();

        Assert.Contains(
            declarations,
            static signature => signature.Contains("MackySoft.Ucli.Hosting.LeakedType", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadPublicSurfaceDeclarations_includes_types_in_block_scoped_namespace ()
    {
        var declarations = PublicSurfaceDeclarationExtractor
            .Read(
                """
                namespace MackySoft.Ucli.Sample
                {
                    public sealed class SampleApi
                    {
                        public MackySoft.Ucli.Hosting.LeakedType Create() => throw new NotSupportedException();
                    }
                }
                """,
                "PublicApi.cs")
            .Select(static declaration => declaration.Signature)
            .ToArray();

        Assert.Contains(
            declarations,
            static signature => signature.Contains("public sealed class SampleApi", StringComparison.Ordinal));
        Assert.Contains(
            declarations,
            static signature => signature.Contains("MackySoft.Ucli.Hosting.LeakedType", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadPublicSurfaceDeclarations_includes_public_delegate_declarations ()
    {
        var declarations = PublicSurfaceDeclarationExtractor
            .Read(
                """
                namespace MackySoft.Ucli.Sample;

                public delegate MackySoft.Ucli.Hosting.LeakedType SampleFactory();
                """,
                "PublicApi.cs")
            .Select(static declaration => declaration.Signature)
            .ToArray();

        Assert.Contains(
            declarations,
            static signature => signature.Contains("public delegate MackySoft.Ucli.Hosting.LeakedType SampleFactory()", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadPublicSurfaceDeclarations_includes_same_line_attribute_declarations ()
    {
        var declarations = PublicSurfaceDeclarationExtractor
            .Read(
                """
                namespace MackySoft.Ucli.Sample;

                [Obsolete] public sealed class SampleApi
                {
                }
                """,
                "PublicApi.cs")
            .Select(static declaration => declaration.Signature)
            .ToArray();

        Assert.Contains(
            declarations,
            static signature => signature.Contains("public sealed class SampleApi", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadPublicSurfaceDeclarations_includes_attribute_namespaces ()
    {
        var declarations = PublicSurfaceDeclarationExtractor
            .Read(
                """
                namespace MackySoft.Ucli.Sample;

                [MackySoft.Ucli.Hosting.LeakedAttribute]
                public sealed class SampleApi
                {
                }
                """,
                "PublicApi.cs")
            .Select(static declaration => declaration.Signature)
            .ToArray();

        Assert.Contains(
            declarations,
            static signature => signature.Contains("MackySoft.Ucli.Hosting.LeakedAttribute", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadPublicSurfaceDeclarations_includes_multiline_attribute_namespaces ()
    {
        var declarations = PublicSurfaceDeclarationExtractor
            .Read(
                """
                namespace MackySoft.Ucli.Sample;

                [MackySoft.Ucli.Hosting.LeakedAttribute(
                    typeof(string))]
                public sealed class SampleApi
                {
                }
                """,
                "PublicApi.cs")
            .Select(static declaration => declaration.Signature)
            .ToArray();

        Assert.Contains(
            declarations,
            static signature => signature.Contains("MackySoft.Ucli.Hosting.LeakedAttribute", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadPublicSurfaceDeclarations_handles_attribute_array_arguments ()
    {
        var declarations = PublicSurfaceDeclarationExtractor
            .Read(
                """
                namespace MackySoft.Ucli.Sample;

                [Sample(new[] { typeof(string) })] public sealed class SampleApi
                {
                }
                """,
                "PublicApi.cs")
            .Select(static declaration => declaration.Signature)
            .ToArray();

        Assert.Contains(
            declarations,
            static signature => signature.Contains("public sealed class SampleApi", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadPublicSurfaceDeclarations_excludes_member_implementation_body ()
    {
        var declarations = PublicSurfaceDeclarationExtractor
            .Read(
                """
                namespace MackySoft.Ucli.Sample;

                public sealed class SampleApi
                {
                    public string Create() => MackySoft.Ucli.Hosting.LeakedImplementation.Value;
                }
                """,
                "PublicApi.cs")
            .Select(static declaration => declaration.Signature)
            .ToArray();

        Assert.DoesNotContain(
            declarations,
            static signature => signature.Contains("MackySoft.Ucli.Hosting.LeakedImplementation", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadPublicSurfaceDeclarations_includes_member_generic_constraints ()
    {
        var declarations = PublicSurfaceDeclarationExtractor
            .Read(
                """
                namespace MackySoft.Ucli.Sample;

                public sealed class SampleApi
                {
                    public T Create<T>()
                        where T : MackySoft.Ucli.Hosting.LeakedType
                    {
                        throw new NotSupportedException();
                    }
                }
                """,
                "PublicApi.cs")
            .Select(static declaration => declaration.Signature)
            .ToArray();

        Assert.Contains(
            declarations,
            static signature => signature.Contains("where T : MackySoft.Ucli.Hosting.LeakedType", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadPublicSurfaceDeclarations_preserves_relative_path ()
    {
        var declarations = PublicSurfaceDeclarationExtractor
            .Read(
                """
                namespace MackySoft.Ucli.Sample;

                public sealed class SampleApi
                {
                }
                """,
                "src/Sample/PublicApi.cs")
            .ToArray();

        Assert.Contains(
            declarations,
            static declaration => declaration.RelativePath == "src/Sample/PublicApi.cs");
    }
}
