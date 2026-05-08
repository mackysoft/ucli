namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Provides shared predicates for scene-tree source-state semantics. </summary>
public static class SceneTreeSourceStatePolicy
{
    /// <summary> Determines whether one source state represents dirty live Unity editor state. </summary>
    /// <param name="sourceState"> The source-state contract to inspect. </param>
    /// <returns> <see langword="true" /> when the state is dirty and live; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="sourceState" /> is <see langword="null" />. </exception>
    public static bool IsDirtyLiveSource (SceneTreeSourceState sourceState)
    {
        if (sourceState == null)
        {
            throw new ArgumentNullException(nameof(sourceState));
        }

        return sourceState.IsDirty
            && IsLiveSourceKind(sourceState.Kind);
    }

    /// <summary> Determines whether one source kind is live Unity editor state rather than persisted data. </summary>
    /// <param name="sourceKind"> The source-state kind. </param>
    /// <returns> <see langword="true" /> when the kind can represent live editor state; otherwise <see langword="false" />. </returns>
    public static bool IsLiveSourceKind (SceneTreeSourceStateKind sourceKind)
    {
        return sourceKind != SceneTreeSourceStateKind.PersistedPreview
            && sourceKind != SceneTreeSourceStateKind.ReadIndex;
    }
}
