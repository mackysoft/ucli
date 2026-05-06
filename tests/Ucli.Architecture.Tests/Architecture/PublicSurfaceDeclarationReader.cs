namespace MackySoft.Ucli.Architecture.Tests.Architecture;

internal static class PublicSurfaceDeclarationReader
{
    internal static IEnumerable<PublicSurfaceDeclaration> Read (string sourceFile)
    {
        var sourceText = CSharpSourceFileReader.ReadWithoutCommentsAndStringLiterals(sourceFile);
        var relativePath = ArchitectureTestRepository.NormalizeRepositoryRelativePath(sourceFile);
        return PublicSurfaceDeclarationExtractor.Read(sourceText, relativePath);
    }
}
