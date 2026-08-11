namespace MackySoft.Ucli.Contracts.Tests.Artifacts;

public sealed class GameViewRecordingArtifactContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void RecordingArtifacts_ExposeThePublishedKindsAndMediaTypes ()
    {
        Assert.Equal("gameViewRecordingRequest", GameViewRecordingArtifactKinds.Request.Value);
        Assert.Equal("gameViewRecordingManifest", GameViewRecordingArtifactKinds.Manifest.Value);
        Assert.Equal("gameViewRecordingVideo", GameViewRecordingArtifactKinds.Video.Value);
        Assert.Equal("gameViewRecordingCleanup", GameViewRecordingArtifactKinds.Cleanup.Value);
        Assert.Equal("gameViewRecordingTerminalRecord", GameViewRecordingArtifactKinds.TerminalRecord.Value);
        Assert.Equal("gameViewRecordingPartialOutput", GameViewRecordingArtifactKinds.PartialOutput.Value);
        Assert.Equal("application/json", GameViewRecordingArtifactMediaTypes.Json.Value);
        Assert.Equal("video/mp4", GameViewRecordingArtifactMediaTypes.Mp4.Value);
        Assert.Equal("application/octet-stream", GameViewRecordingArtifactMediaTypes.Binary.Value);
    }
}
