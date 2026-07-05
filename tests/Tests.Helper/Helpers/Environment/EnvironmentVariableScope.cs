namespace MackySoft.Tests;

internal sealed class EnvironmentVariableScope : IDisposable
{
    private readonly Dictionary<string, string?> originalValues = new(StringComparer.Ordinal);

    private bool disposed;

    public EnvironmentVariableScope (IReadOnlyDictionary<string, string?> environmentVariables)
    {
        ArgumentNullException.ThrowIfNull(environmentVariables);

        foreach (var (name, value) in environmentVariables)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            originalValues.Add(name, Environment.GetEnvironmentVariable(name));
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    public void Dispose ()
    {
        if (disposed)
        {
            return;
        }

        foreach (var (name, value) in originalValues)
        {
            Environment.SetEnvironmentVariable(name, value);
        }

        disposed = true;
    }
}
