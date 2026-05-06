using MackySoft.Ucli.Skills.Digests;
using MackySoft.Ucli.Skills.Generation;
using MackySoft.Ucli.Skills.Hosts.Official;
using MackySoft.Ucli.Skills.Manifests;
using MackySoft.Ucli.Skills.Sources;

namespace MackySoft.Ucli.SkillGenerator;

internal static class Program
{
    /// <summary> Generates canonical SKILL package files from source definitions. </summary>
    /// <param name="args"> The command-line arguments passed to the generator. </param>
    /// <returns> The process exit code. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="args" /> is <see langword="null" />. </exception>
    private static async Task<int> Main (string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (!TryParseArgs(args, out var definitionsRoot, out var outputRoot))
        {
            WriteUsage();
            return 2;
        }

        using var cancellationSource = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationSource.Cancel();
        };

        var hostAdapters = OfficialSkillHostAdapters.CreateSet();
        var generationService = new SkillPackageGenerationService(
            new SkillSourceDefinitionReader(),
            hostAdapters,
            new SkillDigestCalculator(),
            new SkillManifestJsonSerializer());

        var packagesResult = await generationService.GenerateAllAsync(definitionsRoot, cancellationSource.Token).ConfigureAwait(false);
        if (!packagesResult.IsSuccess)
        {
            Console.Error.WriteLine(packagesResult.Failure!.Message);
            return 1;
        }

        var writeResult = await new CanonicalSkillPackageWriter().WriteAllAsync(
            packagesResult.Value!,
            outputRoot,
            cleanOutputRoot: true,
            cancellationSource.Token).ConfigureAwait(false);
        if (!writeResult.IsSuccess)
        {
            Console.Error.WriteLine(writeResult.Failure!.Message);
            return 1;
        }

        Console.WriteLine($"Generated canonical skills: {writeResult.Value}");
        return 0;
    }

    private static bool TryParseArgs (
        string[] args,
        out string definitionsRoot,
        out string outputRoot)
    {
        definitionsRoot = string.Empty;
        outputRoot = string.Empty;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--definitions-root":
                    if (!TryReadOptionValue(args, ref i, out definitionsRoot))
                    {
                        return false;
                    }
                    break;

                case "--output":
                    if (!TryReadOptionValue(args, ref i, out outputRoot))
                    {
                        return false;
                    }
                    break;

                default:
                    return false;
            }
        }

        return !string.IsNullOrWhiteSpace(definitionsRoot)
            && !string.IsNullOrWhiteSpace(outputRoot);
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
        Console.Error.WriteLine("usage: Ucli.SkillGenerator --definitions-root <definitions-dir> --output <skills-dir>");
    }
}
