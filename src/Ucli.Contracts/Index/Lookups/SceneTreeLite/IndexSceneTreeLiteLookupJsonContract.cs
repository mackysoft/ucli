namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Represents persisted <c>scene-tree-lite/&lt;sceneKey&gt;.lookup.json</c> contract fields. </summary>
/// <param name="SchemaVersion"> The schema-version value. </param>
/// <param name="GeneratedAtUtc"> The generated-at timestamp. </param>
/// <param name="ScenePath"> The project-relative scene path represented by this lookup. </param>
/// <param name="SourceInputsHash"> The source-inputs hash value. </param>
/// <param name="Roots"> The root scene-tree-lite nodes. </param>
internal sealed record IndexSceneTreeLiteLookupJsonContract (
    int SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    string? ScenePath,
    string? SourceInputsHash,
    IReadOnlyList<IndexSceneTreeLiteNodeJsonContract>? Roots);
