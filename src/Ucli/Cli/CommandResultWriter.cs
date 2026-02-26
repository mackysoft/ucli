using System.Text.Json;

namespace MackySoft.Ucli.Cli;

/// <summary> Serializes command results and writes them to standard output as JSON. </summary>
internal static class CommandResultWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary> Writes the specified command result to standard output in JSON format. </summary>
    /// <param name="result"> The command result to serialize. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="result" /> is <see langword="null" />. </exception>
    public static void WriteToStandardOutput (CommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Console.Out.WriteLine(JsonSerializer.Serialize(result, SerializerOptions));
    }
}