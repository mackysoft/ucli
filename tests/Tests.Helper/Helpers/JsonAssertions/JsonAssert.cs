namespace MackySoft.Tests;

using System.Text;
using System.Text.Json;
using Xunit.Sdk;

internal static class JsonAssert
{
    public static JsonAssertion For (JsonElement root)
    {
        return new JsonAssertion(root);
    }
}

internal sealed class JsonAssertion
{
    private readonly JsonElement root;
    private readonly string currentPath;

    public JsonAssertion (JsonElement root)
        : this(root, "$")
    {
    }

    private JsonAssertion (JsonElement root, string currentPath)
    {
        this.root = root;
        this.currentPath = currentPath;
    }

    public JsonAssertion HasProperty (string propertyName)
    {
        ResolveOrThrow(propertyName);
        return this;
    }

    public JsonAssertion HasProperty (string propertyName, Action<JsonAssertion> assertion)
    {
        var (value, path) = ResolvePropertyNodeOrThrow(propertyName);
        ApplyNestedAssertion(assertion, value, path);
        return this;
    }

    public JsonAssertion HasProperty (string propertyName, int index, Action<JsonAssertion> assertion)
    {
        var (value, path) = ResolvePropertyNodeOrThrow(propertyName);
        new JsonAssertion(value, path).HasIndex(index, assertion);
        return this;
    }

    public JsonAssertion HasIndex (int index)
    {
        ResolveIndexOrThrow(index);
        return this;
    }

    public JsonAssertion HasIndex (int index, Action<JsonAssertion> assertion)
    {
        var (value, path) = ResolveIndexNodeOrThrow(index);
        ApplyNestedAssertion(assertion, value, path);
        return this;
    }

    public JsonAssertion HasProperties (params string[] propertyNames)
    {
        if (propertyNames is null || propertyNames.Length == 0)
        {
            throw new XunitException("At least one property path is required.");
        }

        foreach (var propertyName in propertyNames)
        {
            ResolveOrThrow(propertyName);
        }

        return this;
    }

    public JsonAssertion HasValueKind (string propertyName, JsonValueKind expect)
    {
        var value = ResolveOrThrow(propertyName);
        return HasValueKindValue(BuildDisplayPath(propertyName), value, expect);
    }

    public JsonAssertion HasValueKind (JsonValueKind expect)
    {
        return HasValueKindValue(currentPath, root, expect);
    }

    public JsonAssertion HasString (string propertyName, string? expect)
    {
        var value = ResolveOrThrow(propertyName);
        return HasStringValue(BuildDisplayPath(propertyName), value, expect);
    }

    public JsonAssertion HasString (string? expect)
    {
        return HasStringValue(currentPath, root, expect);
    }

    public JsonAssertion HasInt32 (string propertyName, int expect)
    {
        var value = ResolveOrThrow(propertyName);
        return HasInt32Value(BuildDisplayPath(propertyName), value, expect);
    }

    public JsonAssertion HasInt32 (int expect)
    {
        return HasInt32Value(currentPath, root, expect);
    }

    public JsonAssertion HasBoolean (string propertyName, bool expect)
    {
        var value = ResolveOrThrow(propertyName);
        return HasBooleanValue(BuildDisplayPath(propertyName), value, expect);
    }

    public JsonAssertion HasBoolean (bool expect)
    {
        return HasBooleanValue(currentPath, root, expect);
    }

    public JsonAssertion IsNull (string propertyName)
    {
        var value = ResolveOrThrow(propertyName);
        return HasNullValue(BuildDisplayPath(propertyName), value);
    }

    public JsonAssertion IsNull ()
    {
        return HasNullValue(currentPath, root);
    }

    public JsonAssertion HasArrayLength (string propertyName, int expect)
    {
        var value = ResolveOrThrow(propertyName);
        return HasArrayLengthValue(BuildDisplayPath(propertyName), value, expect);
    }

