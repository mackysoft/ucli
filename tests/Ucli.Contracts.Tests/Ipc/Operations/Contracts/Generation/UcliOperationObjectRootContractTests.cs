using System.Text.Json;
using Json.Schema;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

public sealed class UcliOperationObjectRootContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Generate_WithActualOperationDtos_ProducesSchemasThatRejectNullRoots ()
    {
        var serializerOptions = IpcJsonSerializerOptions.PublicRawOperationContracts;

        var result = UcliOperationJsonContractGenerator.Generate(
            "ucli.test.assets.find",
            serializerOptions.GetTypeInfo(typeof(AssetsFindArgs)),
            serializerOptions.GetTypeInfo(typeof(AssetsFindResult)));

        AssertRejectsNull(result.GetArgsJsonSchemaUtf8());
        AssertRejectsNull(Assert.IsType<byte[]>(result.GetResultJsonSchemaUtf8()));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(typeof(string))]
    [InlineData(typeof(int[]))]
    [InlineData(typeof(JsonElement))]
    public void Generate_WithNonObjectArgsContract_RejectsRegistration (Type argsType)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => UcliOperationJsonContractGenerator.Generate(
                "ucli.test.invalid.args",
                IpcJsonSerializerOptions.PublicRawOperationContracts.GetTypeInfo(argsType),
                resultTypeInfo: null));

        Assert.Equal("argsTypeInfo", exception.ParamName);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(typeof(string))]
    [InlineData(typeof(int[]))]
    public void Generate_WithNonObjectResultContract_RejectsRegistration (Type resultType)
    {
        var serializerOptions = IpcJsonSerializerOptions.PublicRawOperationContracts;
        var exception = Assert.Throws<ArgumentException>(
            () => UcliOperationJsonContractGenerator.Generate(
                "ucli.test.invalid.result",
                serializerOptions.GetTypeInfo(typeof(AssetsFindArgs)),
                serializerOptions.GetTypeInfo(resultType)));

        Assert.Equal("resultTypeInfo", exception.ParamName);
    }

    private static void AssertRejectsNull (
        byte[] schemaUtf8)
    {
        using var document = JsonDocument.Parse(schemaUtf8);
        var schema = global::Json.Schema.JsonSchema.Build(
            document.RootElement,
            new BuildOptions
            {
                Dialect = Dialect.Draft202012,
                SchemaRegistry = new SchemaRegistry
                {
                    Fetch = null!,
                },
            });

        Assert.False(
            schema.Evaluate(JsonSerializer.SerializeToElement<object?>(null)).IsValid);
    }
}
