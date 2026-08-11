using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Recording.Requests;
using MackySoft.Ucli.Contracts.Recording;

namespace MackySoft.Ucli.Application.Features.Recording.Artifacts;

/// <summary> Owns provider staging and immutable publication for one recording identifier. </summary>
internal interface IGameViewRecordingArtifactLease
{
    /// <summary> Gets the durable execution-state path owned by the recording state store. </summary>
    AbsolutePath ExecutionStatePath { get; }

    /// <summary> Publishes the normalized effective request. </summary>
    ValueTask<GameViewRecordingArtifactPublicationResult> PublishRequestAsync (
        GameViewRecordingEffectiveRequest request,
        PathArtifactRef? knownArtifact,
        CancellationToken cancellationToken = default);

    /// <summary> Validates and publishes the finalized provider MP4. </summary>
    ValueTask<GameViewRecordingVideoPublicationResult> PublishVideoAsync (
        GameViewRecordingEffectiveRequest request,
        int? observedEncodedFrameCount,
        PathArtifactRef? knownArtifact,
        CancellationToken cancellationToken = default);

    /// <summary> Publishes the finalized recording manifest. </summary>
    ValueTask<GameViewRecordingArtifactPublicationResult> PublishManifestAsync (
        GameViewRecordingManifest manifest,
        PathArtifactRef? knownArtifact,
        CancellationToken cancellationToken = default);

    /// <summary> Publishes the finalized cleanup record. </summary>
    ValueTask<GameViewRecordingArtifactPublicationResult> PublishCleanupAsync (
        GameViewRecordingCleanupRecord cleanup,
        PathArtifactRef? knownArtifact,
        CancellationToken cancellationToken = default);

    /// <summary> Publishes the terminal record after every referenced artifact has been published. </summary>
    ValueTask<GameViewRecordingArtifactPublicationResult> PublishTerminalRecordAsync (
        GameViewRecordingTerminalRecord terminalRecord,
        PathArtifactRef? knownArtifact,
        CancellationToken cancellationToken = default);

    /// <summary>Publishes provider bytes as diagnostic partial output when the provider created them.</summary>
    ValueTask<GameViewRecordingPartialOutputRecoveryResult> RecoverPartialOutputAsync (
        PathArtifactRef? knownArtifact,
        CancellationToken cancellationToken = default);

    /// <summary> Removes this fresh lease's request artifact when no other artifact was published. </summary>
    ValueTask<GameViewRecordingArtifactDiscardResult> DiscardUnregisteredArtifactsAsync (
        PathArtifactRef requestArtifact,
        CancellationToken cancellationToken = default);

    /// <summary> Deletes only the known provider output and its empty provider directory. </summary>
    GameViewRecordingStagingCleanupResult CleanupProviderOutput ();
}
