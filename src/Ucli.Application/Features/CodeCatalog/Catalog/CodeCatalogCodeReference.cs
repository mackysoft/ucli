
namespace MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

/// <summary> Represents a code lookup reference parsed from <c>CODE</c> or <c>KIND:CODE</c>. </summary>
/// <param name="Code"> The validated code value. </param>
/// <param name="ExpectedKind"> The expected kind from a qualified lookup, or <see langword="null" /> for an unqualified lookup. </param>
internal sealed record CodeCatalogCodeReference
{
    public CodeCatalogCodeReference (
        UcliCode Code,
        CodeCatalogKind? ExpectedKind)
    {
        this.Code = Code ?? throw new ArgumentNullException(nameof(Code));
        if (ExpectedKind.HasValue && !TextVocabulary.IsDefined(ExpectedKind.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(ExpectedKind), ExpectedKind, "Expected code catalog kind must be specified.");
        }

        this.ExpectedKind = ExpectedKind;
    }

    public UcliCode Code { get; }

    public CodeCatalogKind? ExpectedKind { get; }
}
