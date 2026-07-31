using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Contracts;

/// <summary> References one registered long-lived logical execution bound to one immutable definition. </summary>
public abstract record ExecutionRef
{
    /// <summary> Initializes the common identity and current feature-owned state of one execution. </summary>
    /// <param name="kind"> The feature-defined execution kind. </param>
    /// <param name="id"> The identifier unique within <paramref name="kind" />. </param>
    /// <param name="definitionDigest"> The immutable definition digest fixed when the execution was registered. </param>
    /// <param name="state"> The current state defined by the feature that owns the execution. </param>
    /// <param name="statusLocator"> The opaque feature-owned locator for subsequent operations, or <see langword="null" /> when none remains. </param>
    /// <exception cref="ArgumentNullException"> Thrown when a required reference value is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="id" /> is empty. </exception>
    protected ExecutionRef (
        ExecutionKind kind,
        Guid id,
        Sha256Digest definitionDigest,
        ExecutionState state,
        ExecutionStatusLocator? statusLocator)
    {
        Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        Id = ContractArgumentGuard.RequireNonEmptyGuid(id, nameof(id));
        DefinitionDigest = definitionDigest ?? throw new ArgumentNullException(nameof(definitionDigest));
        State = state ?? throw new ArgumentNullException(nameof(state));
        StatusLocator = statusLocator;
    }

    /// <summary> Gets the feature-defined execution kind. </summary>
    [JsonInclude]
    [JsonRequired]
    public ExecutionKind Kind { get; private init; }

    /// <summary> Gets the identifier unique within <see cref="Kind" />. </summary>
    [JsonInclude]
    [JsonRequired]
    public Guid Id { get; private init; }

    /// <summary> Gets the immutable digest of the definition fixed when this execution was registered. </summary>
    [JsonInclude]
    [JsonRequired]
    public Sha256Digest DefinitionDigest { get; private init; }

    /// <summary> Gets the common lifecycle selected by the owning feature for its current state. </summary>
    [JsonIgnore]
    public ExecutionLifecycle Lifecycle => LifecycleCore;

    private protected abstract ExecutionLifecycle LifecycleCore { get; }

    /// <summary> Gets the current feature-owned execution state. </summary>
    [JsonInclude]
    [JsonRequired]
    public ExecutionState State { get; private init; }

    /// <summary> Gets the opaque feature-owned status locator, or <see langword="null" /> when none remains. </summary>
    [JsonInclude]
    [JsonRequired]
    public ExecutionStatusLocator? StatusLocator { get; private init; }

    /// <summary>
    /// Rejects a candidate that reuses this reference's execution identity with a different definition digest.
    /// </summary>
    /// <param name="candidate"> The execution reference to compare with this established reference. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="candidate" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="candidate" /> has the same kind and identifier as this reference but a different definition digest.
    /// </exception>
    public void EnsureDefinitionConsistencyWith (ExecutionRef candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }

        if (Kind == candidate.Kind
            && Id == candidate.Id
            && DefinitionDigest != candidate.DefinitionDigest)
        {
            throw new ArgumentException(
                "The same execution kind and id must retain their established definition digest.",
                nameof(candidate));
        }
    }
}
