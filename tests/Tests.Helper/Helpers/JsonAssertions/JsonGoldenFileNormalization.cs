namespace MackySoft.Tests;

using System.Text.Encodings.Web;
using System.Text.Json;
using Xunit.Sdk;

internal sealed class JsonGoldenFileNormalization
{
    private static readonly JsonSerializerOptions ReplacementJsonSerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly List<JsonGoldenPathPrefixRule> pathPrefixRules = [];
    private readonly List<JsonGoldenStringPropertyRule> stringPropertyRules = [];

    public static JsonGoldenFileNormalization Create ()
    {
        return new JsonGoldenFileNormalization();
    }

    public JsonGoldenFileNormalization NormalizeRequestIds (string token = "<requestId>")
    {
        return NormalizeStringProperty(
            "requestId",
            token,
            static value => Guid.TryParseExact(value, "D", out _),
            "a GUID in D format");
    }

    public JsonGoldenFileNormalization NormalizeStringProperty (
        string propertyName,
        string token,
        Func<string, bool>? validate = null,
        string validationDescription = "a valid string")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentException.ThrowIfNullOrWhiteSpace(validationDescription);

        stringPropertyRules.Add(new JsonGoldenStringPropertyRule(
            propertyName,
            token,
            validate,
            validationDescription));
        return this;
    }

    public JsonGoldenFileNormalization NormalizePathPrefix (
        string absolutePathPrefix,
        string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePathPrefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        pathPrefixRules.Add(new JsonGoldenPathPrefixRule(
            CreatePathPrefixCandidates(absolutePathPrefix),
            token));
        return this;
    }

    internal string Apply (
        JsonElement root,
        string jsonText)
    {
        ArgumentNullException.ThrowIfNull(jsonText);

        var replacements = new List<JsonGoldenTextReplacement>();
        foreach (var rule in stringPropertyRules)
        {
            rule.Collect(root, replacements);
        }

        foreach (var rule in pathPrefixRules)
        {
            rule.Collect(root, replacements);
        }

        var normalizedText = jsonText;
        foreach (var replacement in replacements.Distinct())
        {
            normalizedText = normalizedText.Replace(
                replacement.Source,
                replacement.Replacement,
                StringComparison.Ordinal);
        }

        return normalizedText;
    }

    private static string ToJsonStringLiteral (string value)
    {
        return JsonSerializer.Serialize(value, ReplacementJsonSerializerOptions);
    }

    private static IReadOnlyList<string> CreatePathPrefixCandidates (string absolutePathPrefix)
    {
        var candidates = new HashSet<string>(StringComparer.Ordinal)
        {
            TrimTrailingDirectorySeparators(Path.GetFullPath(absolutePathPrefix)),
        };

        var resolvedPath = TryResolveExistingPath(absolutePathPrefix);
        if (!string.IsNullOrWhiteSpace(resolvedPath))
        {
            candidates.Add(TrimTrailingDirectorySeparators(resolvedPath));
        }

        return candidates
            .OrderByDescending(static candidate => candidate.Length)
            .ToArray();
    }

    private static string TrimTrailingDirectorySeparators (string path)
    {
        var root = Path.GetPathRoot(path);
        var rootLength = root?.Length ?? 0;
        while (path.Length > rootLength && path.EndsWith(Path.DirectorySeparatorChar))
        {
            path = path[..^1];
        }

        return path;
    }

    private static string? TryResolveExistingPath (string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrWhiteSpace(root))
            {
                return null;
            }

            var currentPath = root;
            var relativePath = fullPath[root.Length..];
            var segments = relativePath.Split(
                Path.DirectorySeparatorChar,
                StringSplitOptions.RemoveEmptyEntries);
            foreach (var segment in segments)
            {
                currentPath = Path.Combine(currentPath, segment);
                var fileSystemInfo = GetExistingFileSystemInfo(currentPath);
                if (fileSystemInfo?.LinkTarget is null)
                {
                    continue;
                }

                var linkTarget = fileSystemInfo.ResolveLinkTarget(returnFinalTarget: true);
                if (linkTarget != null)
                {
                    currentPath = linkTarget.FullName;
                }
            }

            return currentPath;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static FileSystemInfo? GetExistingFileSystemInfo (string path)
    {
        if (Directory.Exists(path))
        {
            return new DirectoryInfo(path);
        }

        return File.Exists(path)
            ? new FileInfo(path)
            : null;
    }

    private readonly record struct JsonGoldenTextReplacement (
        string Source,
        string Replacement);

    private sealed record JsonGoldenStringPropertyRule (
        string PropertyName,
        string Token,
        Func<string, bool>? Validate,
        string ValidationDescription)
    {
        public void Collect (
            JsonElement element,
            List<JsonGoldenTextReplacement> replacements)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        if (string.Equals(property.Name, PropertyName, StringComparison.Ordinal))
                        {
                            CollectPropertyValue(property.Value, replacements);
                        }

                        Collect(property.Value, replacements);
                    }

                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        Collect(item, replacements);
                    }

                    break;
            }
        }

        private void CollectPropertyValue (
            JsonElement value,
            List<JsonGoldenTextReplacement> replacements)
        {
            if (value.ValueKind != JsonValueKind.String)
            {
                throw new XunitException($"JSON property '{PropertyName}' must be a string to be normalized.");
            }

            var text = value.GetString();
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new XunitException($"JSON property '{PropertyName}' must not be empty.");
            }

            if (Validate != null && !Validate(text))
            {
                throw new XunitException($"JSON property '{PropertyName}' must be {ValidationDescription}. Actual: {text}");
            }

            replacements.Add(new JsonGoldenTextReplacement(
                ToJsonStringLiteral(text),
                ToJsonStringLiteral(Token)));
        }
    }

    private sealed record JsonGoldenPathPrefixRule (
        IReadOnlyList<string> AbsolutePathPrefixes,
        string Token)
    {
        public void Collect (
            JsonElement element,
            List<JsonGoldenTextReplacement> replacements)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        Collect(property.Value, replacements);
                    }

                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        Collect(item, replacements);
                    }

                    break;
                case JsonValueKind.String:
                    CollectStringValue(element.GetString(), replacements);
                    break;
            }
        }

        private void CollectStringValue (
            string? value,
            List<JsonGoldenTextReplacement> replacements)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (!TryCreateReplacementValue(value, out var replacementValue))
            {
                return;
            }

            replacements.Add(new JsonGoldenTextReplacement(
                ToJsonStringLiteral(value),
                ToJsonStringLiteral(replacementValue)));
        }

        private bool TryCreateReplacementValue (
            string value,
            out string replacementValue)
        {
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            foreach (var absolutePathPrefix in AbsolutePathPrefixes)
            {
                if (TryCreateReplacementValue(
                    value,
                    absolutePathPrefix,
                    comparison,
                    out replacementValue))
                {
                    return true;
                }
            }

            replacementValue = string.Empty;
            return false;
        }

        private bool TryCreateReplacementValue (
            string value,
            string absolutePathPrefix,
            StringComparison comparison,
            out string replacementValue)
        {
            if (string.Equals(value, absolutePathPrefix, comparison))
            {
                replacementValue = Token;
                return true;
            }

            var prefixWithSeparator = absolutePathPrefix.EndsWith(Path.DirectorySeparatorChar)
                ? absolutePathPrefix
                : absolutePathPrefix + Path.DirectorySeparatorChar;
            var prefixIndex = value.IndexOf(prefixWithSeparator, comparison);
            if (prefixIndex < 0)
            {
                replacementValue = string.Empty;
                return false;
            }

            if (!IsPathPrefixBoundary(value, prefixIndex))
            {
                replacementValue = string.Empty;
                return false;
            }

            var prefixText = value[..prefixIndex];
            var relativeValue = value[(prefixIndex + prefixWithSeparator.Length)..]
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
            replacementValue = $"{prefixText}{Token}/{relativeValue}";
            return true;
        }

        private static bool IsPathPrefixBoundary (
            string value,
            int prefixIndex)
        {
            if (prefixIndex == 0)
            {
                return true;
            }

            var previousChar = value[prefixIndex - 1];
            return char.IsWhiteSpace(previousChar) || previousChar is ':' or '"' or '\'' or '(' or '[' or '{';
        }
    }
}
