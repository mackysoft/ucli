namespace MackySoft.Ucli.Architecture.Tests.Architecture;

internal static class CSharpSourceFileReader
{
    internal static string ReadWithoutCommentsAndStringLiterals (string sourceFile)
    {
        if (ArchitectureTestRepository.IsReparsePoint(sourceFile))
        {
            throw new InvalidOperationException($"C# source file must not be a reparse point: {sourceFile}");
        }

        return CSharpSourceScanner.StripCommentsAndStringLiterals(File.ReadAllText(sourceFile));
    }
}
