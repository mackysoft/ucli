using System.Reflection;
using MackySoft.FileSystem;

namespace MackySoft.Ucli.Hosting.Cli.Schemas;

/// <summary> Resolves the static schema set copied beside the running uCLI executable. </summary>
internal sealed class InstalledStaticSchemaSetProvider : IInstalledStaticSchemaSetProvider
{
    private static readonly RootRelativePath SchemasRelativePath =
        RootRelativePath.Parse("schemas");

    private readonly AbsolutePath schemaRoot;
    private readonly string runningPackageVersion;

    /// <summary>
    /// Initializes a provider for one installed schema root and the assembly version
    /// exposed by the matching uCLI executable.
    /// </summary>
    internal InstalledStaticSchemaSetProvider (
        AbsolutePath schemaRoot,
        string assemblyInformationalVersion)
    {
        this.schemaRoot = schemaRoot
            ?? throw new ArgumentNullException(nameof(schemaRoot));
        runningPackageVersion = NormalizeInformationalVersion(
            assemblyInformationalVersion);
    }

    /// <summary> Creates the provider for the schema set copied beside the running uCLI executable. </summary>
    internal static InstalledStaticSchemaSetProvider CreateForRunningApplication ()
    {
        var applicationRoot = AbsolutePath.Parse(AppContext.BaseDirectory);
        var schemaRoot = ContainedPath.Create(
            applicationRoot,
            SchemasRelativePath).Target;
        var informationalVersion = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? throw new InvalidOperationException(
                "The running uCLI assembly does not declare AssemblyInformationalVersion.");
        return new InstalledStaticSchemaSetProvider(
            schemaRoot,
            informationalVersion);
    }

    /// <inheritdoc />
    public UcliStaticSchemaSet Load ()
    {
        var schemaSet = UcliStaticSchemaSetLoader.Load(schemaRoot);
        if (!string.Equals(
                schemaSet.Manifest.PackageVersion,
                runningPackageVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Installed static schema packageVersion "
                + $"'{schemaSet.Manifest.PackageVersion}' does not match "
                + $"the running uCLI version '{runningPackageVersion}'.");
        }

        return schemaSet;
    }

    private static string NormalizeInformationalVersion (
        string assemblyInformationalVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            assemblyInformationalVersion);
        var version = assemblyInformationalVersion.Trim();
        var metadataSeparatorIndex = version.IndexOf('+');
        if (metadataSeparatorIndex >= 0)
        {
            version = version[..metadataSeparatorIndex];
        }

        if (version.Length == 0)
        {
            throw new ArgumentException(
                "Assembly informational version must contain a package version before build metadata.",
                nameof(assemblyInformationalVersion));
        }

        return version;
    }
}
