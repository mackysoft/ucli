using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents one read-index artifact observation. </summary>
internal sealed record ReadyReadIndexArtifactOutput
{
    private ReadyReadIndexArtifactOutput (
        ReadyReadIndexArtifactName Name,
        ReadyReadIndexArtifactStatus Status,
        bool Required,
        IndexFreshness? Freshness,
        Sha256Digest? SourceInputsHash,
        DateTimeOffset? GeneratedAtUtc,
        UcliCode? Code,
        string? Message,
        string? ActionRequired)
    {
        if (!ContractLiteralCodec.IsDefined(Name))
        {
            throw new ArgumentOutOfRangeException(nameof(Name), Name, "Artifact name must be defined by the ready contract.");
        }

        if (!ContractLiteralCodec.IsDefined(Status))
        {
            throw new ArgumentOutOfRangeException(nameof(Status), Status, "Artifact status must be defined by the ready contract.");
        }

        if (Freshness.HasValue && !ContractLiteralCodec.IsDefined(Freshness.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(Freshness), Freshness, "Artifact freshness must be defined by the index contract.");
        }

        var hasSnapshot = Freshness.HasValue || SourceInputsHash is not null || GeneratedAtUtc.HasValue;
        var hasCompleteSnapshot = Freshness.HasValue
            && SourceInputsHash is not null
            && GeneratedAtUtc.HasValue
            && GeneratedAtUtc.Value != default;
        if (hasSnapshot != hasCompleteSnapshot)
        {
            throw new ArgumentException("Artifact snapshot fields must either all be specified or all be absent.", nameof(Freshness));
        }

        if (GeneratedAtUtc.HasValue && GeneratedAtUtc.Value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Artifact snapshot timestamp must use the UTC offset.", nameof(GeneratedAtUtc));
        }

        if (Status == ReadyReadIndexArtifactStatus.Available)
        {
            if (!hasCompleteSnapshot)
            {
                throw new ArgumentException("Available artifacts require a complete source snapshot.", nameof(Freshness));
            }

            if (Code is not null || Message is not null || ActionRequired is not null)
            {
                throw new ArgumentException("Available artifacts must not contain failure details.", nameof(Code));
            }
        }
        else
        {
            if (Code is null)
            {
                throw new ArgumentNullException(nameof(Code));
            }

            if (string.IsNullOrWhiteSpace(Message))
            {
                throw new ArgumentException("Failed artifacts require an error message.", nameof(Message));
            }

            if (ActionRequired is not null && string.IsNullOrWhiteSpace(ActionRequired))
            {
                throw new ArgumentException("Artifact action must not be empty.", nameof(ActionRequired));
            }
        }

        this.Name = Name;
        this.Status = Status;
        this.Required = Required;
        this.Freshness = Freshness;
        this.SourceInputsHash = SourceInputsHash;
        this.GeneratedAtUtc = GeneratedAtUtc;
        this.Code = Code;
        this.Message = Message;
        this.ActionRequired = ActionRequired;
    }

    /// <summary> Creates an available artifact with a complete source snapshot. </summary>
    public static ReadyReadIndexArtifactOutput Available (
        ReadyReadIndexArtifactName name,
        bool required,
        IndexFreshness freshness,
        Sha256Digest sourceInputsHash,
        DateTimeOffset generatedAtUtc)
    {
        return new ReadyReadIndexArtifactOutput(
            name,
            ReadyReadIndexArtifactStatus.Available,
            required,
            freshness,
            sourceInputsHash,
            generatedAtUtc,
            Code: null,
            Message: null,
            ActionRequired: null);
    }

    /// <summary> Creates a failed artifact without a readable source snapshot. </summary>
    public static ReadyReadIndexArtifactOutput Failed (
        ReadyReadIndexArtifactName name,
        bool required,
        UcliCode code,
        string message,
        string? actionRequired)
    {
        return new ReadyReadIndexArtifactOutput(
            name,
            ReadyReadIndexArtifactStatus.Failed,
            required,
            Freshness: null,
            SourceInputsHash: null,
            GeneratedAtUtc: null,
            code,
            message,
            actionRequired);
    }

    /// <summary> Creates a failed artifact with the source snapshot observed before validation failed. </summary>
    public static ReadyReadIndexArtifactOutput FailedWithSnapshot (
        ReadyReadIndexArtifactName name,
        bool required,
        IndexFreshness freshness,
        Sha256Digest sourceInputsHash,
        DateTimeOffset generatedAtUtc,
        UcliCode code,
        string message,
        string? actionRequired)
    {
        return new ReadyReadIndexArtifactOutput(
            name,
            ReadyReadIndexArtifactStatus.Failed,
            required,
            freshness,
            sourceInputsHash,
            generatedAtUtc,
            code,
            message,
            actionRequired);
    }

    public ReadyReadIndexArtifactName Name { get; }

    public ReadyReadIndexArtifactStatus Status { get; }

    public bool Required { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IndexFreshness? Freshness { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Sha256Digest? SourceInputsHash { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? GeneratedAtUtc { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UcliCode? Code { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ActionRequired { get; }
}
