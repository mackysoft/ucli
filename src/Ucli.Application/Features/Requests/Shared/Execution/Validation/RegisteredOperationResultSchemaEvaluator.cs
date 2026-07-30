using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Json.Schema;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Validation;

/// <summary> Evaluates a registered Operation result value against its product-owned result schema. </summary>
internal static class RegisteredOperationResultSchemaEvaluator
{
    private static readonly Uri ResultSchemaBaseUri = new("urn:ucli:operation-result-schema");

    /// <summary> Evaluates one result and maps schema failures to the Operation response contract diagnostic. </summary>
    public static bool TryEvaluate (
        string operationName,
        string resultSchemaJson,
        JsonElement result,
        [NotNullWhen(false)]
        out string? errorMessage)
    {
        try
        {
            if (!Evaluate(resultSchemaJson, result))
            {
                errorMessage = $"Execute response payload is invalid. The result from operation '{operationName}' does not satisfy its registered result schema.";
                return false;
            }
        }
        catch (Exception exception) when (exception is
            JsonException
            or RefResolutionException
            or JsonSchemaException
            or ArgumentException)
        {
            errorMessage = $"Execute response payload is invalid. The registered result schema for operation '{operationName}' could not be evaluated. {exception.Message}";
            return false;
        }

        errorMessage = null;
        return true;
    }

    private static bool Evaluate (
        string resultSchemaJson,
        JsonElement result)
    {
        using var schemaDocument = JsonDocument.Parse(resultSchemaJson);
        var schema = global::Json.Schema.JsonSchema.Build(
            schemaDocument.RootElement,
            CreateBuildOptions(),
            ResultSchemaBaseUri);
        return schema.Evaluate(
            result,
            new EvaluationOptions
            {
                OutputFormat = OutputFormat.Flag,
                RequireFormatValidation = false,
            }).IsValid;
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
}
