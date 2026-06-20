using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Calculates canonical build profile digests. </summary>
internal static class BuildProfileDigestCalculator
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary> Calculates the canonical digest for one resolved build profile content. </summary>
    public static string Calculate (
        int schemaVersion,
        ResolvedBuildInputs inputs,
        ResolvedBuildRunner runner,
        ResolvedBuildPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(policy);

        var canonical = new CanonicalBuildProfile(
            SchemaVersion: schemaVersion,
            Inputs: CanonicalBuildInputs.From(inputs),
            Runner: CanonicalBuildRunner.From(runner),
            Policy: CanonicalBuildPolicy.From(policy));

        var json = JsonSerializer.Serialize(canonical, SerializerOptions);
        return Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(json));
    }

    private sealed record CanonicalBuildProfile (
        int SchemaVersion,
        CanonicalBuildInputs Inputs,
        CanonicalBuildRunner Runner,
        CanonicalBuildPolicy Policy);

    private sealed record CanonicalBuildInputs (
        string Kind,
        string BuildTarget,
        CanonicalBuildScenes Scenes,
        CanonicalBuildOptions Options)
    {
        public static CanonicalBuildInputs From (ResolvedBuildInputs inputs)
        {
            return new CanonicalBuildInputs(
                ContractLiteralCodec.ToValue(inputs.Kind),
                inputs.BuildTarget.StableName,
                CanonicalBuildScenes.From(inputs.Scenes),
                new CanonicalBuildOptions(inputs.Options.Development));
        }
    }

    private sealed record CanonicalBuildScenes (
        string Source,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Paths)
    {
        public static CanonicalBuildScenes From (ResolvedBuildScenes scenes)
        {
            var paths = scenes.Source == BuildProfileSceneSource.Explicit
                ? scenes.Paths
                : null;
            return new CanonicalBuildScenes(ContractLiteralCodec.ToValue(scenes.Source), paths);
        }
    }

    private sealed record CanonicalBuildOptions (bool Development);

    private sealed record CanonicalBuildRunner (
        string Kind,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Method,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CanonicalBuildRunnerInvocation? Invocation)
    {
        public static CanonicalBuildRunner From (ResolvedBuildRunner runner)
        {
            return runner.Kind == BuildProfileRunnerKind.ExecuteMethod
                ? new CanonicalBuildRunner(
                    ContractLiteralCodec.ToValue(runner.Kind),
                    runner.Method,
                    CanonicalBuildRunnerInvocation.From(runner.Invocation))
                : new CanonicalBuildRunner(
                    ContractLiteralCodec.ToValue(runner.Kind),
                    Method: null,
                    Invocation: null);
        }
    }

    private sealed record CanonicalBuildRunnerInvocation (
        IReadOnlyDictionary<string, string> Arguments,
        IReadOnlyList<string> Environment)
    {
        public static CanonicalBuildRunnerInvocation From (ResolvedBuildRunnerInvocation invocation)
        {
            var arguments = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in invocation.Arguments)
            {
                arguments.Add(pair.Key, pair.Value);
            }

            return new CanonicalBuildRunnerInvocation(
                arguments,
                invocation.EnvironmentNames);
        }
    }

    private sealed record CanonicalBuildPolicy (
        CanonicalBuildRuntimePolicy Runtime,
        string ProjectMutationMode)
    {
        public static CanonicalBuildPolicy From (ResolvedBuildPolicy policy)
        {
            return new CanonicalBuildPolicy(
                CanonicalBuildRuntimePolicy.From(policy.Runtime),
                ContractLiteralCodec.ToValue(policy.ProjectMutationMode));
        }
    }

    private sealed record CanonicalBuildRuntimePolicy (
        IReadOnlyList<string> AllowedExecutionModes,
        IReadOnlyList<string> AllowedEditorModes)
    {
        public static CanonicalBuildRuntimePolicy From (ResolvedBuildRuntimePolicy runtime)
        {
            return new CanonicalBuildRuntimePolicy(
                runtime.AllowedExecutionModes.Select(ContractLiteralCodec.ToValue).ToArray(),
                runtime.AllowedEditorModes.Select(ContractLiteralCodec.ToValue).ToArray());
        }
    }
}
