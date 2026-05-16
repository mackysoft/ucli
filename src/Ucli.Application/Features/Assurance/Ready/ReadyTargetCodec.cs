namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Converts ready target values between typed values and public literals. </summary>
internal static class ReadyTargetCodec
{
    public const string Execution = "execution";

    public const string Mutation = "mutation";

    public const string Test = "test";

    public const string ReadIndex = "readIndex";

    /// <summary> Attempts to parse one public literal into a ready target. </summary>
    public static bool TryParseValue (
        string value,
        out ReadyTarget target)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (string.Equals(value, Execution, StringComparison.OrdinalIgnoreCase))
        {
            target = ReadyTarget.Execution;
            return true;
        }

        if (string.Equals(value, Mutation, StringComparison.OrdinalIgnoreCase))
        {
            target = ReadyTarget.Mutation;
            return true;
        }

        if (string.Equals(value, Test, StringComparison.OrdinalIgnoreCase))
        {
            target = ReadyTarget.Test;
            return true;
        }

        if (string.Equals(value, ReadIndex, StringComparison.OrdinalIgnoreCase))
        {
            target = ReadyTarget.ReadIndex;
            return true;
        }

        target = default;
        return false;
    }

    /// <summary> Converts one ready target to the public literal. </summary>
    public static string ToValue (ReadyTarget target)
    {
        return target switch
        {
            ReadyTarget.Execution => Execution,
            ReadyTarget.Mutation => Mutation,
            ReadyTarget.Test => Test,
            ReadyTarget.ReadIndex => ReadIndex,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported ready target."),
        };
    }
}
