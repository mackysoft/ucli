namespace MackySoft.Tests;

using System.Text.Json;
using Xunit.Sdk;

internal static class JsonPathResolver
{
    public static JsonAssertionContext ResolvePropertyOrThrow (JsonAssertionContext context, string propertyPath)
    {
        if (string.IsNullOrWhiteSpace(propertyPath))
        {
            throw new XunitException("JSON path must not be null or whitespace.");
        }

        var displayPath = BuildDisplayPath(context.Path, propertyPath);
        if (!TryResolvePath(context.Value, propertyPath, out var value, out var error))
        {
            throw new XunitException($"Failed to resolve JSON path '{displayPath}': {error}");
        }

        return new JsonAssertionContext(value, displayPath);
    }

    public static JsonAssertionContext ResolveIndexOrThrow (JsonAssertionContext context, int index)
    {
        if (index < 0)
        {
            throw new XunitException("Array index must be non-negative.");
        }

        if (context.Value.ValueKind != JsonValueKind.Array)
        {
            throw new XunitException(
                $"JSON path '{context.Path}' expected array before index access but was '{context.Value.ValueKind}'.");
        }

        var length = context.Value.GetArrayLength();
        if (index >= length)
        {
            throw new XunitException(
                $"JSON path '{context.Path}' array index {index} is out of range. Length is {length}.");
        }

        return new JsonAssertionContext(context.Value[index], $"{context.Path}[{index}]");
    }

    private static string BuildDisplayPath (string currentPath, string propertyPath)
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
}
