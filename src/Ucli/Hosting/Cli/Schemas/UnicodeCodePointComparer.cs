namespace MackySoft.Ucli.Hosting.Cli.Schemas;

/// <summary> Orders text by Unicode scalar value for deterministic manifest validation. </summary>
internal sealed class UnicodeCodePointComparer : IComparer<string>
{
    public static UnicodeCodePointComparer Instance { get; } = new();

    public int Compare (string? left, string? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left == null)
        {
            return -1;
        }

        if (right == null)
        {
            return 1;
        }

        var leftRunes = left.EnumerateRunes().GetEnumerator();
        var rightRunes = right.EnumerateRunes().GetEnumerator();
        while (true)
        {
            var hasLeft = leftRunes.MoveNext();
            var hasRight = rightRunes.MoveNext();
            if (!hasLeft || !hasRight)
            {
                return hasLeft.CompareTo(hasRight);
            }

            var comparison = leftRunes.Current.Value.CompareTo(rightRunes.Current.Value);
            if (comparison != 0)
            {
                return comparison;
            }
        }
    }
}
