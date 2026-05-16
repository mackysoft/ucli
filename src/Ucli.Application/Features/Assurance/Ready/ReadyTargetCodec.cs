namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Converts ready target values between typed values and public literals. </summary>
internal static class ReadyTargetCodec
{
    public const string Execution = "execution";

    public const string Mutation = "mutation";

    public const string Test = "test";

    public const string ReadIndex = "readIndex";

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

    /// <summary> Parses one ready target literal. </summary>
    public static bool TryParse (
        string? value,
        out ReadyTarget target)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            target = default;
            return false;
        }

        var normalizedValue = value.Trim();
        if (string.Equals(normalizedValue, Execution, StringComparison.OrdinalIgnoreCase))
        {
            target = ReadyTarget.Execution;
            return true;
        }

        if (string.Equals(normalizedValue, Mutation, StringComparison.OrdinalIgnoreCase))
        {
            target = ReadyTarget.Mutation;
            return true;
        }

        if (string.Equals(normalizedValue, Test, StringComparison.OrdinalIgnoreCase))
        {
            target = ReadyTarget.Test;
            return true;
        }

        if (string.Equals(normalizedValue, ReadIndex, StringComparison.OrdinalIgnoreCase))
        {
            target = ReadyTarget.ReadIndex;
            return true;
        }

        target = default;
        return false;
    }
}
