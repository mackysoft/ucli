using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StaticCodeCatalog : ICodeCatalog
{
    private readonly IReadOnlyDictionary<string, CodeCatalogDescriptor> descriptorsByCode;

    public StaticCodeCatalog (IEnumerable<CodeCatalogDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        Descriptors = descriptors
            .OrderBy(static descriptor => descriptor.Code.Value, StringComparer.Ordinal)
            .ToArray();
        descriptorsByCode = Descriptors.ToDictionary(
            static descriptor => descriptor.Code.Value,
            static descriptor => descriptor,
            StringComparer.Ordinal);
    }

    public IReadOnlyList<CodeCatalogDescriptor> Descriptors { get; }

    public bool TryFind (
        UcliCode code,
        out CodeCatalogDescriptor descriptor)
    {
        if (code.IsValid && descriptorsByCode.TryGetValue(code.Value, out descriptor!))
        {
            return true;
        }

        descriptor = null!;
        return false;
    }
}
