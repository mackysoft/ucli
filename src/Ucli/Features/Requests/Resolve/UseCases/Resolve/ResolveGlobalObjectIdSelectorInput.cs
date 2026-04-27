namespace MackySoft.Ucli.Features.Requests.Resolve.UseCases.Resolve;

/// <summary> Represents a selector that resolves an existing Unity global object ID. </summary>
internal sealed record ResolveGlobalObjectIdSelectorInput (string GlobalObjectId) : ResolveSelectorInput;
