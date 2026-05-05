namespace MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve;

/// <summary> Represents a selector that resolves a Unity asset GUID. </summary>
internal sealed record ResolveAssetGuidSelectorInput (string AssetGuid) : ResolveSelectorInput;
