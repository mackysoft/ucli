namespace MackySoft.Tests;

using System.Text.Json;

internal sealed class JsonSchemaArtifactSet : IDisposable
{
    private const string SchemaBaseId = "https://schemas.mackysoft.dev/ucli/v1/";

    private static readonly HashSet<string> SupportedKeywords = new(StringComparer.Ordinal)
    {
        "$schema",
        "$id",
        "$defs",
        "$ref",
        "type",
        "required",
        "properties",
        "items",
        "additionalProperties",
        "enum",
        "const",
        "oneOf",
    };

    private readonly Dictionary<string, JsonDocument> documentsByPath;

    private readonly Dictionary<string, string> pathsById;

    private readonly Dictionary<string, string> payloadSchemaPathsByCommand;

    private bool disposed;

    private JsonSchemaArtifactSet (
        Dictionary<string, JsonDocument> documentsByPath,
        Dictionary<string, string> pathsById,
        Dictionary<string, string> payloadSchemaPathsByCommand)
    {
        this.documentsByPath = documentsByPath;
        this.pathsById = pathsById;
        this.payloadSchemaPathsByCommand = payloadSchemaPathsByCommand;
    }

    public static JsonSchemaArtifactSet Load (string versionRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionRoot);

        var fullVersionRoot = Path.GetFullPath(versionRoot);
        var manifestPath = Path.Combine(fullVersionRoot, "schema-manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new InvalidOperationException($"Schema manifest was not found: {manifestPath}");
        }

        var documentsByPath = new Dictionary<string, JsonDocument>(StringComparer.Ordinal);
        var pathsById = new Dictionary<string, string>(StringComparer.Ordinal);
        var schemaDefinitionErrors = new List<string>();
        foreach (var schemaPath in Directory.EnumerateFiles(fullVersionRoot, "*.schema.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var relativePath = NormalizeRelativeSchemaPath(Path.GetRelativePath(fullVersionRoot, schemaPath));
            var document = JsonDocument.Parse(File.ReadAllText(schemaPath));
            documentsByPath.Add(relativePath, document);
            ValidateSchemaDefinition(document.RootElement, relativePath, "$", schemaDefinitionErrors);
            if (document.RootElement.TryGetProperty("$id", out var idElement)
                && idElement.ValueKind == JsonValueKind.String)
            {
                pathsById.Add(idElement.GetString()!, relativePath);
            }
        }

        if (schemaDefinitionErrors.Count > 0)
        {
            foreach (var document in documentsByPath.Values)
            {
                document.Dispose();
            }

            throw new InvalidOperationException(
                $"Schema artifact set contains unsupported schema definitions:{Environment.NewLine}{string.Join(Environment.NewLine, schemaDefinitionErrors)}");
        }

        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var payloadSchemaPathsByCommand = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var schemaEntry in manifest.RootElement.GetProperty("schemas").EnumerateArray())
        {
            if (!schemaEntry.TryGetProperty("command", out var commandElement))
            {
                continue;
            }

            var command = commandElement.GetString()
                ?? throw new InvalidOperationException("Schema manifest command entry must be a string.");
            var path = schemaEntry.GetProperty("path").GetString()
                ?? throw new InvalidOperationException("Schema manifest path entry must be a string.");
            payloadSchemaPathsByCommand.Add(command, NormalizeRelativeSchemaPath(path));
        }