    public JsonAssertion HasArrayLength (int expect)
    {
        return HasArrayLengthValue(currentPath, root, expect);
    }

    private JsonAssertion HasValueKindValue (string sourcePath, JsonElement value, JsonValueKind expect)
    {
        if (value.ValueKind != expect)
        {
            throw new XunitException(
                $"JSON path '{sourcePath}' expected kind '{expect}' but was '{value.ValueKind}'.");
        }

        return this;
    }

    private JsonAssertion HasStringValue (string sourcePath, JsonElement value, string? expect)
    {
        if (expect is null)
        {
            if (value.ValueKind != JsonValueKind.Null)
            {
                throw new XunitException(
                    $"JSON path '{sourcePath}' expected null but was '{value.ValueKind}'.");
            }

            return this;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new XunitException(
                $"JSON path '{sourcePath}' expected string but was '{value.ValueKind}'.");
        }

        Assert.Equal(expect, value.GetString());
        return this;
    }

    private JsonAssertion HasInt32Value (string sourcePath, JsonElement value, int expect)
    {
        if (value.ValueKind != JsonValueKind.Number)
        {
            throw new XunitException(
                $"JSON path '{sourcePath}' expected number but was '{value.ValueKind}'.");
        }

        if (!value.TryGetInt32(out var actual))
        {
            throw new XunitException(
                $"JSON path '{sourcePath}' expected Int32-compatible number but was '{value.GetRawText()}'.");
        }

        Assert.Equal(expect, actual);
        return this;
    }

    private JsonAssertion HasBooleanValue (string sourcePath, JsonElement value, bool expect)
    {
        if (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
        {
            throw new XunitException(
                $"JSON path '{sourcePath}' expected boolean but was '{value.ValueKind}'.");
        }

        Assert.Equal(expect, value.GetBoolean());
        return this;
    }

    private JsonAssertion HasNullValue (string sourcePath, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Null)
        {
            throw new XunitException(
                $"JSON path '{sourcePath}' expected null but was '{value.ValueKind}'.");
        }

        return this;
    }

