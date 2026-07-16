using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StaticCodeCatalog : ICodeCatalog
{
    private readonly IReadOnlyDictionary<UcliCode, CodeCatalogDescriptor> descriptorsByCode;

    public StaticCodeCatalog (IEnumerable<CodeCatalogDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        Descriptors = descriptors
            .OrderBy(static descriptor => descriptor.Code.Value, StringComparer.Ordinal)
            .ToArray();
        descriptorsByCode = Descriptors.ToDictionary(
            static descriptor => descriptor.Code,
            static descriptor => descriptor);
    }

    public IReadOnlyList<CodeCatalogDescriptor> Descriptors { get; }

    public bool TryFind (
        UcliCode code,
        out CodeCatalogDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(code);
        return descriptorsByCode.TryGetValue(code, out descriptor!);
    }
}
