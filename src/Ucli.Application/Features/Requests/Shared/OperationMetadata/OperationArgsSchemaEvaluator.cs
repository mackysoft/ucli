using System.Text.Json;
using Json.Schema;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Evaluates dynamic Operation arguments against their registered product contract. </summary>
internal static class OperationArgsSchemaEvaluator
{
    private static readonly Uri SchemaBaseUri = new("urn:ucli:operation-args-schema");

    public static ValidationResult? TryValidate (
        JsonElement args,
        string instancePath,
        UcliOperationDescriptor operationDescriptor,
        ICollection<ValidationError> errors)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instancePath);
        ArgumentNullException.ThrowIfNull(operationDescriptor);
        ArgumentNullException.ThrowIfNull(errors);

        var schemaFailure = TryBuildSchema(operationDescriptor, out var schema);
        if (schemaFailure != null)
        {
            return schemaFailure;
        }

        var evaluationFailure = TryEvaluate(
            schema!,
            args,
            operationDescriptor.Name,
            out var isValid);
        if (evaluationFailure != null)
        {
            return evaluationFailure;
        }

        if (!isValid)
        {
            errors.Add(CreateInvalidArgumentsError(operationDescriptor.Name, instancePath));
        }

        return null;
    }

    private static ValidationResult? TryBuildSchema (
        UcliOperationDescriptor descriptor,
        out global::Json.Schema.JsonSchema? schema)
    {
        var parseFailure = TryParseSchema(descriptor, out var document);
        if (parseFailure != null)
        {
            schema = null;
            return parseFailure;
        }

        using (var schemaDocument = document!)
        {
            if (schemaDocument.RootElement.ValueKind != JsonValueKind.Object)
            {
                schema = null;
                return CreateSchemaFailure(descriptor.Name, "must be a JSON object.");
            }

            return TryBuildParsedSchema(
                descriptor.Name,
                schemaDocument.RootElement.Clone(),
                out schema);
        }
    }

    private static ValidationResult? TryParseSchema (
        UcliOperationDescriptor descriptor,
        out JsonDocument? document)
    {
        try
        {
            document = JsonDocument.Parse(descriptor.ArgsSchemaJson);
            return null;
        }
        catch (JsonException exception)
        {
            return CreateSchemaFailure(
                descriptor.Name,
                $"is invalid JSON. {exception.Message}",
                out document);
        }
    }

    private static ValidationResult? TryBuildParsedSchema (
        string operationName,
        JsonElement document,
        out global::Json.Schema.JsonSchema? schema)
    {
        try
        {
            schema = global::Json.Schema.JsonSchema.Build(
                document,
                CreateBuildOptions(),
                SchemaBaseUri);
            return null;
        }
        catch (RefResolutionException exception)
        {
            return CreateSchemaFailure(
                operationName,
                $"contains an unresolved reference. {exception.Message}",
                out schema);
        }
        catch (JsonSchemaException exception)
        {
            return CreateSchemaFailure(
                operationName,
                $"is invalid. {exception.Message}",
                out schema);
        }
        catch (ArgumentException exception)
        {
            return CreateSchemaFailure(
                operationName,
                $"is invalid. {exception.Message}",
                out schema);
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

    private static ValidationResult? TryEvaluate (
        global::Json.Schema.JsonSchema schema,
        JsonElement args,
        string operationName,
        out bool isValid)
    {
        try
        {
            isValid = schema.Evaluate(
                args,
                new EvaluationOptions
                {
                    OutputFormat = OutputFormat.Flag,
                    RequireFormatValidation = false,
                }).IsValid;
            return null;
        }
        catch (RefResolutionException exception)
        {
            return CreateEvaluationFailure(
                operationName,
                $"contains an unresolved reference. {exception.Message}",
                out isValid);
        }
        catch (JsonSchemaException exception)
        {
            return CreateEvaluationFailure(
                operationName,
                $"is invalid. {exception.Message}",
                out isValid);
        }
    }

    private static ValidationResult CreateSchemaFailure (
        string operationName,
        string detail)
    {
        return ValidationResult.Failure(ExecutionError.InternalError(
            $"Static validation could not validate args for operation '{operationName}'. "
            + $"The registered operation args schema {detail}"));
    }

    private static ValidationResult CreateSchemaFailure<T> (
        string operationName,
        string detail,
        out T? value)
    {
        value = default;
        return CreateSchemaFailure(operationName, detail);
    }

    private static ValidationResult CreateEvaluationFailure (
        string operationName,
        string detail,
        out bool isValid)
    {
        isValid = false;
        return CreateSchemaFailure(operationName, detail);
    }

    private static ValidationError CreateInvalidArgumentsError (
        string operationName,
        string instancePath)
    {
        return new ValidationError(
            ValidationErrorCodes.OperationArgsInvalid,
            $"Arguments for operation '{operationName}' do not satisfy its JSON Schema.",
            instancePath);
    }
}
