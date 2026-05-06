using System.Text;

namespace MackySoft.Ucli.Architecture.Tests.Architecture;

internal static class SourceMarkerDetector
{
    internal static string[] FindMarkersInCode (IEnumerable<string> sourceFiles, IReadOnlyCollection<string> forbiddenMarkers)
    {
        var violations = new List<string>();
        foreach (var sourceFile in sourceFiles)
        {
            var sourceText = CSharpSourceFileReader.ReadWithoutCommentsAndStringLiterals(sourceFile);
            AddMarkerViolations(violations, sourceFile, sourceText, forbiddenMarkers);
        }

        return violations.ToArray();
    }

    internal static string[] FindMarkersOutsideComments (IEnumerable<string> sourceFiles, IReadOnlyCollection<string> forbiddenMarkers)
    {
        var violations = new List<string>();
        foreach (var sourceFile in sourceFiles)
        {
            if (ArchitectureTestRepository.IsReparsePoint(sourceFile))
            {
                throw new InvalidOperationException($"C# source file must not be a reparse point: {sourceFile}");
            }

            var sourceText = CSharpSourceScanner.StripComments(File.ReadAllText(sourceFile));
            AddMarkerViolations(violations, sourceFile, sourceText, forbiddenMarkers);
        }

        return violations.ToArray();
    }

    internal static bool ContainsQualifiedNameWithSegment (string sourceText, string prefix, string requiredSegment)
    {
        var normalizedSourceText = NormalizeReferenceTrivia(sourceText);
        var normalizedPrefix = NormalizeReferenceTrivia(prefix);
        var normalizedRequiredSegment = NormalizeReferenceTrivia(requiredSegment);
        var searchIndex = 0;
        while (true)
        {
            var markerIndex = normalizedSourceText.IndexOf(normalizedPrefix, searchIndex, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                return false;
            }

            var markerEndIndex = markerIndex + normalizedPrefix.Length;
            while (markerEndIndex < normalizedSourceText.Length && IsQualifiedNameCharacter(normalizedSourceText[markerEndIndex]))
            {
                markerEndIndex++;
            }

            if (normalizedSourceText[markerIndex..markerEndIndex].Contains(normalizedRequiredSegment, StringComparison.Ordinal))
            {
                return true;
            }

            searchIndex = markerEndIndex;
        }
    }

    private static void AddMarkerViolations (
        List<string> violations,
        string sourceFile,
        string sourceText,
        IReadOnlyCollection<string> forbiddenMarkers)
    {
        var normalizedSourceText = NormalizeReferenceTrivia(sourceText);
        foreach (var marker in forbiddenMarkers)
        {
            var normalizedMarker = NormalizeReferenceTrivia(marker);
            if (normalizedSourceText.Contains(normalizedMarker, StringComparison.Ordinal))
            {
                violations.Add($"{ArchitectureTestRepository.NormalizeRepositoryRelativePath(sourceFile)} contains {marker}.");
            }
        }
    }

    private static string NormalizeReferenceTrivia (string value)
    {
        var builder = new StringBuilder(value.Length);
        var hasPendingWhitespace = false;
        foreach (var current in value)
        {
            if (char.IsWhiteSpace(current))
            {
                hasPendingWhitespace = true;
                continue;
            }

            if (hasPendingWhitespace)
            {
                AppendNormalizedWhitespace(builder, current);
                hasPendingWhitespace = false;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    private static void AppendNormalizedWhitespace (StringBuilder builder, char next)
    {
        if (builder.Length > 0 && builder[^1] != '.' && next != '.' && next != '(')
        {
            builder.Append(' ');
        }
    }

    private static bool IsQualifiedNameCharacter (char value)
    {
        return value == '.' || value == '_' || char.IsAsciiLetterOrDigit(value);
    }
}
