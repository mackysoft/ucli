namespace MackySoft.Ucli.Shared.EnvironmentVariables;

/// <summary> Reads process environment variables for runtime input resolution. </summary>
internal interface IEnvironmentVariableReader
{
    /// <summary> Reads one environment variable value by name. </summary>
    /// <param name="variableName"> The environment variable name. </param>
    /// <returns> The environment variable value when present; otherwise <see langword="null" />. </returns>
    string? Get (string variableName);
}