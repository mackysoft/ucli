namespace MackySoft.Ucli.Architecture.Tests.Architecture;

internal readonly record struct PublicSurfaceDeclaration (
    string RelativePath,
    int LineNumber,
    string Namespace,
    string Signature);
