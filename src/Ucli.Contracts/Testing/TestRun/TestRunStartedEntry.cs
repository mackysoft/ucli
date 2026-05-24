namespace MackySoft.Ucli.Contracts.Testing;

/// <summary> Represents the <c>test.run.started</c> stream payload. </summary>
public sealed record TestRunStartedEntry (
    string RunId,
    string TestPlatform,
    string? TestFilter,
    string[] AssemblyNames,
    string[] TestCategories);