    private JsonAssertion HasArrayLengthValue (string sourcePath, JsonElement value, int expect)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new XunitException(
                $"JSON path '{sourcePath}' expected array but was '{value.ValueKind}'.");
        }

        Assert.Equal(expect, value.GetArrayLength());
        return this;
    }

    public JsonAssertion MatchesSchema (JsonSchemaNode schema, string? schemaName = null)
    {
        ArgumentNullException.ThrowIfNull(schema);

        var errors = new List<string>();
        ValidateAgainstSchema(
            element: root,
            schema: schema,
            path: currentPath,
            errors: errors);

        if (errors.Count > 0)
        {
            var builder = new StringBuilder();
            if (string.IsNullOrWhiteSpace(schemaName))
            {
                builder.Append("JSON schema validation failed.");
            }
            else
            {
                builder.Append($"JSON schema validation failed. schema={schemaName}");
            }

            foreach (var error in errors)
            {
                builder.AppendLine();
                builder.Append("- ");
                builder.Append(error);
            }

            throw new XunitException(builder.ToString());
        }

        return this;
    }

    private void ApplyNestedAssertion (
        Action<JsonAssertion> assertion,
        JsonElement value,
        string path)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        assertion(new JsonAssertion(value, path));
    }

    private (JsonElement Value, string Path) ResolvePropertyNodeOrThrow (string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            throw new XunitException("JSON path must not be null or whitespace.");
        }

        if (!TryResolvePath(root, propertyName, out var value, out var error))
        {
            throw new XunitException($"Failed to resolve JSON path '{BuildDisplayPath(propertyName)}': {error}");
        }

        return (value, BuildDisplayPath(propertyName));
    }

    private JsonElement ResolveOrThrow (string propertyName)
    {
        return ResolvePropertyNodeOrThrow(propertyName).Value;
    }

    private JsonElement ResolveIndexOrThrow (int index)
    {
        return ResolveIndexNodeOrThrow(index).Value;
    }

    private (JsonElement Value, string Path) ResolveIndexNodeOrThrow (int index)
    {
        if (index < 0)
        {
            throw new XunitException("Array index must be non-negative.");
        }

        if (root.ValueKind != JsonValueKind.Array)
        {
            throw new XunitException(
                $"JSON path '{currentPath}' expected array before index access but was '{root.ValueKind}'.");
        }

        var length = root.GetArrayLength();
        if (index >= length)
        {
            throw new XunitException(
                $"JSON path '{currentPath}' array index {index} is out of range. Length is {length}.");
        }

        return (root[index], $"{currentPath}[{index}]");
    }

    private string BuildDisplayPath (string propertyPath)
    {
        if (string.IsNullOrWhiteSpace(propertyPath))
        {
            return currentPath;
        }

        if (currentPath == "$")
        {
            return propertyPath[0] == '[' ? $"${propertyPath}" : $"$.{propertyPath}";
        }

        return propertyPath[0] == '[' ? $"{currentPath}{propertyPath}" : $"{currentPath}.{propertyPath}";
    }

    private static bool TryResolvePath (
        JsonElement root,
        string path,
        out JsonElement value,
        out string error)
    {
        value = root;
        error = string.Empty;
        var index = 0;

        while (index < path.Length)
        {
            if (path[index] == '.')
            {
                error = $"Unexpected '.' at index {index}.";
                return false;
            }

            if (path[index] == '[')
            {
                if (!TryApplyArrayIndex(ref value, path, ref index, out error))
                {
                    return false;
                }
            }
            else
            {
                if (!TryReadPropertyName(path, ref index, out var propertyName, out error))
                {
                    return false;
                }

                if (value.ValueKind != JsonValueKind.Object)
                {
                    error = $"Expected object before property '{propertyName}' but was '{value.ValueKind}'.";
                    return false;
                }

                if (!value.TryGetProperty(propertyName, out var child))
                {
                    error = $"Property '{propertyName}' was not found.";
                    return false;
                }

                value = child;

                while (index < path.Length && path[index] == '[')
                {
                    if (!TryApplyArrayIndex(ref value, path, ref index, out error))
                    {
                        return false;
                    }
                }
            }

            if (index < path.Length)
            {
                if (path[index] != '.')
                {
                    error = $"Expected '.' at index {index} but found '{path[index]}'.";
                    return false;
                }

                index++;
                if (index == path.Length)
                {
                    error = "Path must not end with '.'.";
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TryReadPropertyName (
        string path,
        ref int index,
        out string propertyName,
        out string error)
    {
        var start = index;
        while (index < path.Length && path[index] != '.' && path[index] != '[')
        {
            index++;
        }

        if (start == index)
        {
            propertyName = string.Empty;
            error = $"Expected property name at index {index}.";
            return false;
        }

        propertyName = path[start..index];
        error = string.Empty;
        return true;
    }

    private static bool TryApplyArrayIndex (
        ref JsonElement value,
        string path,
        ref int index,
        out string error)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            error = $"Expected array before index access but was '{value.ValueKind}'.";
            return false;
        }

        index++;
        var start = index;

        while (index < path.Length && char.IsDigit(path[index]))
        {
            index++;
        }

        if (start == index)
        {
            error = $"Array index was not specified at index {start}.";
            return false;
        }

        if (index >= path.Length || path[index] != ']')
        {
            error = $"Expected ']' after array index at index {index}.";
            return false;
        }

        if (!int.TryParse(path[start..index], out var arrayIndex))
        {
            error = $"Array index '{path[start..index]}' is not a valid Int32.";
            return false;
        }

        var length = value.GetArrayLength();
        if (arrayIndex < 0 || arrayIndex >= length)
        {
            error = $"Array index {arrayIndex} is out of range. Length is {length}.";
            return false;
        }

        value = value[arrayIndex];
        index++;
        error = string.Empty;
        return true;
    }

    private static void ValidateAgainstSchema (
        JsonElement element,
        JsonSchemaNode schema,
        string path,
        List<string> errors)
    {
        if (schema.Types is null || schema.Types.Count == 0)
        {
            errors.Add($"path '{path}' has an invalid schema definition: no allowed types.");
            return;
        }

        if (!MatchesAnyType(element, schema.Types))
        {
            errors.Add(
                $"path '{path}' expected {FormatExpectedTypes(schema.Types)} but was {DescribeActualType(element)}.");
            return;
        }

        if (HasType(schema.Types, JsonSchemaType.Object) && element.ValueKind == JsonValueKind.Object)
        {
            ValidateObject(element, schema, path, errors);
        }

        if (HasType(schema.Types, JsonSchemaType.Array) && element.ValueKind == JsonValueKind.Array)
        {
            ValidateArray(element, schema, path, errors);
        }
    }

    private static void ValidateObject (
        JsonElement element,
        JsonSchemaNode schema,
        string path,
        List<string> errors)
    {
        var schemaProperties = schema.Properties ?? new Dictionary<string, JsonSchemaProperty>();

        foreach (var (propertyName, propertySchema) in schemaProperties)
        {
            if (!element.TryGetProperty(propertyName, out var propertyValue))
            {
                if (propertySchema.Required)
                {
                    errors.Add($"path '{BuildPropertyPath(path, propertyName)}' is missing.");
                }

                continue;
            }

            ValidateAgainstSchema(
                element: propertyValue,
                schema: propertySchema.Schema,
                path: BuildPropertyPath(path, propertyName),
                errors: errors);
        }

        if (!schema.AllowAdditionalProperties)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (!schemaProperties.ContainsKey(property.Name))
                {
                    errors.Add(
                        $"path '{BuildPropertyPath(path, property.Name)}' is not allowed by schema.");
                }
            }
        }
    }

    private static void ValidateArray (
        JsonElement element,
        JsonSchemaNode schema,
        string path,
        List<string> errors)
    {
        if (schema.ItemSchema is null)
        {
            errors.Add($"path '{path}' has an invalid schema definition: item schema is missing.");
            return;
        }

        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            ValidateAgainstSchema(
                element: item,
                schema: schema.ItemSchema,
                path: $"{path}[{index}]",
                errors: errors);
            index++;
        }
    }

    private static bool MatchesAnyType (JsonElement element, IReadOnlyList<JsonSchemaType> allowedTypes)
    {
        foreach (var allowedType in allowedTypes)
        {
            if (MatchesType(element, allowedType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesType (JsonElement element, JsonSchemaType allowedType)
    {
        return allowedType switch
        {
            JsonSchemaType.Object => element.ValueKind == JsonValueKind.Object,
            JsonSchemaType.Array => element.ValueKind == JsonValueKind.Array,
            JsonSchemaType.String => element.ValueKind == JsonValueKind.String,
            JsonSchemaType.Int32 => element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out _),
            JsonSchemaType.Boolean => element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False,
            JsonSchemaType.Null => element.ValueKind == JsonValueKind.Null,
            _ => false,
        };
    }

    private static bool HasType (IReadOnlyList<JsonSchemaType> allowedTypes, JsonSchemaType type)
    {
        foreach (var allowedType in allowedTypes)
        {
            if (allowedType == type)
            {
                return true;
            }
        }

        return false;
    }

    private static string FormatExpectedTypes (IReadOnlyList<JsonSchemaType> allowedTypes)
    {
        return "one of [" + string.Join(", ", allowedTypes.Select(static x => x.ToString())) + "]";
    }

    private static string DescribeActualType (JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && !element.TryGetInt32(out _))
        {
            return "Number(non-Int32)";
        }

        return element.ValueKind.ToString();
    }

    private static string BuildPropertyPath (string parentPath, string propertyName)
    {
        return $"{parentPath}.{propertyName}";
    }
}