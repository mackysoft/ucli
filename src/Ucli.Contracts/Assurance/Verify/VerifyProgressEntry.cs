using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents the <c>verify.started</c> and <c>verify.completed</c> stream payload. </summary>
public sealed record VerifyProgressEntry
{
    /// <summary> Initializes one verify progress entry. </summary>
    /// <exception cref="ArgumentNullException"> Thrown when a required reference value is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="StepCount" /> is negative or <paramref name="Verdict" /> has an undefined value. </exception>
    [JsonConstructor]
    public VerifyProgressEntry (
        VerifyProfileSource ProfileSource,
        string ProfileName,
        string? ProfilePath,
        Sha256Digest ProfileDigest,
        int StepCount,
        AssuranceVerdict? Verdict)
    {
        if (!TextVocabulary.IsDefined(ProfileSource))
        {
            throw new ArgumentOutOfRangeException(nameof(ProfileSource), ProfileSource, "Verify profile source must be defined.");
        }

        if (Verdict.HasValue && !TextVocabulary.IsDefined(Verdict.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(Verdict), Verdict, "Verdict must be defined by the assurance contract.");
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
        this.Verdict = Verdict;
    }

    public VerifyProfileSource ProfileSource { get; }

    public string ProfileName { get; }

    public string? ProfilePath { get; }

    public Sha256Digest ProfileDigest { get; }

    public int StepCount { get; }

    public AssuranceVerdict? Verdict { get; }
}
