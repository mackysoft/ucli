using MackySoft.FileSystem;
using MackySoft.Ucli.Hosting.Cli.Schemas;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Schemas;

public sealed class InstalledStaticSchemaSetProviderTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void Load_WhenAssemblyInformationalVersionMatchesManifest_AcceptsBuildMetadata ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "installed-static-schema-set",
            "matching-version");
        var schemaRoot =
            StaticSchemaSetTestSupport.CopyRepositorySchemaSet(scope);
        var packageVersion = StaticSchemaSetTestSupport
            .ReadManifest(schemaRoot)["packageVersion"]!
            .GetValue<string>();
        var provider = new InstalledStaticSchemaSetProvider(
            AbsolutePath.Parse(schemaRoot),
            packageVersion + "+source-revision");

        var schemaSet = provider.Load();

        Assert.Equal(
            packageVersion,
            schemaSet.Manifest.PackageVersion);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Load_WhenAssemblyInformationalVersionDiffersFromManifest_RejectsSet ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "installed-static-schema-set",
            "mismatched-version");
        var schemaRoot =
            StaticSchemaSetTestSupport.CopyRepositorySchemaSet(scope);
        var provider = new InstalledStaticSchemaSetProvider(
            AbsolutePath.Parse(schemaRoot),
            "999.0.0+source-revision");

        Assert.Throws<InvalidDataException>(
            () => provider.Load());
    }
}
