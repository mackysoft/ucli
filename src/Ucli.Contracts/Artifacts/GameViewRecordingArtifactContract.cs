namespace MackySoft.Ucli.Contracts;

/// <summary>Defines immutable artifact kinds published by GameView recording.</summary>
public static class GameViewRecordingArtifactKinds
{
    public static ArtifactKind Request { get; } = new("gameViewRecordingRequest");

    public static ArtifactKind Manifest { get; } = new("gameViewRecordingManifest");

    public static ArtifactKind Video { get; } = new("gameViewRecordingVideo");

    public static ArtifactKind Cleanup { get; } = new("gameViewRecordingCleanup");

    public static ArtifactKind TerminalRecord { get; } = new("gameViewRecordingTerminalRecord");

    public static ArtifactKind PartialOutput { get; } = new("gameViewRecordingPartialOutput");
}

/// <summary>Defines media types published by GameView recording.</summary>
public static class GameViewRecordingArtifactMediaTypes
{
    public static ArtifactMediaType Json { get; } = new("application/json");

    public static ArtifactMediaType Mp4 { get; } = new("video/mp4");

    public static ArtifactMediaType Binary { get; } = new("application/octet-stream");
}
