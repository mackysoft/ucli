using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Contracts.Recording;
using MackySoft.Ucli.Hosting.Cli.Play.Contracts;
using MackySoft.Ucli.Hosting.Cli.Recording;

namespace MackySoft.Ucli.Hosting.Cli.Common.Contracts;

/// <summary> Provides JSON serializer options for public CLI output contracts. </summary>
internal static class CliOutputJsonSerializerOptions
{
    private static readonly JsonNamingPolicy CliPropertyNamingPolicy = JsonNamingPolicy.CamelCase;

    /// <summary> Gets the serializer options shared by command results and stream entries. </summary>
    public static JsonSerializerOptions Default { get; } = CreateDefault();

    private static JsonSerializerOptions CreateDefault ()
    {
        return new JsonSerializerOptions
        {
            Converters =
            {
                new UcliNonNullJsonObjectJsonConverterFactory(),
                new VocabularyJsonConverterFactory(),
                new MackySoft.AgentSkills.Shared.Text.ContractLiteralJsonConverterFactory(),
            },
            PropertyNamingPolicy = CliPropertyNamingPolicy,
            TypeInfoResolver = CreateTypeInfoResolver(),
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
    }

    /// <summary>
    /// Creates metadata for the write-only public CLI contract.
    /// The same metadata drives serialization and Schema generation so that an
    /// unconditionally written property is also required by the generated Schema.
    /// </summary>
    private static DefaultJsonTypeInfoResolver CreateTypeInfoResolver ()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(static typeInfo =>
        {
            ArtifactRefJsonPolymorphismConfigurator.TryConfigure(typeInfo);
            ExecutionRefJsonPolymorphismConfigurator.TryConfigure(typeInfo);
            GameViewRecordingJsonPolymorphismConfigurator.TryConfigure(typeInfo);
            GameViewRecordingStatusCommandPayload.TryConfigure(typeInfo);
            LifecycleExecutionTerminalRecordJsonPolymorphismConfigurator.TryConfigure(typeInfo);
        });
        resolver.Modifiers.Add(ConfigureCommandErrorPayload);
        resolver.Modifiers.Add(ConfigurePlayTransitionErrorPayload);
        resolver.Modifiers.Add(ConfigureAssuranceUnions);
        resolver.Modifiers.Add(ConfigureAssuranceEvidence);
        resolver.Modifiers.Add(MarkAlwaysWrittenPropertiesRequired);
        return resolver;
    }

    private static void ConfigureAssuranceUnions (JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type == typeof(VerifyProfileOutput))
        {
            typeInfo.PolymorphismOptions = CreateClosedPolymorphism(
                "source",
                new JsonDerivedType(
                    typeof(BuiltInVerifyProfileOutput),
                    TextVocabulary.GetText(VerifyProfileSource.BuiltIn)),
                new JsonDerivedType(
                    typeof(FileVerifyProfileOutput),
                    TextVocabulary.GetText(VerifyProfileSource.File)));
            return;
        }

