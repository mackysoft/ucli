using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Contracts;

/// <summary> References a logical execution whose immutable terminal record has been finalized. </summary>
public sealed record TerminalExecutionRef : ExecutionRef
{
    /// <summary> Initializes a terminal execution reference. </summary>
    /// <param name="kind"> The feature-defined execution kind. </param>
    /// <param name="id"> The identifier unique within <paramref name="kind" />. </param>
    /// <param name="definitionDigest"> The immutable definition digest fixed when the execution was registered. </param>
    /// <param name="state"> The feature-owned state that maps to <see cref="ExecutionLifecycle.Terminal" />. </param>
    /// <param name="statusLocator"> The opaque feature-owned locator for subsequent operations, or <see langword="null" /> when none remains. </param>
    /// <param name="terminalRecordRef"> The reference returned only after the immutable terminal record has been published. </param>
    /// <exception cref="ArgumentNullException"> Thrown when a required reference value is <see langword="null" />. </exception>
    [JsonConstructor]
    public TerminalExecutionRef (
        ExecutionKind kind,
        Guid id,
        Sha256Digest definitionDigest,
        ExecutionState state,
        ExecutionStatusLocator? statusLocator,
        ArtifactRef terminalRecordRef)
        : base(kind, id, definitionDigest, state, statusLocator)
    {
        TerminalRecordRef = terminalRecordRef
            ?? throw new ArgumentNullException(nameof(terminalRecordRef));
    }

    /// <inheritdoc />
    private protected override ExecutionLifecycle LifecycleCore =>
        ExecutionLifecycle.Terminal;

    /// <summary> Gets the finalized immutable terminal-record artifact. </summary>
    [JsonInclude]
    [JsonRequired]
    public ArtifactRef TerminalRecordRef { get; private init; }
}
