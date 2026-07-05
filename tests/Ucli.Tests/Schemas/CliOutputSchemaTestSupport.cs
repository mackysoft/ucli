using MackySoft.Tests;

namespace MackySoft.Ucli.Tests.Schemas;

internal static class CliOutputSchemaTestSupport
{
    public static string SchemaRoot { get; } = TestRepositoryPaths.GetFullPath("schemas", "v1");

    private static readonly Lazy<JsonSchemaArtifactSet> LazySchemaSet = new(() => JsonSchemaArtifactSet.Load(SchemaRoot));

    public static JsonSchemaArtifactSet SchemaSet => LazySchemaSet.Value;
}
