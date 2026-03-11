namespace MackySoft.Ucli.EnvironmentVariables;

/// <summary> Reads environment variables from the current process environment. </summary>
internal sealed class ProcessEnvironmentVariableReader : IEnvironmentVariableReader
{
    /// <inheritdoc />
    public string? Get (string variableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(variableName);
        return Environment.GetEnvironmentVariable(variableName);
    }
}