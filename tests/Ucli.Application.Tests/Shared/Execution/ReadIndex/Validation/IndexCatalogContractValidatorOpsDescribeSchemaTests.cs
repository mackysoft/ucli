using System.Text.Json;
using System.Text.Json.Nodes;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex;

public sealed class IndexCatalogContractValidatorOpsDescribeSchemaTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryCreateOpsDescribeSnapshot_ReturnsFalse_WhenArgsContractIsMissing ()
    {
        var contract = IndexCatalogContractValidatorOpsTestSupport.CreateOpsDescribe(
            IndexCatalogContractValidatorOpsTestSupport.WithDescriptorDigest(
                new IndexOpEntryJsonContract(
                    Name: "ucli.scene.open",
                    Kind: UcliOperationKind.Command,
                    Policy: OperationPolicy.Safe,
                    ArgsContract: null,
                    DescriptorDigest: null,
                    VerdictContract: null,
                    ResultContract: null,
                    Exposure: null,
                    PlayModeSupport: UcliOperationPlayModeSupport.Disallowed)));

        var result = OpsDescribeSnapshot.TryCreate(contract, out var snapshot);

        Assert.False(result);
        Assert.Null(snapshot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreateOpsDescribeSnapshot_ReturnsTrue_WhenProviderGeneratedContractsAreComplete ()
    {
        var operation = IndexCatalogContractValidatorOpsTestSupport.CreateValidOpsEntry();
        var contract = IndexCatalogContractValidatorOpsTestSupport.CreateOpsDescribe(
            operation);

        var result = OpsDescribeSnapshot.TryCreate(contract, out var snapshot);

        Assert.True(result);
        Assert.NotNull(snapshot);
        var expectedArgsContract = Assert.IsType<UcliOperationJsonContract>(operation.ArgsContract);
        var expectedResultContract = Assert.IsType<UcliOperationJsonContract>(operation.ResultContract);
        var actualResultContract = Assert.IsType<UcliOperationJsonContract>(
            snapshot.Operation.ResultContract);
        Assert.Equal(expectedArgsContract.ContractDigest, snapshot.Operation.ArgsContract.ContractDigest);
        Assert.Equal(expectedResultContract.ContractDigest, actualResultContract.ContractDigest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreateOpsDescribeSnapshot_ReturnsFalse_WhenContractDigestDiffersFromSchema ()
    {
        var operation = IndexCatalogContractValidatorOpsTestSupport.CreateValidOpsEntry();
        var argsContract = operation.ArgsContract!.Value;
        operation = operation with
        {
            ArgsContract = argsContract with
            {
                Schema = new UcliJsonObject(ReplaceRootStringProperty(
                    argsContract.Schema.ToJsonElement(),
                    "x-contract-digest",
                    new string('f', 64))),
            },
        };
        operation = IndexCatalogContractValidatorOpsTestSupport.WithDescriptorDigest(operation);
        var contract = IndexCatalogContractValidatorOpsTestSupport.CreateOpsDescribe(operation);

        var result = OpsDescribeSnapshot.TryCreate(contract, out var snapshot);

        Assert.False(result);
        Assert.Null(snapshot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreateOpsDescribeSnapshot_ReturnsFalse_WhenContractDigestDiffersFromTypeMetadata ()
    {
        var operation = IndexCatalogContractValidatorOpsTestSupport.CreateValidOpsEntry();
        var argsContract = operation.ArgsContract!.Value;
        operation = operation with
        {
            ArgsContract = argsContract with
            {
                TypeMetadata = new UcliJsonObject(ReplaceRootStringProperty(
                    argsContract.TypeMetadata.ToJsonElement(),
                    "contractDigest",
                    new string('f', 64))),
            },
        };
        operation = IndexCatalogContractValidatorOpsTestSupport.WithDescriptorDigest(operation);
        var contract = IndexCatalogContractValidatorOpsTestSupport.CreateOpsDescribe(operation);

        var result = OpsDescribeSnapshot.TryCreate(contract, out var snapshot);

        Assert.False(result);
        Assert.Null(snapshot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreateOpsDescribeSnapshot_ReturnsFalse_WhenSchemaReferenceRequiresExternalFetch ()
    {
        var operation = IndexCatalogContractValidatorOpsTestSupport.CreateValidOpsEntry();
        var argsContract = operation.ArgsContract!.Value;
        operation = operation with
        {
            ArgsContract = argsContract with
            {
                Schema = new UcliJsonObject(ReplaceFirstReference(
                    argsContract.Schema.ToJsonElement(),
                    "https://example.invalid/target.schema.json")),
            },
        };
        operation = IndexCatalogContractValidatorOpsTestSupport.WithDescriptorDigest(operation);
        var contract = IndexCatalogContractValidatorOpsTestSupport.CreateOpsDescribe(operation);

        var result = OpsDescribeSnapshot.TryCreate(contract, out var snapshot);

        Assert.False(result);
        Assert.Null(snapshot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreateOpsDescribeSnapshot_ReturnsFalse_WhenSchemaContainsUnresolvedLocalReference ()
    {
        var operation = IndexCatalogContractValidatorOpsTestSupport.CreateValidOpsEntry();
        var argsContract = operation.ArgsContract!.Value;
        operation = operation with
        {
            ArgsContract = argsContract with
            {
                Schema = new UcliJsonObject(ReplaceFirstReference(
                    argsContract.Schema.ToJsonElement(),
                    "#/$defs/not-found")),
            },
        };
        operation = IndexCatalogContractValidatorOpsTestSupport.WithDescriptorDigest(operation);
        var contract = IndexCatalogContractValidatorOpsTestSupport.CreateOpsDescribe(operation);

        var result = OpsDescribeSnapshot.TryCreate(contract, out var snapshot);

        Assert.False(result);
        Assert.Null(snapshot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreateOpsDescribeSnapshot_ReturnsFalse_WhenProviderSchemaTypeIsInvalid ()
    {
        var operation = IndexCatalogContractValidatorOpsTestSupport.CreateValidOpsEntry();
        var generationResult = UcliOperationJsonContractGenerator.Generate(
            UcliPrimitiveOperationNames.Resolve,
            IpcJsonSerializerOptions.PublicRawOperationContracts.GetTypeInfo(typeof(ResolveSelectorArgs)),
            IpcJsonSerializerOptions.PublicRawOperationContracts.GetTypeInfo(typeof(IpcResolveOperationResult)));
        var resultContract = generationResult.ResultContract!.Value;
        operation = operation with
        {
            ResultContract = resultContract with
            {
                Schema = new UcliJsonObject(ReplaceFirstStringProperty(
                    resultContract.Schema.ToJsonElement(),
                    "type",
                    "not-a-json-schema-type")),
            },
        };
        operation = IndexCatalogContractValidatorOpsTestSupport.WithDescriptorDigest(operation);
        var contract = IndexCatalogContractValidatorOpsTestSupport.CreateOpsDescribe(operation);

        var result = OpsDescribeSnapshot.TryCreate(contract, out var snapshot);

        Assert.False(result);
        Assert.Null(snapshot);
    }

    private static JsonElement ReplaceRootStringProperty (
        JsonElement source,
        string propertyName,
        string value)
    {
        var root = JsonNode.Parse(source.GetRawText())!.AsObject();
        root[propertyName] = value;
        return JsonSerializer.SerializeToElement(root);
    }

    private static JsonElement ReplaceFirstReference (
        JsonElement source,
        string reference)
    {
        return ReplaceFirstStringProperty(source, "$ref", reference);
    }

    private static JsonElement ReplaceFirstStringProperty (
        JsonElement source,
        string propertyName,
        string value)
    {
        var root = JsonNode.Parse(source.GetRawText())
            ?? throw new InvalidOperationException("Provider schema fixture must be a JSON value.");
        if (!TryReplaceFirstStringProperty(root, propertyName, value))
        {
            throw new InvalidOperationException(
                $"Provider schema fixture must contain a '{propertyName}' property.");
        }

        return JsonSerializer.SerializeToElement(root);
    }

    private static bool TryReplaceFirstStringProperty (
        JsonNode node,
        string propertyName,
        string value)
    {
        if (node is JsonObject jsonObject)
        {
            if (jsonObject.ContainsKey(propertyName))
            {
                jsonObject[propertyName] = value;
                return true;
            }

            foreach (var property in jsonObject)
            {
                if (property.Value != null
                    && TryReplaceFirstStringProperty(
                        property.Value,
                        propertyName,
                        value))
                {
                    return true;
                }
            }

            return false;
        }

        if (node is JsonArray jsonArray)
        {
            foreach (var item in jsonArray)
            {
                if (item != null
                    && TryReplaceFirstStringProperty(
                        item,
                        propertyName,
                        value))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
