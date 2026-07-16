using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve.Contracts;

/// <summary> Represents a selector that resolves an existing Unity global object ID. </summary>
internal sealed record ResolveGlobalObjectIdSelectorInput : ResolveSelectorInput
{
    /// <summary> Initializes a selector with a validated Unity GlobalObjectId. </summary>
    /// <param name="globalObjectId"> The supported non-null Unity GlobalObjectId. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="globalObjectId" /> is <see langword="null" />. </exception>
    public ResolveGlobalObjectIdSelectorInput (UnityGlobalObjectId globalObjectId)
    {
        GlobalObjectId = globalObjectId ?? throw new ArgumentNullException(nameof(globalObjectId));
    }

    /// <summary> Gets the validated Unity GlobalObjectId. </summary>
    public UnityGlobalObjectId GlobalObjectId { get; }
}
