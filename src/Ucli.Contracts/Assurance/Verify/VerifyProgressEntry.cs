using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Provides the profile identity shared by verify progress events. </summary>
public abstract record VerifyProgressEntry
{
    protected VerifyProgressEntry (
        VerifyProfileSource ProfileSource,
        string ProfileName,
        string? ProfilePath,
        Sha256Digest ProfileDigest,
        int StepCount)
    {
        if (!TextVocabulary.IsDefined(ProfileSource))
        {
            throw new ArgumentOutOfRangeException(nameof(ProfileSource), ProfileSource, "Verify profile source must be defined.");
        }
        if (StepCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(StepCount), StepCount, "Step count must not be negative.");
        }

        this.ProfileSource = ProfileSource;
        this.ProfileName = ProfileName ?? throw new ArgumentNullException(nameof(ProfileName));
        this.ProfilePath = ProfilePath;
        this.ProfileDigest = ProfileDigest ?? throw new ArgumentNullException(nameof(ProfileDigest));
        this.StepCount = StepCount;
    }

    public VerifyProfileSource ProfileSource { get; }

    public string ProfileName { get; }

    public string? ProfilePath { get; }

    public Sha256Digest ProfileDigest { get; }

    public int StepCount { get; }
}

/// <summary> Represents the <c>verify.started</c> stream payload. </summary>
public sealed record VerifyStartedEntry : VerifyProgressEntry
{
    [JsonConstructor]
    public VerifyStartedEntry (
        VerifyProfileSource ProfileSource,
        string ProfileName,
        string? ProfilePath,
        Sha256Digest ProfileDigest,
        int StepCount)
        : base(ProfileSource, ProfileName, ProfilePath, ProfileDigest, StepCount)
    {
    }
}

/// <summary> Represents the <c>verify.completed</c> stream payload. </summary>
public sealed record VerifyCompletedEntry : VerifyProgressEntry
{
    [JsonConstructor]
    public VerifyCompletedEntry (
        VerifyProfileSource ProfileSource,
        string ProfileName,
        string? ProfilePath,
        Sha256Digest ProfileDigest,
        int StepCount,
        Verdict Verdict)
        : base(ProfileSource, ProfileName, ProfilePath, ProfileDigest, StepCount)
    {
        if (!TextVocabulary.IsDefined(Verdict))
        {
            throw new ArgumentOutOfRangeException(nameof(Verdict), Verdict, "Verdict must be defined by the assurance contract.");
        }

        this.Verdict = Verdict;
    }

    public Verdict Verdict { get; }
}
