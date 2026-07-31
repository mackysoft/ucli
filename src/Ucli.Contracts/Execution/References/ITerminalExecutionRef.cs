using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Contracts;

/// <summary>
/// References an execution whose immutable terminal record has been finalized.
/// </summary>
public interface ITerminalExecutionRef
{
    /// <summary> Gets the feature-defined execution kind. </summary>
    [JsonIgnore]
    ExecutionKind Kind { get; }

    /// <summary> Gets the identifier unique within <see cref="Kind" />. </summary>
    [JsonIgnore]
    Guid Id { get; }

    /// <summary> Gets the immutable digest of the definition fixed when this execution was registered. </summary>
    [JsonIgnore]
    Sha256Digest DefinitionDigest { get; }

    /// <summary> Gets the terminal lifecycle selected by the owning feature. </summary>
    [JsonIgnore]
    ExecutionLifecycle Lifecycle { get; }

    /// <summary> Gets the finalized feature-owned execution state. </summary>
    [JsonIgnore]
    ExecutionState State { get; }

    /// <summary> Gets the opaque feature-owned status locator, or <see langword="null" /> when none remains. </summary>
    [JsonIgnore]
    ExecutionStatusLocator? StatusLocator { get; }

    /// <summary> Gets the finalized immutable terminal-record artifact. </summary>
    [JsonIgnore]
    ArtifactRef TerminalRecordRef { get; }
}
