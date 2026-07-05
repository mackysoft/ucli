using MackySoft.Ucli.Application.Shared.EnvironmentVariables;

namespace MackySoft.Ucli.TestSupport;

internal sealed class StubEnvironmentVariableReader : IEnvironmentVariableReader
{
    private readonly IReadOnlyDictionary<string, string?> values;

    public StubEnvironmentVariableReader ()
        : this(new Dictionary<string, string?>(StringComparer.Ordinal))
    {
    }

    public StubEnvironmentVariableReader (IReadOnlyDictionary<string, string?> values)
    {
        this.values = values ?? throw new ArgumentNullException(nameof(values));
    }

    public string? Get (string variableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(variableName);
        return values.TryGetValue(variableName, out var value)
            ? value
            : null;
    }
}
