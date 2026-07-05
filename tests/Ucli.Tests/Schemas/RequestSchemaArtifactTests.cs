using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Schemas;

public sealed class RequestSchemaArtifactTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void RequestSchemasAndPrimitiveOperationNames_DoNotExposePlayModeLifecycleOperations ()
    {
        var requestEnvelopeSchemaPath = TestRepositoryPaths.GetFullPath("schemas", "v1", "request", "request-envelope.schema.json");
        var editDslSchemaPath = TestRepositoryPaths.GetFullPath("schemas", "v1", "request", "edit-dsl.schema.json");
        var requestSchemas = File.ReadAllText(requestEnvelopeSchemaPath) + File.ReadAllText(editDslSchemaPath);
        var primitiveOperationNames = StaticFieldValueReader.ReadFromStaticClasses<string>(
            typeof(UcliPrimitiveOperationNames).Assembly,
            "PrimitiveOperationNames");

        Assert.DoesNotContain("play.enter", requestSchemas, StringComparison.Ordinal);
        Assert.DoesNotContain("play.exit", requestSchemas, StringComparison.Ordinal);
        Assert.DoesNotContain("ucli.play.enter", requestSchemas, StringComparison.Ordinal);
        Assert.DoesNotContain("ucli.play.exit", requestSchemas, StringComparison.Ordinal);
        Assert.DoesNotContain("ucli.play.enter", primitiveOperationNames);
        Assert.DoesNotContain("ucli.play.exit", primitiveOperationNames);
    }
}
