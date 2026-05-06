namespace MackySoft.Ucli.Architecture.Tests.Architecture;

internal static class ApplicationUseCaseNamespaceReferenceDetector
{
    internal static bool ContainsReference (string sourceText)
    {
        const string prefix = "MackySoft.Ucli.Application.Features.";
        return SourceMarkerDetector.ContainsQualifiedNameWithSegment(sourceText, prefix, ".UseCases.");
    }
}
