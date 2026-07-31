using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Contracts;

/// <summary> References a logical execution that can continue normal forward progress. </summary>
public sealed record ActiveExecutionRef :
    ExecutionRef,
    IReconnectableExecutionRef
{
    /// <summary> Initializes an active execution reference. </summary>
    /// <param name="kind"> The feature-defined execution kind. </param>
    /// <param name="id"> The identifier unique within <paramref name="kind" />. </param>
    /// <param name="definitionDigest"> The immutable definition digest fixed when the execution was registered. </param>
    /// <param name="state"> The feature-owned state that maps to <see cref="ExecutionLifecycle.Active" />. </param>
    /// <param name="statusLocator"> The opaque feature-owned locator for subsequent operations, or <see langword="null" /> when none is available. </param>
    /// <exception cref="ArgumentNullException"> Thrown when a required reference value is <see langword="null" />. </exception>
    [JsonConstructor]
    public ActiveExecutionRef (
        ExecutionKind kind,
        Guid id,
        Sha256Digest definitionDigest,
        ExecutionState state,
        ExecutionStatusLocator? statusLocator)
        : base(kind, id, definitionDigest, state, statusLocator)
    {
    }

    /// <inheritdoc />
    private protected override ExecutionLifecycle LifecycleCore =>
        ExecutionLifecycle.Active;
}
