using System.Diagnostics.CodeAnalysis;

namespace MackySoft.Ucli.Contracts;

/// <summary> Represents one command identifier used by CLI/IPC contracts. </summary>
public sealed record UcliCommand
{
    /// <summary> Gets the command identifier string. </summary>
    public string Name { get; }

    /// <summary> Initializes a command identifier after validating its dot-delimited syntax. </summary>
    /// <param name="name"> The command identifier string. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="name" /> is null, empty, consists only of whitespace, contains an ASCII space, tab, carriage return, or line feed, or contains an empty dot-delimited segment. </exception>
    public UcliCommand (
        string name)
    {
        if (!IsValidName(name))
        {
            throw new ArgumentException("Command name is invalid.", nameof(name));
        }

        Name = name;
    }

    /// <summary> Tries to create one validated command identifier. </summary>
    /// <param name="name"> The candidate command identifier. </param>
    /// <param name="command"> The validated command when successful; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the value contains a non-whitespace character, contains no ASCII space, tab, carriage return, or line feed, and has no empty dot-delimited segment; otherwise <see langword="false" />. </returns>
    public static bool TryCreate (
        string? name,
        [NotNullWhen(true)] out UcliCommand? command)
    {
        if (!IsValidName(name))
        {
            command = null;
            return false;
        }

        command = new UcliCommand(name);
        return true;
    }

    /// <inheritdoc />
    public override string ToString ()
    {
        return Name;
    }

    /// <summary> Determines whether the specified string is a valid command identifier. </summary>
    /// <param name="name"> The candidate command identifier. </param>
    /// <returns> <see langword="true" /> when the value satisfies the whitespace and dot-delimited segment constraints; otherwise <see langword="false" />. </returns>
    public static bool IsValidName ([NotNullWhen(true)] string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        var hasNonWhitespaceCharacter = false;
        var segmentStart = true;
        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];
            if (character is ' ' or '\t' or '\r' or '\n')
            {
                return false;
            }

            hasNonWhitespaceCharacter |= !char.IsWhiteSpace(character);
            if (character == '.')
            {
                if (segmentStart)
                {
                    return false;
                }

                segmentStart = true;
                continue;
            }

            segmentStart = false;
        }

        return hasNonWhitespaceCharacter && !segmentStart;
    }
}
