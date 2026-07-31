using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Contracts;

/// <summary>
/// References an active or recovering execution whose durable status can be reconnected.
/// </summary>
public interface IReconnectableExecutionRef
{
    /// <summary> Gets the feature-defined execution kind. </summary>
    [JsonIgnore]
    ExecutionKind Kind { get; }

    /// <summary> Gets the identifier unique within <see cref="Kind" />. </summary>
    [JsonIgnore]
    Guid Id { get; }

    /// <summary> Gets the immutable digest of the registered execution definition. </summary>
    [JsonIgnore]
    Sha256Digest DefinitionDigest { get; }

    /// <summary> Gets the active or recovery lifecycle selected by the owning feature. </summary>
    [JsonIgnore]
    ExecutionLifecycle Lifecycle { get; }

    /// <summary> Gets the current feature-owned execution state. </summary>
    [JsonIgnore]
    ExecutionState State { get; }

    /// <summary> Gets the locator used to reconnect to durable execution status. </summary>
    [JsonIgnore]
    ExecutionStatusLocator? StatusLocator { get; }
}
