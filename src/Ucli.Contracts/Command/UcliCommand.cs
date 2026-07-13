namespace MackySoft.Ucli.Contracts;

/// <summary> Represents one command identifier used by CLI/IPC contracts. </summary>
public readonly record struct UcliCommand
{
    /// <summary> Gets the command identifier string. </summary>
    public string Name { get; }

    /// <summary> Gets whether this instance represents a valid command identifier. </summary>
    public bool IsValid => IsValidName(Name);

    /// <summary> Initializes a new instance of the <see cref="UcliCommand" /> struct. </summary>
    /// <param name="name"> The command identifier string. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="name" /> is null, empty, or whitespace. </exception>
    public UcliCommand (
        string name)
    {
        if (!IsValidName(name))
        {
            throw new ArgumentException("Command name is invalid.", nameof(name));
        }

        Name = name;
    }

    /// <summary> Converts one command identifier into its string form. </summary>
    /// <param name="command"> The command identifier to convert. </param>
    public static implicit operator string (UcliCommand command)
    {
        return command.Name;
    }

    /// <summary> Tries to create one validated command identifier. </summary>
    /// <param name="name"> The raw command identifier string. </param>
    /// <param name="command"> The validated command when successful. </param>
    /// <returns> <see langword="true" /> when the input is a valid command identifier; otherwise <see langword="false" />. </returns>
    public static bool TryCreate (
        string? name,
        out UcliCommand command)
    {
        if (!IsValidName(name))
        {
            command = default;
            return false;
        }

        command = new UcliCommand(name!);
        return true;
    }

    /// <summary> Determines whether the specified string is a valid command identifier. </summary>
    /// <param name="name"> The command identifier string to validate. </param>
    /// <returns> <see langword="true" /> when valid; otherwise <see langword="false" />. </returns>
    public static bool IsValidName (string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (name.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }) >= 0)
        {
            return false;
        }

        if (name.StartsWith(".", StringComparison.Ordinal)
            || name.EndsWith(".", StringComparison.Ordinal)
            || name.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }
}
