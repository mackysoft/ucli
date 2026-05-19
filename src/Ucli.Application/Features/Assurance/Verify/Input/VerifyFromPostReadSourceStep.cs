namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Input;

/// <summary> Represents normalized source facts for one <c>verify --from</c> operation result. </summary>
internal sealed record VerifyFromPostReadSourceStep (
    string OpId,
    string SourceKind,
    bool PlayModeMutation,
    string? Commit,
    bool PersistenceExpected,
    string ExpectedPostState);
