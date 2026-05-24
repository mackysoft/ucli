namespace MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve.Contracts;

/// <summary> Represents a selector that resolves an existing Unity global object ID. </summary>
internal sealed record ResolveGlobalObjectIdSelectorInput (string GlobalObjectId) : ResolveSelectorInput;
