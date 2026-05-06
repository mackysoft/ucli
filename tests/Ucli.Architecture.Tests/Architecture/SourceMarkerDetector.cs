namespace MackySoft.Ucli.Architecture.Tests.Architecture;

internal static class SourceMarkerDetector
{
    internal static string[] FindMarkersInCode (IEnumerable<string> sourceFiles, IReadOnlyCollection<string> forbiddenMarkers)
    {
        var violations = new List<string>();
        foreach (var sourceFile in sourceFiles)
        {
            var sourceText = ArchitectureTestRepository.ReadCSharpSourceWithoutCommentsAndStringLiterals(sourceFile);
            AddMarkerViolations(violations, sourceFile, sourceText, forbiddenMarkers);
        }

        return violations.ToArray();
    }

    internal static string[] FindRawMarkers (IEnumerable<string> sourceFiles, IReadOnlyCollection<string> forbiddenMarkers)
    {
        var violations = new List<string>();
        foreach (var sourceFile in sourceFiles)
        {
            var sourceText = File.ReadAllText(sourceFile);
            AddMarkerViolations(violations, sourceFile, sourceText, forbiddenMarkers);
        }

        return violations.ToArray();
    }

    internal static bool ContainsQualifiedNameWithSegment (string sourceText, string prefix, string requiredSegment)
    {
        var searchIndex = 0;
        while (true)
        {
            var markerIndex = sourceText.IndexOf(prefix, searchIndex, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                return false;
            }

            var markerEndIndex = markerIndex + prefix.Length;
            while (markerEndIndex < sourceText.Length && IsQualifiedNameCharacter(sourceText[markerEndIndex]))
            {
                markerEndIndex++;
            }

            if (sourceText[markerIndex..markerEndIndex].Contains(requiredSegment, StringComparison.Ordinal))
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
        foreach (var marker in forbiddenMarkers)
        {
            if (sourceText.Contains(marker, StringComparison.Ordinal))
            {
                violations.Add($"{ArchitectureTestRepository.NormalizeRepositoryRelativePath(sourceFile)} contains {marker}.");
            }
        }
    }

    private static bool IsQualifiedNameCharacter (char value)
    {
        return value == '.' || value == '_' || char.IsAsciiLetterOrDigit(value);
    }
}
