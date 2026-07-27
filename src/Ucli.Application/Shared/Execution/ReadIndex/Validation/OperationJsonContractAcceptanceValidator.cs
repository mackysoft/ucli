using System.Text.Json;
using Json.Schema;
using Json.Schema.Keywords;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Validates generated JSON contracts received through external operation metadata. </summary>
internal static class OperationJsonContractAcceptanceValidator
{
    private static readonly Uri OperationSchemaBaseUri = new("urn:ucli:index-operation-schema");

    /// <summary> Validates one generated operation contract before the operation metadata is accepted. </summary>
    /// <param name="contract"> The generated operation contract. </param>
    /// <param name="ownerName"> The operation owner rendered in diagnostics. </param>
    /// <param name="propertyName"> The contract property rendered in diagnostics. </param>
    /// <param name="error"> The validation error; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the contract is self-contained and internally consistent; otherwise <see langword="false" />. </returns>
    internal static bool TryValidate (
        UcliOperationJsonContract contract,
        string ownerName,
        string propertyName,
        out string? error)
    {
        if (!TryBuildSchema(contract, ownerName, propertyName, out var schema, out error))
        {
            return false;
        }

        var referenceFailure = GetSchemaReferenceFailure(schema!.Root);
        if (referenceFailure != null)
        {
            error = $"{ownerName} has {referenceFailure} in its {propertyName} schema.";
            return false;
        }

        if (!HasExpectedDigest(contract.Schema, "x-contract-digest", contract))
        {
            error = $"{ownerName} has a {propertyName} digest that does not match its schema.";
            return false;
        }

        if (!HasExpectedDigest(contract.TypeMetadata, "contractDigest", contract))
        {
            error = $"{ownerName} has a {propertyName} digest that does not match its type metadata.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryBuildSchema (
        UcliOperationJsonContract contract,
        string ownerName,
        string propertyName,
        out global::Json.Schema.JsonSchema? schema,
        out string? error)
    {
        try
        {
            schema = global::Json.Schema.JsonSchema.Build(
                contract.Schema.ToJsonElement(),
                CreateBuildOptions(),
                OperationSchemaBaseUri);
            error = null;
            return true;
        }
        catch (RefResolutionException exception)
        {
            schema = null;
            error = $"{ownerName} has an unresolved reference in its {propertyName} schema. {exception.Message}";
            return false;
        }
        catch (JsonSchemaException exception)
        {
            schema = null;
            error = $"{ownerName} has an invalid {propertyName} schema. {exception.Message}";
            return false;
        }
        catch (ArgumentException exception)
        {
            schema = null;
            error = $"{ownerName} has an invalid {propertyName} schema. {exception.Message}";
            return false;
        }
    }

    private static BuildOptions CreateBuildOptions ()
    {
        return new BuildOptions
        {
            Dialect = Dialect.Draft202012,
            SchemaRegistry = new SchemaRegistry
            {
                Fetch = null!,
            },
        };
    }

    private static bool HasExpectedDigest (
        UcliJsonObject document,
        string propertyName,
        UcliOperationJsonContract contract)
    {
        return document.TryGetProperty(propertyName, out var digest)
            && digest.ValueKind == JsonValueKind.String
            && string.Equals(
                digest.GetString(),
                contract.ContractDigest.ToString(),
                StringComparison.Ordinal);
    }

    private static string? GetSchemaReferenceFailure (JsonSchemaNode root)
    {
        var pending = new Stack<JsonSchemaNode>();
        var visited = new HashSet<JsonSchemaNode>(ReferenceEqualityComparer.Instance);
        pending.Push(root);
        while (pending.TryPop(out var node))
        {
            if (!visited.Add(node))
            {
                continue;
            }

            foreach (var keyword in node.Keywords)
            {
                var failure = GetSchemaReferenceFailure(keyword);
                if (failure != null)
                {
                    return failure;
                }

                foreach (var subschema in keyword.Subschemas)
                {
                    pending.Push(subschema);
                }
            }
        }

        return null;
    }

    private static string? GetSchemaReferenceFailure (KeywordData keyword)
    {
        if (keyword.Handler is DynamicRefKeyword)
        {
            return "an unsupported $dynamicRef";
        }

        if (keyword.Handler is not RefKeyword)
        {
            return null;
        }

        var reference = keyword.RawValue.GetString()!;
        if (!reference.StartsWith("#", StringComparison.Ordinal))
        {
            return $"an external $ref '{reference}'";
        }

        return !keyword.Subschemas.Any()
            ? $"an unresolved local $ref '{reference}'"
            : null;
    }
}
