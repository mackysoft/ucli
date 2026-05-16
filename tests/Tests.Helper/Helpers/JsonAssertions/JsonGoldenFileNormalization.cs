namespace MackySoft.Tests;

using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Xunit.Sdk;

internal sealed class JsonGoldenFileNormalization
{
    private static readonly string[] TimestampFormats =
    [
        "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz",
    ];

    private static readonly JsonSerializerOptions ReplacementJsonSerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly List<JsonGoldenPathPrefixRule> pathPrefixRules = [];
    private readonly List<JsonGoldenStringPropertyRule> stringPropertyRules = [];

    public JsonGoldenFileNormalization NormalizeGuidProperty (
        string propertyName,
        string token)
    {
        return NormalizeStringProperty(
            propertyName,
            token,
            static value => Guid.TryParseExact(value, "D", out _),
            "a GUID in D format");
    }

    public JsonGoldenFileNormalization NormalizeTimestampProperty (
        string propertyName,
        string token = "<timestamp>",
        Func<DateTimeOffset, bool>? validateTimestamp = null,
        string validationDescription = "an ISO-8601 timestamp")
    {
        return NormalizeStringProperty(
            propertyName,
            token,
            value => TryValidateTimestamp(value, validateTimestamp),
            validationDescription);
    }

    public JsonGoldenFileNormalization NormalizeStringPropertyValue (
        string propertyName,
        string token,
        Func<string, bool>? validate = null,
        string validationDescription = "a valid string")
    {
        return NormalizeStringProperty(
            propertyName,
            token,
            validate,
            validationDescription);
    }

    private JsonGoldenFileNormalization NormalizeStringProperty (
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
                    $"JSON string literal selected for normalization appears {actualOccurrenceCount} time(s), but {expectedOccurrenceCount} selected node(s) require normalization. Token: {replacement.ReplacementValue}; source length: {replacement.SourceValue.Length}.");
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

    private static bool TryValidateTimestamp (
        string value,
        Func<DateTimeOffset, bool>? validateTimestamp)
    {
        if (!DateTimeOffset.TryParseExact(
            value,
            TimestampFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var timestamp))
        {
            return false;
        }

        return validateTimestamp?.Invoke(timestamp) ?? true;
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
        while (path.Length > rootLength && IsDirectorySeparator(path[^1]))
        {
            path = path[..^1];
        }

        return path;
    }

    private static bool IsDirectorySeparator (char value)
    {
        return value is '/' or '\\';
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

            StringBuilder? builder = null;
            var pendingAppendStartIndex = 0;
            var currentIndex = 0;
            while (currentIndex < value.Length)
            {
                if (!TryFindPathPrefixMatch(
                    value,
                    currentIndex,
                    comparison,
                    out var prefixLength))
                {
                    currentIndex++;
                    continue;
                }

                builder ??= new StringBuilder(value.Length);
                AppendPendingText(
                    builder,
                    value,
                    pendingAppendStartIndex,
                    currentIndex,
                    normalizeDirectorySeparators: pendingAppendStartIndex > 0);
                builder.Append(Token);

                currentIndex += prefixLength;
                pendingAppendStartIndex = currentIndex;
            }

            if (builder is null)
            {
                replacementValue = string.Empty;
                return false;
            }

            AppendPendingText(
                builder,
                value,
                pendingAppendStartIndex,
                value.Length,
                normalizeDirectorySeparators: true);
            replacementValue = builder.ToString();
            return true;
        }

        private bool TryFindPathPrefixMatch (
            string value,
            int startIndex,
            StringComparison comparison,
            out int prefixLength)
        {
            prefixLength = 0;
            if (!IsPathPrefixBoundary(value, startIndex))
            {
                return false;
            }

            foreach (var absolutePathPrefix in AbsolutePathPrefixes)
            {
                if (MatchesPathPrefix(
                    value,
                    startIndex,
                    absolutePathPrefix,
                    comparison))
                {
                    prefixLength = absolutePathPrefix.Length;
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesPathPrefix (
            string value,
            int startIndex,
            string absolutePathPrefix,
            StringComparison comparison)
        {
            if (startIndex + absolutePathPrefix.Length > value.Length)
            {
                return false;
            }

            for (var offset = 0; offset < absolutePathPrefix.Length; offset++)
            {
                var valueChar = value[startIndex + offset];
                var prefixChar = absolutePathPrefix[offset];
                if (IsDirectorySeparator(valueChar) && IsDirectorySeparator(prefixChar))
                {
                    continue;
                }

                if (string.Compare(
                    value,
                    startIndex + offset,
                    absolutePathPrefix,
                    offset,
                    length: 1,
                    comparison) != 0)
                {
                    return false;
                }
            }

            var endIndex = startIndex + absolutePathPrefix.Length;
            return endIndex == value.Length || IsDirectorySeparator(value[endIndex]) || IsPathBoundaryChar(value[endIndex]);
        }

        private static void AppendPendingText (
            StringBuilder builder,
            string value,
            int startIndex,
            int endIndex,
            bool normalizeDirectorySeparators)
        {
            for (var i = startIndex; i < endIndex; i++)
            {
                builder.Append(normalizeDirectorySeparators
                    ? NormalizeDirectorySeparator(value[i])
                    : value[i]);
            }
        }

        private static char NormalizeDirectorySeparator (char value)
        {
            return value == '\\'
                ? '/'
                : value;
        }

        private static bool IsPathBoundaryChar (char value)
        {
            return char.IsWhiteSpace(value) || value is ':' or '=' or '"' or '\'' or '(' or '[' or '{' or ')' or ']' or '}' or ',' or ';';
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
            return IsPathBoundaryChar(previousChar);
        }
    }
}
