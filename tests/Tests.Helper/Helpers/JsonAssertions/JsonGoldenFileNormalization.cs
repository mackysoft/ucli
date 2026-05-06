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
        Collect(root, replacements);

        var normalizedText = jsonText;
        foreach (var replacementGroup in replacements.GroupBy(static replacement => replacement))
        {
            var replacement = replacementGroup.Key;
            var sourceCandidates = ToSourceJsonStringLiterals(replacement.SourceValue);
            var replacementLiteral = ToReplacementJsonStringLiteral(replacement.ReplacementValue);
            var expectedOccurrenceCount = replacementGroup.Count();
            var actualOccurrenceCount = sourceCandidates.Sum(source => CountOccurrences(normalizedText, source));
            if (actualOccurrenceCount != expectedOccurrenceCount)
            {
                throw new XunitException(
                    $"JSON string literal selected for normalization appears {actualOccurrenceCount} time(s), but {expectedOccurrenceCount} selected node(s) require normalization: {replacement.SourceValue}");
            }

            foreach (var source in sourceCandidates)
            {
                normalizedText = normalizedText.Replace(
                    source,
                    replacementLiteral,
                    StringComparison.Ordinal);
            }
        }

        return normalizedText;
    }

    private void Collect (
        JsonElement element,
        List<JsonGoldenTextReplacement> replacements)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    foreach (var rule in stringPropertyRules)
                    {
                        rule.CollectPropertyValue(property, replacements);
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
            case JsonValueKind.String:
                foreach (var rule in pathPrefixRules)
                {
                    rule.CollectStringValue(element.GetString(), replacements);
                }

                break;
        }
    }

    private static int CountOccurrences (
        string text,
        string value)
    {
        var count = 0;
        var startIndex = 0;
        while (true)
        {
            var index = text.IndexOf(value, startIndex, StringComparison.Ordinal);
            if (index < 0)
            {
                return count;
            }

            count++;
            startIndex = index + value.Length;
        }
    }

    private static string[] ToSourceJsonStringLiterals (string value)
    {
        var defaultLiteral = JsonSerializer.Serialize(value);
        var relaxedLiteral = JsonSerializer.Serialize(value, ReplacementJsonSerializerOptions);
        return string.Equals(defaultLiteral, relaxedLiteral, StringComparison.Ordinal)
            ? [defaultLiteral]
            : [defaultLiteral, relaxedLiteral];
    }

    private static string ToReplacementJsonStringLiteral (string value)
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
        string SourceValue,
        string ReplacementValue);

    private sealed record JsonGoldenStringPropertyRule (
        string PropertyName,
        string Token,
        Func<string, bool>? Validate,
        string ValidationDescription)
    {
        public void CollectPropertyValue (
            JsonProperty property,
            List<JsonGoldenTextReplacement> replacements)
        {
            if (!string.Equals(property.Name, PropertyName, StringComparison.Ordinal))
            {
                return;
            }

            if (property.Value.ValueKind != JsonValueKind.String)
            {
                throw new XunitException($"JSON property '{PropertyName}' must be a string to be normalized.");
            }

            var text = property.Value.GetString();
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new XunitException($"JSON property '{PropertyName}' must not be empty.");
            }

            if (Validate != null && !Validate(text))
            {
                throw new XunitException($"JSON property '{PropertyName}' must be {ValidationDescription}. Actual: {text}");
            }

            replacements.Add(new JsonGoldenTextReplacement(
                text,
                Token));
        }
    }

    private sealed record JsonGoldenPathPrefixRule (
        IReadOnlyList<string> AbsolutePathPrefixes,
        string Token)
    {
        public void CollectStringValue (
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
                value,
                replacementValue));
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
