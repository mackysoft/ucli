using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
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
    public static Sha256Digest Calculate (
        int schemaVersion,
        ResolvedBuildInputs inputs,
        ResolvedBuildRunner runner,
        ResolvedBuildPolicy policy)
    {
        var canonical = new CanonicalBuildProfile(
            SchemaVersion: schemaVersion,
            Inputs: CanonicalBuildInputs.From(inputs),
            Runner: CanonicalBuildRunner.From(runner),
            Policy: CanonicalBuildPolicy.From(policy));

        var json = JsonSerializer.Serialize(canonical, SerializerOptions);
        return Sha256Digest.Compute(Encoding.UTF8.GetBytes(json));
    }

    private sealed record CanonicalBuildProfile (
        int SchemaVersion,
        CanonicalBuildInputs Inputs,
        CanonicalBuildRunner Runner,
        CanonicalBuildPolicy Policy);

    private sealed record CanonicalBuildInputs (
        string Kind,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? BuildTarget,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CanonicalBuildScenes? Scenes,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CanonicalBuildOptions? Options,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Path)
    {
        public static CanonicalBuildInputs From (ResolvedBuildInputs inputs)
        {
            if (inputs is ResolvedBuildInputs.UnityBuildProfile unityBuildProfileInputs)
            {
                return new CanonicalBuildInputs(
                    TextVocabulary.GetText(inputs.Kind),
                    null,
                    null,
                    null,
                    unityBuildProfileInputs.Path.Value);
            }

            var explicitInputs = (ResolvedBuildInputs.Explicit)inputs;
            return new CanonicalBuildInputs(
                TextVocabulary.GetText(inputs.Kind),
                TextVocabulary.GetText(explicitInputs.BuildTarget),
                CanonicalBuildScenes.From(explicitInputs.Scenes),
                new CanonicalBuildOptions(explicitInputs.Options.Development),
                null);
        }
    }

    private sealed record CanonicalBuildScenes (
        string Source,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SceneAssetPath>? Paths)
    {
        public static CanonicalBuildScenes From (ResolvedBuildScenes scenes)
        {
            var paths = scenes is ResolvedBuildScenes.Explicit explicitScenes
                ? explicitScenes.Paths
                : null;
            return new CanonicalBuildScenes(TextVocabulary.GetText(scenes.Source), paths);
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
            if (runner is ResolvedBuildRunner.ExecuteMethod executeMethodRunner)
            {
                return new CanonicalBuildRunner(
                    TextVocabulary.GetText(runner.Kind),
                    executeMethodRunner.Method,
                    CanonicalBuildRunnerInvocation.From(executeMethodRunner.Invocation));
            }

            return new CanonicalBuildRunner(
                TextVocabulary.GetText(runner.Kind),
                Method: null,
                Invocation: null);
        }
    }

    private sealed record CanonicalBuildRunnerInvocation (
        IReadOnlyDictionary<string, string> Arguments,
        CanonicalBuildRunnerEnvironment Environment)
    {
        public static CanonicalBuildRunnerInvocation From (ResolvedBuildRunnerInvocation invocation)
        {
            var arguments = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in invocation.Arguments)
            {
                arguments.Add(pair.Key, pair.Value);
            }

            var invocationEnv = invocation.Environment;
            return new CanonicalBuildRunnerInvocation(
                arguments,
                new CanonicalBuildRunnerEnvironment(
                    invocationEnv.Variables,
                    invocationEnv.Secrets));
        }
    }

    private sealed record CanonicalBuildRunnerEnvironment (
        IReadOnlyList<string> Variables,
        IReadOnlyList<string> Secrets);

    private sealed record CanonicalBuildPolicy (
        CanonicalBuildRuntimePolicy Runtime,
        string ProjectMutationMode)
    {
        public static CanonicalBuildPolicy From (ResolvedBuildPolicy policy)
        {
            return new CanonicalBuildPolicy(
                CanonicalBuildRuntimePolicy.From(policy.Runtime),
                TextVocabulary.GetText(policy.ProjectMutationMode));
        }
    }

    private sealed record CanonicalBuildRuntimePolicy (
        IReadOnlyList<string> AllowedExecutionModes,
        IReadOnlyList<string> AllowedEditorModes)
    {
        public static CanonicalBuildRuntimePolicy From (ResolvedBuildRuntimePolicy runtime)
        {
            return new CanonicalBuildRuntimePolicy(
                runtime.AllowedExecutionModes.Select(TextVocabulary.GetText).ToArray(),
                runtime.AllowedEditorModes.Select(TextVocabulary.GetText).ToArray());
        }
    }
}
