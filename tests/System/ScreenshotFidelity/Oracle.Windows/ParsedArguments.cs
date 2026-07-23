using System.Globalization;

namespace MackySoft.Ucli.ScreenshotFidelityOracle.Windows;

internal sealed class ParsedArguments
{
    private readonly Dictionary<string, string> options;

    private ParsedArguments (string command, Dictionary<string, string> options)
    {
        Command = command;
        this.options = options;
    }

    internal string Command { get; }

    internal static ParsedArguments Parse (string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            throw new OracleUsageException("A command is required.");
        }

        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 1; index < args.Length; index += 2)
        {
            string option = args[index];
            if (!option.StartsWith("--", StringComparison.Ordinal) || option.Length == 2)
            {
                throw new OracleUsageException($"Expected an option name, but received '{option}'.");
            }

            if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
            {
                throw new OracleUsageException($"A value is required for '{option}'.");
            }

            if (!options.TryAdd(option, args[index + 1]))
            {
                throw new OracleUsageException($"The option '{option}' was specified more than once.");
            }
        }

        return new ParsedArguments(args[0], options);
    }

    internal void EnsureOnly (params string[] allowedOptions)
    {
        ArgumentNullException.ThrowIfNull(allowedOptions);

        var allowed = new HashSet<string>(allowedOptions, StringComparer.Ordinal);
        string? unexpected = options.Keys.FirstOrDefault(option => !allowed.Contains(option));
        if (unexpected != null)
        {
            throw new OracleUsageException($"The option '{unexpected}' is not valid for '{Command}'.");
        }
    }

    internal string Required (string option)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(option);

        if (!options.TryGetValue(option, out string? value))
        {
            throw new OracleUsageException($"The option '{option}' is required for '{Command}'.");
        }

        return value;
    }

    internal int RequiredPositiveInt32 (string option)
    {
        string value = Required(option);
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int result) || result <= 0)
        {
            throw new OracleUsageException($"The option '{option}' must be a positive 32-bit integer.");
        }

        return result;
    }
}
