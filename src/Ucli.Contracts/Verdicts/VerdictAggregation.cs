namespace MackySoft.Ucli.Contracts;

/// <summary> Combines already established verdicts without interpreting their domain evidence. </summary>
public static class VerdictAggregation
{
    /// <summary>
    /// Returns the highest-priority verdict in <c>fail &gt; incomplete &gt; pass &gt; null</c> order.
    /// </summary>
    /// <param name="verdicts">
    /// Established verdicts. A <see langword="null" /> item denotes the absence of a verdict and does not
    /// represent incomplete evidence.
    /// </param>
    /// <returns>
    /// <see cref="Verdict.Fail" /> when any item failed; otherwise <see cref="Verdict.Incomplete" /> when
    /// any item is incomplete; otherwise <see cref="Verdict.Pass" /> when any item passed; otherwise
    /// <see langword="null" />.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="verdicts" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An item contains a value outside the public verdict vocabulary.
    /// </exception>
    public static Verdict? Aggregate (IEnumerable<Verdict?> verdicts)
    {
        if (verdicts == null)
        {
            throw new ArgumentNullException(nameof(verdicts));
        }

        var aggregate = (Verdict?)null;
        foreach (var verdict in verdicts)
        {
            if (!verdict.HasValue)
            {
                continue;
            }

            if (!TextVocabulary.IsDefined(verdict.Value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(verdicts),
                    verdict.Value,
                    "Every verdict must be a defined public contract value.");
            }

            aggregate = Combine(aggregate, verdict.Value);
        }

        return aggregate;
    }

    private static Verdict Combine (Verdict? aggregate, Verdict verdict)
    {
        return verdict switch
        {
            Verdict.Fail => Verdict.Fail,
            Verdict.Incomplete => aggregate == Verdict.Fail
                ? Verdict.Fail
                : Verdict.Incomplete,
            Verdict.Pass => aggregate ?? Verdict.Pass,
            _ => throw new ArgumentOutOfRangeException(
                "verdicts",
                verdict,
                "Every verdict must have an explicit aggregation policy."),
        };
    }
}
