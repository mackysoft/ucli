namespace MackySoft.Ucli.Application.Features.ErrorCatalog.Catalog;

internal interface IErrorCodeCatalog
{
    IReadOnlyList<UcliErrorCodeDescriptor> Descriptors { get; }

    bool TryFind (
        UcliErrorCode code,
        out UcliErrorCodeDescriptor descriptor);
}