        return new JsonSchemaArtifactSet(
            documentsByPath,
            pathsById,
            payloadSchemaPathsByCommand);
    }

    public string? FindPayloadSchemaPath (string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        return payloadSchemaPathsByCommand.TryGetValue(command, out var schemaPath) ? schemaPath : null;
    }

    public IReadOnlyList<string> Validate (
        string schemaPath,
        JsonElement element,
        string path = "$")
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var errors = new List<string>();
        if (!TryNormalizeRelativeSchemaPath(schemaPath, out var normalizedSchemaPath, out var normalizationError))
        {
            errors.Add(normalizationError!);
            return errors;
        }

        if (!documentsByPath.TryGetValue(normalizedSchemaPath, out var document))
        {
            errors.Add($"schema '{normalizedSchemaPath}' was not found.");
            return errors;
        }

        ValidateAgainstSchema(
            element,
            document.RootElement,
            normalizedSchemaPath,
            path,
            errors);
        return errors;
    }

    public void Dispose ()
    {
        if (disposed)
        {
            return;
        }

        foreach (var document in documentsByPath.Values)
        {
            document.Dispose();
        }

        disposed = true;
    }

    private void ValidateAgainstSchema (
        JsonElement element,
        JsonElement schema,
        string schemaPath,
        string path,
        List<string> errors)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"schema '{schemaPath}' for path '{path}' must be an object.");
            return;
        }

        ValidateSupportedKeywords(schema, schemaPath, path, errors);

        if (schema.TryGetProperty("$ref", out var referenceElement))
        {
            ValidateReference(element, referenceElement, schemaPath, path, errors);
            return;
        }

        if (schema.TryGetProperty("oneOf", out var oneOfElement))
        {
            ValidateOneOf(element, oneOfElement, schemaPath, path, errors);
            return;
        }

        if (schema.TryGetProperty("type", out var typeElement)
            && !MatchesAllowedTypes(element, typeElement, out var expectedTypes))
        {
            errors.Add($"path '{path}' expected one of [{expectedTypes}] but was {DescribeActualType(element)}.");
            return;
        }

        if (schema.TryGetProperty("const", out var constElement)
            && !JsonLiteralEquals(element, constElement))
        {
            errors.Add($"path '{path}' did not match schema const.");
            return;
        }

        if (schema.TryGetProperty("enum", out var enumElement)
            && !MatchesEnum(element, enumElement))
        {
            errors.Add($"path '{path}' did not match any schema enum value.");
            return;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            ValidateObject(element, schema, schemaPath, path, errors);
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            ValidateArray(element, schema, schemaPath, path, errors);
        }
    }

    private void ValidateReference (
        JsonElement element,
        JsonElement referenceElement,
        string schemaPath,
        string path,
        List<string> errors)
    {
        if (referenceElement.ValueKind != JsonValueKind.String)
        {
            errors.Add($"schema '{schemaPath}' for path '{path}' has a non-string $ref.");
            return;
        }

        var reference = referenceElement.GetString();
        if (string.IsNullOrWhiteSpace(reference))
        {
            errors.Add($"schema '{schemaPath}' for path '{path}' has an empty $ref.");
            return;
        }

        if (!TryResolveReference(schemaPath, reference!, out var referencedSchema, out var referencedSchemaPath, out var error))
        {
            errors.Add(error ?? $"schema '{schemaPath}' for path '{path}' has an unresolved $ref.");
            return;
        }

        ValidateAgainstSchema(element, referencedSchema, referencedSchemaPath, path, errors);
    }

    private void ValidateOneOf (
        JsonElement element,
        JsonElement oneOfElement,
        string schemaPath,
        string path,
        List<string> errors)
    {
        if (oneOfElement.ValueKind != JsonValueKind.Array)
        {
            errors.Add($"schema '{schemaPath}' for path '{path}' has a non-array oneOf.");
            return;
        }

        var matchCount = 0;
        foreach (var branchSchema in oneOfElement.EnumerateArray())
        {
            var branchErrors = new List<string>();
            ValidateAgainstSchema(element, branchSchema, schemaPath, path, branchErrors);
            if (branchErrors.Count == 0)
            {
                matchCount++;
            }
        }

        if (matchCount != 1)
        {
            errors.Add($"path '{path}' matched {matchCount} oneOf branches in schema '{schemaPath}'.");
        }
    }

    private void ValidateObject (
        JsonElement element,
        JsonElement schema,
        string schemaPath,
        string path,
        List<string> errors)
    {
        if (schema.TryGetProperty("required", out var requiredElement))
        {
            if (requiredElement.ValueKind != JsonValueKind.Array)
            {
                errors.Add($"schema '{schemaPath}' for path '{path}' has a non-array required.");
            }
            else
            {
                foreach (var requiredProperty in requiredElement.EnumerateArray())
                {
                    if (requiredProperty.ValueKind != JsonValueKind.String)
                    {
                        errors.Add($"schema '{schemaPath}' for path '{path}' has a non-string required item.");
                        continue;
                    }

                    var propertyName = requiredProperty.GetString()!;
                    if (!element.TryGetProperty(propertyName, out _))
                    {
                        errors.Add($"path '{BuildPropertyPath(path, propertyName)}' is missing.");
                    }
                }
            }
        }

        var propertySchemas = default(JsonElement);
        var hasPropertySchemas = false;
        if (schema.TryGetProperty("properties", out var propertiesElement))
        {
            if (propertiesElement.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"schema '{schemaPath}' for path '{path}' has non-object properties.");
            }
            else
            {
                propertySchemas = propertiesElement;
                hasPropertySchemas = true;
                foreach (var propertySchema in propertiesElement.EnumerateObject())
                {
                    if (!element.TryGetProperty(propertySchema.Name, out var propertyValue))
                    {
                        continue;
                    }

                    ValidateAgainstSchema(
                        propertyValue,
                        propertySchema.Value,
                        schemaPath,
                        BuildPropertyPath(path, propertySchema.Name),
                        errors);
                }
            }
        }

        if (schema.TryGetProperty("additionalProperties", out var additionalPropertiesElement)
            && additionalPropertiesElement.ValueKind == JsonValueKind.False)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (!hasPropertySchemas || !propertySchemas.TryGetProperty(property.Name, out _))
                {
                    errors.Add($"path '{BuildPropertyPath(path, property.Name)}' is not allowed by schema.");
                }
            }
        }
    }

    private void ValidateArray (
        JsonElement element,
        JsonElement schema,
        string schemaPath,
        string path,
        List<string> errors)
    {
        if (!schema.TryGetProperty("items", out var itemSchema))
        {
            return;
        }

        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            ValidateAgainstSchema(
                item,
                itemSchema,
                schemaPath,
                $"{path}[{index}]",
                errors);
            index++;
        }
    }

    private bool TryResolveReference (
        string currentSchemaPath,
        string reference,
        out JsonElement referencedSchema,
        out string referencedSchemaPath,
        out string? error)
    {
        referencedSchema = default;
        referencedSchemaPath = currentSchemaPath;
        error = null;

        var hashIndex = reference.IndexOf('#', StringComparison.Ordinal);
        var referencePath = hashIndex >= 0 ? reference[..hashIndex] : reference;
        var pointer = hashIndex >= 0 ? reference[(hashIndex + 1)..] : string.Empty;

        if (reference.StartsWith("#", StringComparison.Ordinal))
        {
            referencePath = currentSchemaPath;
            pointer = reference[1..];
        }
        else if (reference.StartsWith(SchemaBaseId, StringComparison.Ordinal))
        {
            var id = hashIndex >= 0 ? reference[..hashIndex] : reference;
            if (!pathsById.TryGetValue(id, out referencePath!))
            {
                error = $"schema id '{id}' referenced by '{currentSchemaPath}' was not found.";
                return false;
            }
        }
        else
        {
            var relativeReferencePath = Path.Join(
                Path.GetDirectoryName(currentSchemaPath) ?? string.Empty,
                referencePath);
            if (!TryNormalizeRelativeSchemaPath(relativeReferencePath, out referencePath, out error))
            {
                return false;
            }
        }

        if (!TryNormalizeRelativeSchemaPath(referencePath, out referencePath, out error))
        {
            return false;
        }

        if (!documentsByPath.TryGetValue(referencePath, out var document))
        {
            error = $"schema reference '{reference}' from '{currentSchemaPath}' resolved to missing schema '{referencePath}'.";
            return false;
        }

        referencedSchemaPath = referencePath;
        referencedSchema = document.RootElement;
        if (string.IsNullOrWhiteSpace(pointer))
        {
            return true;
        }

        return TryResolveJsonPointer(document.RootElement, pointer, out referencedSchema, out error);
    }

    private static bool TryResolveJsonPointer (
        JsonElement root,
        string pointer,
        out JsonElement element,
        out string? error)
    {
        element = root;
        error = null;
        if (!pointer.StartsWith("/", StringComparison.Ordinal))
        {
            error = $"Only JSON pointer $ref fragments are supported. Actual: #{pointer}";
            return false;
        }

        foreach (var rawSegment in pointer.Split('/').Skip(1))
        {
            var segment = rawSegment
                .Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(segment, out element))
            {
                error = $"JSON pointer segment '{segment}' was not found in schema reference '#{pointer}'.";
                return false;
            }
        }

        return true;
    }

    private static void ValidateSupportedKeywords (
        JsonElement schema,
        string schemaPath,
        string path,
        List<string> errors)
    {
        foreach (var property in schema.EnumerateObject())
        {
            if (!SupportedKeywords.Contains(property.Name))
            {
                errors.Add($"schema '{schemaPath}' for path '{path}' uses unsupported keyword '{property.Name}'.");
            }
        }
    }

    private static void ValidateSchemaDefinition (
        JsonElement schema,
        string schemaPath,
        string path,
        List<string> errors)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"schema '{schemaPath}' for path '{path}' must be an object.");
            return;
        }

        ValidateSupportedKeywords(schema, schemaPath, path, errors);

        if (schema.TryGetProperty("$defs", out var defsElement))
        {
            if (defsElement.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"schema '{schemaPath}' for path '{path}' has non-object $defs.");
            }
            else
            {
                foreach (var definition in defsElement.EnumerateObject())
                {
                    ValidateSchemaDefinition(
                        definition.Value,
                        schemaPath,
                        BuildPropertyPath(BuildPropertyPath(path, "$defs"), definition.Name),
                        errors);
                }
            }
        }

        if (schema.TryGetProperty("properties", out var propertiesElement))
        {
            if (propertiesElement.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"schema '{schemaPath}' for path '{path}' has non-object properties.");
            }
            else
            {
                foreach (var property in propertiesElement.EnumerateObject())
                {
                    ValidateSchemaDefinition(
                        property.Value,
                        schemaPath,
                        BuildPropertyPath(BuildPropertyPath(path, "properties"), property.Name),
                        errors);
                }
            }
        }

        if (schema.TryGetProperty("additionalProperties", out var additionalPropertiesElement)
            && additionalPropertiesElement.ValueKind != JsonValueKind.False)
        {
            errors.Add($"schema '{schemaPath}' for path '{path}' has unsupported additionalProperties value.");
        }

        if (schema.TryGetProperty("items", out var itemsElement))
        {
            ValidateSchemaDefinition(itemsElement, schemaPath, BuildPropertyPath(path, "items"), errors);
        }

        if (schema.TryGetProperty("oneOf", out var oneOfElement))
        {
            if (oneOfElement.ValueKind != JsonValueKind.Array)
            {
                errors.Add($"schema '{schemaPath}' for path '{path}' has a non-array oneOf.");
            }
            else
            {
                var index = 0;
                foreach (var branch in oneOfElement.EnumerateArray())
                {
                    ValidateSchemaDefinition(branch, schemaPath, $"{BuildPropertyPath(path, "oneOf")}[{index}]", errors);
                    index++;
                }
            }
        }
    }

    private static bool MatchesAllowedTypes (
        JsonElement element,
        JsonElement typeElement,
        out string expectedTypes)
    {
        if (typeElement.ValueKind == JsonValueKind.String)
        {
            expectedTypes = typeElement.GetString() ?? string.Empty;
            return MatchesType(element, expectedTypes);
        }

        if (typeElement.ValueKind != JsonValueKind.Array)
        {
            expectedTypes = "<invalid>";
            return false;
        }

        var types = new List<string>();
        foreach (var item in typeElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var type = item.GetString() ?? string.Empty;
            types.Add(type);
            if (MatchesType(element, type))
            {
                expectedTypes = string.Join(", ", types);
                return true;
            }
        }

        expectedTypes = string.Join(", ", types);
        return false;
    }

    private static bool MatchesType (
        JsonElement element,
        string type)
    {
        return type switch
        {
            "object" => element.ValueKind == JsonValueKind.Object,
            "array" => element.ValueKind == JsonValueKind.Array,
            "string" => element.ValueKind == JsonValueKind.String,
            "integer" => element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out _),
            "number" => element.ValueKind == JsonValueKind.Number,
            "boolean" => element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False,
            "null" => element.ValueKind == JsonValueKind.Null,
            _ => false,
        };
    }

    private static bool MatchesEnum (
        JsonElement element,
        JsonElement enumElement)
    {
        if (enumElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var enumValue in enumElement.EnumerateArray())
        {
            if (JsonLiteralEquals(element, enumValue))
            {
                return true;
            }
        }

        return false;
    }

    private static bool JsonLiteralEquals (
        JsonElement left,
        JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        return left.ValueKind switch
        {
            JsonValueKind.String => string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal),
            JsonValueKind.Number => left.GetRawText() == right.GetRawText(),
            JsonValueKind.True => true,
            JsonValueKind.False => true,
            JsonValueKind.Null => true,
            _ => left.GetRawText() == right.GetRawText(),
        };
    }

    private static string DescribeActualType (JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && !element.TryGetInt64(out _))
        {
            return "Number(non-integer)";
        }

        return element.ValueKind.ToString();
    }

    private static string BuildPropertyPath (
        string parentPath,
        string propertyName)
    {
        return $"{parentPath}.{propertyName}";
    }

    private static string NormalizeRelativeSchemaPath (string path)
    {
        if (!TryNormalizeRelativeSchemaPath(path, out var normalizedPath, out var error))
        {
            throw new InvalidOperationException(error);
        }

        return normalizedPath;
    }

    private static bool TryNormalizeRelativeSchemaPath (
        string path,
        out string normalizedPath,
        out string? error)
    {
        normalizedPath = string.Empty;
        error = null;

        if (Path.IsPathRooted(path))
        {
            error = $"schema path '{path}' must be relative.";
            return false;
        }

        var normalized = path.Replace('\\', '/');
        var segments = new Stack<string>();
        foreach (var segment in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(segment, ".", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(segment, "..", StringComparison.Ordinal))
            {
                if (segments.Count == 0)
                {
                    error = $"schema path '{path}' escapes the schema root.";
                    return false;
                }

                segments.Pop();
                continue;
            }

            segments.Push(segment);
        }

        normalizedPath = string.Join("/", segments.Reverse());
        return true;
    }

    private void ThrowIfDisposed ()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
