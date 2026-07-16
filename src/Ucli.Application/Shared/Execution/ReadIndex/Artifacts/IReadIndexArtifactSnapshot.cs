using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Exposes validated metadata shared by persisted read-index artifacts. </summary>
internal interface IReadIndexArtifactSnapshot
{
    /// <summary> Gets the artifact generation timestamp. </summary>
    DateTimeOffset GeneratedAtUtc { get; }

    /// <summary> Gets the validated source-inputs digest. </summary>
    Sha256Digest SourceInputsHash { get; }
}
