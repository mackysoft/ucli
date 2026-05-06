using System.Text.RegularExpressions;

namespace MackySoft.Ucli.Architecture.Tests.Architecture;

internal static class ConcreteTypeNameExtractor
{
    internal static IEnumerable<string> Read (string sourceFile)
    {
        var sourceText = CSharpSourceFileReader.ReadWithoutCommentsAndStringLiterals(sourceFile);
        return Regex
            .Matches(
                sourceText,
                @"\b(?:class|struct)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\b|\brecord\s+(?:class\s+|struct\s+)?(?<name>[A-Za-z_][A-Za-z0-9_]*)\b")
            .Select(static match => match.Groups["name"].Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value));
    }
}
