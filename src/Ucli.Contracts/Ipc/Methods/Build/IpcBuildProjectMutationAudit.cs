using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the project mutation audit captured around build runner invocation. </summary>
public sealed record IpcBuildProjectMutationAudit
{
    /// <summary> Initializes one internally consistent project mutation audit. </summary>
    /// <param name="Mode"> The project mutation policy mode used for this audit. </param>
    /// <param name="Coverage"> The audit coverage. </param>
    /// <param name="Mutated"> Whether the project snapshot changed across runner invocation. </param>
    /// <param name="BeforeDigest"> The aggregate pre-runner project digest. </param>
    /// <param name="AfterDigest"> The aggregate post-runner project digest. </param>
    /// <param name="Items"> The project file changes ordered by unique project-relative path. </param>
    /// <exception cref="ArgumentNullException"> Thrown when a required digest or item collection is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when the aggregate digest, mutation flag, or item ordering is inconsistent. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when a required contract literal is not specified. </exception>
    [JsonConstructor]
    public IpcBuildProjectMutationAudit (
        BuildProfileProjectMutationMode Mode,
        IpcBuildProjectMutationAuditCoverage Coverage,
        bool Mutated,
        Sha256Digest BeforeDigest,
        Sha256Digest AfterDigest,
        IReadOnlyList<IpcBuildProjectMutationAuditItem> Items)
    {
        if (!TextVocabulary.IsDefined(Mode))
        {
            throw new ArgumentOutOfRangeException(nameof(Mode), Mode, "Project mutation mode must be specified.");
        }

        if (!TextVocabulary.IsDefined(Coverage))
        {
            throw new ArgumentOutOfRangeException(nameof(Coverage), Coverage, "Project mutation audit coverage must be specified.");
        }

        if (BeforeDigest == null)
        {
            throw new ArgumentNullException(nameof(BeforeDigest));
        }

        if (AfterDigest == null)
        {
            throw new ArgumentNullException(nameof(AfterDigest));
        }

        var items = ContractArgumentGuard.RequireItems(Items, nameof(Items));
        ValidateItems(items);
        if (Mutated != (items.Count != 0))
        {
            throw new ArgumentException("Mutated must match whether the audit contains changed items.", nameof(Mutated));
        }

        if (Mutated == (BeforeDigest == AfterDigest))
        {
            throw new ArgumentException("Aggregate project digests must differ exactly when the project mutated.");
        }

        this.Mode = Mode;
        this.Coverage = Coverage;
        this.Mutated = Mutated;
        this.BeforeDigest = BeforeDigest;
        this.AfterDigest = AfterDigest;
        this.Items = items;
    }

    public BuildProfileProjectMutationMode Mode { get; }

    public IpcBuildProjectMutationAuditCoverage Coverage { get; }

    public bool Mutated { get; }

    public Sha256Digest BeforeDigest { get; }

    public Sha256Digest AfterDigest { get; }

    public IReadOnlyList<IpcBuildProjectMutationAuditItem> Items { get; }

    private static void ValidateItems (IReadOnlyList<IpcBuildProjectMutationAuditItem> items)
    {
        ProjectMutationAuditPath? previousPath = null;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (previousPath != null && previousPath.CompareTo(item.Path) >= 0)
            {
                throw new ArgumentException("Project mutation items must be ordered by unique project-relative path.", nameof(items));
            }

            previousPath = item.Path;
        }
    }
}
