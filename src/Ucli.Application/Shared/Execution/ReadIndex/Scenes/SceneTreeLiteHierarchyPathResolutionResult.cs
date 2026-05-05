namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;

/// <summary> Represents one hierarchy-path resolution attempt against scene-tree-lite data. </summary>
/// <param name="GlobalObjectId"> The resolved GlobalObjectId when resolution succeeded; otherwise <see langword="null" />. </param>
/// <param name="ErrorMessage"> The failure message when resolution failed; otherwise <see langword="null" />. </param>
internal sealed record SceneTreeLiteHierarchyPathResolutionResult (
    string? GlobalObjectId,
    string? ErrorMessage)
{
    /// <summary> Gets a value indicating whether the path resolved to one GlobalObjectId. </summary>
    public bool IsSuccess => GlobalObjectId is not null && ErrorMessage is null;

    /// <summary> Creates a successful resolution result. </summary>
    /// <param name="globalObjectId"> The non-empty GlobalObjectId returned by scene-tree-lite. </param>
    /// <returns> A result whose <see cref="IsSuccess" /> value is <see langword="true" />. </returns>
    /// <exception cref="ArgumentNullException"> <paramref name="globalObjectId" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> <paramref name="globalObjectId" /> is empty or whitespace. </exception>
    public static SceneTreeLiteHierarchyPathResolutionResult Success (string globalObjectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(globalObjectId);
        return new SceneTreeLiteHierarchyPathResolutionResult(globalObjectId, null);
    }

    /// <summary> Creates a failed resolution result. </summary>
    /// <param name="message"> The non-empty failure message to report to the caller or fallback metadata. </param>
    /// <returns> A result whose <see cref="IsSuccess" /> value is <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> <paramref name="message" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> <paramref name="message" /> is empty or whitespace. </exception>
    public static SceneTreeLiteHierarchyPathResolutionResult Failure (string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new SceneTreeLiteHierarchyPathResolutionResult(null, message);
    }
}
