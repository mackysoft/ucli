using System.Text.RegularExpressions;

namespace MackySoft.Ucli.Architecture.Tests.Architecture;

internal static class InternalsVisibleToAssemblyReader
{
    private static readonly Regex InternalsVisibleToAttributePattern = new(
        @"(?:System\.Runtime\.CompilerServices\.)?InternalsVisibleTo(?:Attribute)?\s*\(\s*(?:""(?<assemblyName>[^""]+)""|@""(?<verbatimAssemblyName>(?:[^""]|"""")*)"")",
        RegexOptions.CultureInvariant);

    private static readonly Regex InternalsVisibleToAttributeInvocationPattern = new(
        @"(?:System\.Runtime\.CompilerServices\.)?InternalsVisibleTo(?:Attribute)?\s*\(",
        RegexOptions.CultureInvariant);

    internal static string[] ReadAssemblyNames (string assemblyInfoPath)
    {
        var sourceText = File.ReadAllText(ArchitectureTestRepository.ToRegularFileFullPath(assemblyInfoPath));
        return ReadAssemblyNamesFromSource(sourceText);
    }

    internal static string[] ReadAssemblyNamesFromSource (string sourceText)
    {
        var commentlessSource = CSharpSourceScanner.StripComments(sourceText);
        var assemblyNames = InternalsVisibleToAttributePattern
            .Matches(commentlessSource)
            .Select(ReadAssemblyName)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        if (assemblyNames.Length != InternalsVisibleToAttributeInvocationPattern.Matches(commentlessSource).Count)
        {
            throw new InvalidOperationException("InternalsVisibleTo declarations must use a string literal assembly name.");
        }

        return assemblyNames;
    }

    private static string ReadAssemblyName (Match match)
    {
        var assemblyName = match.Groups["assemblyName"].Value;
        if (!string.IsNullOrWhiteSpace(assemblyName))
        {
            return assemblyName;
        }

        return match.Groups["verbatimAssemblyName"].Value.Replace("\"\"", "\"", StringComparison.Ordinal);
    }
}