        if (typeInfo.Type == typeof(ReadyClaimValidityOutput))
        {
            typeInfo.PolymorphismOptions = CreateClosedPolymorphism(
                "kind",
                new JsonDerivedType(
                    typeof(ProbeOnlyReadyClaimValidityOutput),
                    TextVocabulary.GetText(ReadyValidityKind.ProbeOnly)),
                new JsonDerivedType(
                    typeof(SessionBoundReadyClaimValidityOutput),
                    TextVocabulary.GetText(ReadyValidityKind.SessionBound)));
        }
    }

    private static void ConfigureAssuranceEvidence (JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type == typeof(BuildEvidenceOutput))
        {
            typeInfo.PolymorphismOptions = CreateClosedPolymorphism(
                GetJsonPropertyName(nameof(BuildEvidenceOutput.Kind)),
                new JsonDerivedType(typeof(BuildProfileEvidenceOutput), TextVocabulary.GetText(BuildEvidenceKind.BuildProfile)),
                new JsonDerivedType(typeof(BuildInputEvidenceOutput), TextVocabulary.GetText(BuildEvidenceKind.BuildInput)),
                new JsonDerivedType(typeof(BuildLifecycleEvidenceOutput), TextVocabulary.GetText(BuildEvidenceKind.ReadyLifecycleSnapshot)),
                new JsonDerivedType(typeof(BuildRunnerEvidenceOutput), TextVocabulary.GetText(BuildEvidenceKind.BuildRunner)),
                new JsonDerivedType(typeof(BuildReportSummaryEvidenceOutput), TextVocabulary.GetText(BuildEvidenceKind.BuildReportSummary)),
                new JsonDerivedType(typeof(BuildSummaryEvidenceOutput), TextVocabulary.GetText(BuildEvidenceKind.BuildSummary)),
                new JsonDerivedType(typeof(BuildRunnerResultEvidenceOutput), TextVocabulary.GetText(BuildEvidenceKind.BuildRunnerResult)),
                new JsonDerivedType(typeof(BuildLogEvidenceOutput), TextVocabulary.GetText(BuildEvidenceKind.BuildLogSummary)),
                new JsonDerivedType(typeof(BuildOutputAccountingEvidenceOutput), TextVocabulary.GetText(BuildEvidenceKind.BuildOutputAccounting)),
                new JsonDerivedType(typeof(BuildOutputManifestEvidenceOutput), TextVocabulary.GetText(BuildEvidenceKind.BuildOutputManifest)),
                new JsonDerivedType(typeof(BuildGenerationEvidenceOutput), TextVocabulary.GetText(BuildEvidenceKind.GenerationSnapshot)),
                new JsonDerivedType(typeof(BuildProjectMutationEvidenceOutput), TextVocabulary.GetText(BuildEvidenceKind.ProjectMutationAudit)));
            return;
        }

        if (typeInfo.Type == typeof(CompileEvidenceOutput))
        {
            typeInfo.PolymorphismOptions = CreateClosedPolymorphism(
                GetJsonPropertyName(nameof(CompileEvidenceOutput.Kind)),
                new JsonDerivedType(typeof(CompileScriptEvidenceOutput), TextVocabulary.GetText(CompileEvidenceKind.ScriptCompilation)),
                new JsonDerivedType(typeof(CompileDomainReloadEvidenceOutput), TextVocabulary.GetText(CompileEvidenceKind.DomainReload)),
                new JsonDerivedType(typeof(CompileLifecycleEvidenceOutput), TextVocabulary.GetText(CompileEvidenceKind.LifecycleSnapshot)));
            return;
        }

        if (typeInfo.Type == typeof(ReadyEvidenceOutput))
        {
            typeInfo.PolymorphismOptions = CreateClosedPolymorphism(
                GetJsonPropertyName(nameof(ReadyEvidenceOutput.Kind)),
                new JsonDerivedType(typeof(ReadyLifecycleEvidenceOutput), TextVocabulary.GetText(ReadyEvidenceKind.LifecycleSnapshot)),
                new JsonDerivedType(typeof(ReadyDecisionEvidenceOutput), TextVocabulary.GetText(ReadyEvidenceKind.ReadinessDecision)),
                new JsonDerivedType(typeof(ReadyReadIndexEvidenceOutput), TextVocabulary.GetText(ReadyEvidenceKind.ReadIndexSummary)));
            return;
        }

        if (typeInfo.Type == typeof(VerifyEvidenceOutput))
        {
            typeInfo.PolymorphismOptions = CreateClosedPolymorphism(
                GetJsonPropertyName(nameof(VerifyEvidenceOutput.Kind)),
                new JsonDerivedType(typeof(VerifyScriptEvidenceOutput), TextVocabulary.GetText(VerifyEvidenceKind.ScriptCompilation)),
                new JsonDerivedType(typeof(VerifyDomainReloadEvidenceOutput), TextVocabulary.GetText(VerifyEvidenceKind.DomainReload)),
                new JsonDerivedType(typeof(VerifyReadyLifecycleEvidenceOutput), TextVocabulary.GetText(VerifyEvidenceKind.ReadyLifecycleSnapshot)),
                new JsonDerivedType(typeof(VerifyCompileLifecycleEvidenceOutput), TextVocabulary.GetText(VerifyEvidenceKind.CompileLifecycleSnapshot)),
                new JsonDerivedType(typeof(VerifyReadinessEvidenceOutput), TextVocabulary.GetText(VerifyEvidenceKind.ReadinessDecision)),
                new JsonDerivedType(typeof(VerifyReadIndexEvidenceOutput), TextVocabulary.GetText(VerifyEvidenceKind.ReadIndexSummary)),
                new JsonDerivedType(typeof(VerifyTestSummaryEvidenceOutput), TextVocabulary.GetText(VerifyEvidenceKind.TestSummary)),
                new JsonDerivedType(typeof(VerifyFromResultMissingEvidenceOutput), TextVocabulary.GetText(VerifyEvidenceKind.FromResultMissing)),
                new JsonDerivedType(typeof(VerifyFromResultSummaryEvidenceOutput), TextVocabulary.GetText(VerifyEvidenceKind.FromResultSummary)));
        }
    }

    private static JsonPolymorphismOptions CreateClosedPolymorphism (
        string discriminatorPropertyName,
        params JsonDerivedType[] derivedTypes)
    {
        var options = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = discriminatorPropertyName,
            IgnoreUnrecognizedTypeDiscriminators = false,
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
        };
        foreach (var derivedType in derivedTypes)
        {
            options.DerivedTypes.Add(derivedType);
        }

        return options;
    }

    private static string GetJsonPropertyName (string clrPropertyName)
    {
        return CliPropertyNamingPolicy.ConvertName(clrPropertyName);
    }

    private static void ConfigureCommandErrorPayload (JsonTypeInfo typeInfo)
    {
        var contractType = typeInfo.Type;
        if (!contractType.IsGenericType
            || contractType.GetGenericTypeDefinition() != typeof(CommandErrorPayload<>))
        {
            return;
        }

        var detailsType = contractType.GetGenericArguments()[0];
        var emptyType = typeof(EmptyCommandErrorPayload<>).MakeGenericType(detailsType);
        var options = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = "payloadKind",
            IgnoreUnrecognizedTypeDiscriminators = false,
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
        };
        options.DerivedTypes.Add(new JsonDerivedType(
            emptyType,
            TextVocabulary.GetText(CommandErrorPayloadKind.Empty)));
        options.DerivedTypes.Add(new JsonDerivedType(
            detailsType,
            TextVocabulary.GetText(CommandErrorPayloadKind.Detailed)));
        typeInfo.PolymorphismOptions = options;
    }

    private static void ConfigurePlayTransitionErrorPayload (
        JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type != typeof(PlayTransitionErrorCommandPayload))
        {
            return;
        }

        typeInfo.PolymorphismOptions = CreateClosedPolymorphism(
            "payloadKind",
            new JsonDerivedType(
                typeof(EmptyPlayTransitionErrorCommandPayload),
                TextVocabulary.GetText(
                    PlayTransitionErrorPayloadKind.Empty)),
            new JsonDerivedType(
                typeof(PlayTransitionStartErrorCommandPayload),
                TextVocabulary.GetText(
                    PlayTransitionErrorPayloadKind.Start)),
            new JsonDerivedType(
                typeof(PlayTransitionFailureErrorCommandPayload),
                TextVocabulary.GetText(
                    PlayTransitionErrorPayloadKind.TransitionFailure)),
            new JsonDerivedType(
                typeof(PlayTerminalFailureErrorCommandPayload),
                TextVocabulary.GetText(
                    PlayTransitionErrorPayloadKind.TerminalFailure)),
            new JsonDerivedType(
                typeof(PlayTerminalPublicationFailureErrorCommandPayload),
                TextVocabulary.GetText(
                    PlayTransitionErrorPayloadKind
                        .TerminalPublicationFailure)));
    }

    /// <summary>
    /// Marks properties without a serialization condition as required.
    /// System.Text.Json requires a setter before it accepts required metadata, including
    /// for output DTOs whose public contract is intentionally getter-only. The throwing
    /// setter makes this output-only boundary explicit without changing the DTO surface.
    /// </summary>
    private static void MarkAlwaysWrittenPropertiesRequired (JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        foreach (var property in typeInfo.Properties)
        {
            if (property.Get != null
                && !property.IsExtensionData
                && property.ShouldSerialize == null)
            {
                property.Set ??= ThrowOutputContractDeserialization;
                property.IsRequired = true;
            }
        }
    }

    private static void ThrowOutputContractDeserialization (
        object _,
        object? __)
    {
        throw new NotSupportedException(
            "CLI output serializer metadata does not support deserialization.");
    }
}
