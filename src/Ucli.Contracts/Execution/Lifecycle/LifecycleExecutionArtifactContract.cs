namespace MackySoft.Ucli.Contracts.Execution.Lifecycle;

/// <summary> Creates common artifact reference values from the feature-owned finite vocabulary. </summary>
public static class LifecycleExecutionArtifactContract
{
    /// <summary> Gets the artifact kind of every Lifecycle Execution terminal record. </summary>
    public static ArtifactKind TerminalRecordKind =>
        new(TextVocabulary.GetText(LifecycleExecutionArtifactKind.TerminalRecord));

    /// <summary> Gets the media type of every Lifecycle Execution terminal record. </summary>
    public static ArtifactMediaType TerminalRecordMediaType =>
        new(TextVocabulary.GetText(LifecycleExecutionArtifactMediaType.Json));
}
