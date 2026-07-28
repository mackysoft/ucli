using MackySoft.FileSystem;
using MackySoft.JsonSchema.Generation.Diagnostics;
using MackySoft.Ucli.Hosting.Composition.Schemas;

namespace MackySoft.Ucli.SchemaGenerator;

internal static class Program
{
    private static int Main (string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (!TryParseArgs(args, out var options))
        {
            WriteUsage();
            return 2;
        }

        return Generate(options!);
    }

    private static int Generate (SchemaGeneratorArguments options)
    {
        try
        {
            UcliStaticSchemaSetGenerator.Generate(
                options.OutputRoot,
                options.RepositoryRoot,
                options.PackageVersion);
            Console.WriteLine($"Generated schemas: {options.OutputRoot.Value}");
            return 0;
        }
        catch (JsonContractGenerationException exception)
        {
            Console.Error.WriteLine(
                $"{exception.Message} contractId={exception.ContractId ?? "(unknown)"} "
                + $"targetType={exception.TargetType?.FullName ?? "(unknown)"} "
                + $"property={exception.JsonPropertyName ?? "(unknown)"}");
            return 1;
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or InvalidOperationException
                                          or ArgumentException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static bool TryParseArgs (
        string[] args,
        out SchemaGeneratorArguments? options)
    {
        options = null;
        string? outputArgument = null;
        string? packageVersion = null;
        string? repositoryArgument = null;
        if (!TryParseOptionValues(
                args,
                ref outputArgument,
                ref packageVersion,
                ref repositoryArgument))
        {
            return false;
        }

        return TryResolveArguments(
            outputArgument!,
            packageVersion,
            repositoryArgument,
            out options);
    }

    private static bool TryResolveArguments (
        string outputArgument,
        string? packageVersion,
        string? repositoryArgument,
        out SchemaGeneratorArguments? options)
    {
        var currentDirectory = AbsolutePath.Parse(Environment.CurrentDirectory);
        var repositoryRoot = currentDirectory;
        if (!TryResolvePath(currentDirectory, outputArgument, out var outputRoot)
            || (repositoryArgument != null
                && !TryResolvePath(currentDirectory, repositoryArgument, out repositoryRoot)))
        {
            options = null;
            return false;
        }

        options = new SchemaGeneratorArguments(outputRoot!, packageVersion, repositoryRoot!);
        return true;
    }

    private static bool TryParseOptionValues (
        string[] args,
        ref string? outputArgument,
        ref string? packageVersion,
        ref string? repositoryArgument)
    {
        for (var index = 0; index < args.Length; index++)
        {
            var optionName = args[index];
            if (!TryReadOptionValue(args, ref index, out var value)
                || !TryAssignOption(
                    optionName,
                    value,
                    ref outputArgument,
                    ref packageVersion,
                    ref repositoryArgument))
            {
                return false;
            }
        }

        return outputArgument != null;
    }

    private static bool TryAssignOption (
        string optionName,
        string value,
        ref string? outputArgument,
        ref string? packageVersion,
        ref string? repositoryArgument)
    {
        switch (optionName)
        {
            case "--output":
                outputArgument = value;
                return true;
            case "--package-version":
                packageVersion = value;
                return true;
            case "--repository-root":
                repositoryArgument = value;
                return true;
            default:
                return false;
        }
    }

    private static bool TryResolvePath (
        AbsolutePath currentDirectory,
        string? value,
        out AbsolutePath? result)
    {
        if (value == null
            || !AbsolutePath.TryResolve(currentDirectory, value, out result, out _)
            || result == null)
        {
            result = null;
            return false;
        }

        return true;
    }

    private static bool TryReadOptionValue (
        string[] args,
        ref int optionIndex,
        out string value)
    {
        if (optionIndex >= args.Length - 1)
        {
            value = string.Empty;
            return false;
        }

        value = args[++optionIndex];
        return !string.IsNullOrWhiteSpace(value);
    }

    private static void WriteUsage ()
    {
        Console.Error.WriteLine(
            "usage: Ucli.SchemaGenerator --output <schemas-dir> "
            + "[--repository-root <repo-root>] [--package-version <version>]");
    }

    private sealed record SchemaGeneratorArguments (
        AbsolutePath OutputRoot,
        string? PackageVersion,
        AbsolutePath RepositoryRoot);
}
