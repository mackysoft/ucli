using MackySoft.FileSystem;
using MackySoft.Ucli.Hosting.Cli.Schemas;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Schemas;

internal sealed class SchemaSetTestProvider : IInstalledStaticSchemaSetProvider
{
    private readonly string schemaRoot;

    public SchemaSetTestProvider (string schemaRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaRoot);
        this.schemaRoot = schemaRoot;
    }

    public UcliStaticSchemaSet Load ()
    {
        return UcliStaticSchemaSetLoader.Load(AbsolutePath.Parse(schemaRoot));
    }
}
