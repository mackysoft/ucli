using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Schemas;
using MackySoft.Ucli.Hosting.Cli.Schemas;
using MackySoft.Ucli.Hosting.Cli.Testing;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Schemas;

public sealed class UcliStaticSchemaPayloadContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void PublicObjectRootSchemas_RejectNull ()
    {
        var schemaSet = UcliStaticSchemaSetLoader.Load(
            AbsolutePath.Parse(TestRepositoryPaths.GetFullPath("schemas")));
        var failures = schemaSet.Manifest.Schemas
            .Where(static entry => entry.Kind is
                UcliStaticSchemaKind.SchemaSetMetadata
                or UcliStaticSchemaKind.CliOutputEnvelope
                or UcliStaticSchemaKind.CliOutputPayload
                or UcliStaticSchemaKind.CommonDefinition)
            .Where(entry => BuildSchema(schemaSet, entry.Name)
                .Evaluate(JsonSerializer.SerializeToElement<object?>(null))
                .IsValid)
            .Select(static entry => entry.Name)
            .ToArray();

        Assert.True(
            failures.Length == 0,
            "The following public object roots accepted null: " + string.Join(", ", failures));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void CommonReferenceSchemas_AgreeWithActualSerializedContracts ()
    {
        var schemaSet = UcliStaticSchemaSetLoader.Load(
            AbsolutePath.Parse(TestRepositoryPaths.GetFullPath("schemas")));
        var artifactSchema = BuildSchema(schemaSet, "common.artifact-ref");
        var executionSchema = BuildSchema(schemaSet, "common.execution-ref");
        ArtifactRef terminalRecord = new PathArtifactRef(
            new ArtifactKind("programRun.terminalRecord"),
            new ArtifactMediaType("application/json"),
            new ArtifactPath(".ucli/local/program-runs/terminal-record.json"),
            Sha256Digest.Parse(new string('a', 64)),
            sizeBytes: 128,
            new DateTimeOffset(2026, 7, 28, 12, 0, 0, TimeSpan.Zero));
        ArtifactRef remotelyLocatedArtifact = new UriArtifactRef(
            new ArtifactKind("report"),
            new ArtifactMediaType("application/json"),
            new ArtifactUri("https://artifacts.example.test/reports/report.json"),
            Sha256Digest.Parse(new string('c', 64)),
            sizeBytes: 256,
            new DateTimeOffset(2026, 7, 28, 12, 1, 0, TimeSpan.Zero));
        ArtifactRef multiplyLocatedEditorLog = new PathAndUriArtifactRef(
            new ArtifactKind("testRun.editorLog"),
            new ArtifactMediaType("text/plain; charset=UTF-8"),
            new ArtifactPath(".ucli/local/test-runs/editor.log"),
            new ArtifactUri("https://artifacts.example.test/test-runs/editor.log"),
            Sha256Digest.Parse(new string('d', 64)),
            sizeBytes: 512,
            new DateTimeOffset(2026, 7, 28, 12, 2, 0, TimeSpan.Zero));
        var active = new ActiveExecutionRef(
            new ExecutionKind("programRun"),
            Guid.Parse("8b8b657d-f631-4509-af40-88f6af40f53b"),
            Sha256Digest.Parse(new string('b', 64)),
            new ExecutionState("running"),
            new ExecutionStatusLocator(
                ".ucli/local/program-runs/8b8b657df6314509af4088f6af40f53b/status.json"));
        var recovery = new RecoveryExecutionRef(
            active.Kind,
            active.Id,
            active.DefinitionDigest,
            new ExecutionState("recovering"),
            active.StatusLocator);
        var terminal = new TerminalExecutionRef(
            active.Kind,
            active.Id,
            active.DefinitionDigest,
            new ExecutionState("completed"),
            statusLocator: null,
            terminalRecord);

        var artifactJson = JsonSerializer.SerializeToElement<ArtifactRef>(
            terminalRecord,
            IpcJsonSerializerOptions.StrictPropertyNames);
        var remotelyLocatedArtifactJson = JsonSerializer.SerializeToElement<ArtifactRef>(
            remotelyLocatedArtifact,
            IpcJsonSerializerOptions.StrictPropertyNames);
        var multiplyLocatedEditorLogJson = JsonSerializer.SerializeToElement<ArtifactRef>(
            multiplyLocatedEditorLog,
            IpcJsonSerializerOptions.StrictPropertyNames);
        var activeJson = JsonSerializer.SerializeToElement<ExecutionRef>(
            active,
            IpcJsonSerializerOptions.StrictPropertyNames);
        var recoveryJson = JsonSerializer.SerializeToElement<ExecutionRef>(
            recovery,
            IpcJsonSerializerOptions.StrictPropertyNames);
        var terminalJson = JsonSerializer.SerializeToElement<ExecutionRef>(
            terminal,
            IpcJsonSerializerOptions.StrictPropertyNames);

        Assert.True(artifactSchema.Evaluate(artifactJson).IsValid);
        Assert.True(artifactSchema.Evaluate(remotelyLocatedArtifactJson).IsValid);
        Assert.True(artifactSchema.Evaluate(multiplyLocatedEditorLogJson).IsValid);
        Assert.True(executionSchema.Evaluate(activeJson).IsValid);
        Assert.True(executionSchema.Evaluate(recoveryJson).IsValid);
        Assert.True(executionSchema.Evaluate(terminalJson).IsValid);

        var artifactWithoutLocator = JsonNode.Parse(artifactJson.GetRawText())!.AsObject();
        Assert.True(artifactWithoutLocator.Remove("path"));
        Assert.False(artifactSchema
            .Evaluate(JsonSerializer.SerializeToElement(artifactWithoutLocator))
            .IsValid);

        var artifactWithoutLocationKind =
            JsonNode.Parse(artifactJson.GetRawText())!.AsObject();
        Assert.True(artifactWithoutLocationKind.Remove("locationKind"));
        Assert.False(artifactSchema
            .Evaluate(JsonSerializer.SerializeToElement(artifactWithoutLocationKind))
            .IsValid);

        var activeWithTerminalRecord = JsonNode.Parse(activeJson.GetRawText())!.AsObject();
        activeWithTerminalRecord["terminalRecordRef"] =
            JsonNode.Parse(artifactJson.GetRawText());
        Assert.False(executionSchema
            .Evaluate(JsonSerializer.SerializeToElement(activeWithTerminalRecord))
            .IsValid);

        var activeWithoutStatusLocator = JsonNode.Parse(activeJson.GetRawText())!.AsObject();
        Assert.True(activeWithoutStatusLocator.Remove("statusLocator"));
        Assert.False(executionSchema
            .Evaluate(JsonSerializer.SerializeToElement(activeWithoutStatusLocator))
            .IsValid);

        var activeWithNullStatusLocator = JsonNode.Parse(activeJson.GetRawText())!.AsObject();
        activeWithNullStatusLocator["statusLocator"] = null;
        Assert.False(executionSchema
            .Evaluate(JsonSerializer.SerializeToElement(activeWithNullStatusLocator))
            .IsValid);

        var terminalWithoutRecord = JsonNode.Parse(terminalJson.GetRawText())!.AsObject();
        Assert.True(terminalWithoutRecord.Remove("terminalRecordRef"));
        Assert.False(executionSchema
            .Evaluate(JsonSerializer.SerializeToElement(terminalWithoutRecord))
            .IsValid);

        var terminalWithoutStatusLocator = JsonNode.Parse(terminalJson.GetRawText())!.AsObject();
        Assert.True(terminalWithoutStatusLocator.Remove("statusLocator"));
        Assert.False(executionSchema
            .Evaluate(JsonSerializer.SerializeToElement(terminalWithoutStatusLocator))
            .IsValid);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void CommonArtifactReferenceSchema_AgreesWithRuntimeLexicalRejections ()
    {
        var schemaSet = UcliStaticSchemaSetLoader.Load(
            AbsolutePath.Parse(TestRepositoryPaths.GetFullPath("schemas")));
        var artifactSchema = BuildSchema(schemaSet, "common.artifact-ref");
        ArtifactRef artifact = new PathAndUriArtifactRef(
            new ArtifactKind(TextVocabulary.GetText(ScreenshotArtifactKind.Screenshot)),
            new ArtifactMediaType(TextVocabulary.GetText(ScreenshotArtifactMediaType.Png)),
            new ArtifactPath(".ucli/local/screenshots/game.png"),
            new ArtifactUri("https://artifacts.example.test/screenshots/game.png"),
            Sha256Digest.Parse(new string('a', 64)),
            sizeBytes: 128,
            new DateTimeOffset(2026, 7, 28, 12, 0, 0, TimeSpan.Zero));
        var serialized = JsonSerializer.SerializeToElement(
            artifact,
            IpcJsonSerializerOptions.StrictPropertyNames);

        var invalidPath = JsonNode.Parse(serialized.GetRawText())!.AsObject();
        invalidPath["path"] = ".ucli/local/\u0080game.png";
        AssertRejectedBySchemaAndStrictDeserializer<ArtifactRef>(
            artifactSchema,
            invalidPath);

        var invalidPublicationTime =
            JsonNode.Parse(serialized.GetRawText())!.AsObject();
        invalidPublicationTime["createdAtUtc"] =
            "2026-99-99T99:99:99.0000000Z";
        AssertRejectedBySchemaAndStrictDeserializer<ArtifactRef>(
            artifactSchema,
            invalidPublicationTime);

        var finalLineFeedCases = new (string PropertyName, string Value)[]
        {
            ("path", ".ucli/local/screenshots/game.png\n"),
            ("uri", "https://artifacts.example.test/screenshots/game.png\n"),
            (
                "kind",
                TextVocabulary.GetText(ScreenshotArtifactKind.Screenshot) + "\n"),
            (
                "mediaType",
                TextVocabulary.GetText(ScreenshotArtifactMediaType.Png) + "\n"),
        };
        foreach (var testCase in finalLineFeedCases)
        {
            var invalid = JsonNode.Parse(serialized.GetRawText())!.AsObject();
            invalid[testCase.PropertyName] = testCase.Value;
            AssertRejectedBySchemaAndStrictDeserializer<ArtifactRef>(
                artifactSchema,
                invalid);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void CommonExecutionReferenceSchema_AgreesWithRuntimeLexicalRejections ()
    {
        var schemaSet = UcliStaticSchemaSetLoader.Load(
            AbsolutePath.Parse(TestRepositoryPaths.GetFullPath("schemas")));
        var executionSchema = BuildSchema(schemaSet, "common.execution-ref");
        ExecutionRef execution = new ActiveExecutionRef(
            new ExecutionKind("programRun"),
            Guid.Parse("8b8b657d-f631-4509-af40-88f6af40f53b"),
            Sha256Digest.Parse(new string('b', 64)),
            new ExecutionState("running"),
            new ExecutionStatusLocator(
                ".ucli/local/program-runs/8b8b657df6314509af4088f6af40f53b/status.json"));
        var serialized = JsonSerializer.SerializeToElement(
            execution,
            IpcJsonSerializerOptions.StrictPropertyNames);
        var finalLineFeedCases = new (string PropertyName, string Value)[]
        {
            ("kind", execution.Kind.Value + "\n"),
            ("state", execution.State.Value + "\n"),
            ("statusLocator", execution.StatusLocator!.Value + "\n"),
        };

        foreach (var testCase in finalLineFeedCases)
        {
            var invalid = JsonNode.Parse(serialized.GetRawText())!.AsObject();
            invalid[testCase.PropertyName] = testCase.Value;
            AssertRejectedBySchemaAndStrictDeserializer<ExecutionRef>(
                executionSchema,
                invalid);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void CliOutputEnvelope_RejectsNullScalarAndArrayPayloads ()
    {
        var schemaSet = UcliStaticSchemaSetLoader.Load(
            AbsolutePath.Parse(TestRepositoryPaths.GetFullPath("schemas")));
        var envelopeSchema = BuildSchema(schemaSet, "cli-output.envelope");
        var golden = CliOutputGoldenFiles.ReadAllDocuments().First();
        var invalidPayloads = new JsonNode?[]
        {
            null,
            JsonValue.Create("text"),
            JsonValue.Create(42),
            new JsonArray(),
        };

        foreach (var invalidPayload in invalidPayloads)
        {
            var instance = JsonNode.Parse(golden.Root.GetRawText())!.AsObject();
            instance["payload"] = invalidPayload;

            var result = envelopeSchema.Evaluate(
                JsonSerializer.SerializeToElement(instance));

            Assert.False(
                result.IsValid,
                $"The CLI output envelope accepted payload '{invalidPayload?.ToJsonString() ?? "null"}'.");
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void EveryCliOutputGolden_AgreesWithEnvelopeAndCommandStatusSchemas ()
    {
        var schemaSet = UcliStaticSchemaSetLoader.Load(
            AbsolutePath.Parse(TestRepositoryPaths.GetFullPath("schemas")));
        var envelopeSchema = BuildSchema(schemaSet, "cli-output.envelope");
        var failures = new List<string>();

        foreach (var golden in CliOutputGoldenFiles.ReadAllDocuments())
        {
            try
            {
                var envelopeFailure = EvaluateGolden(
                    envelopeSchema,
                    golden.Root,
                    golden.RepositoryRelativePath,
                    "cli-output.envelope");
                if (envelopeFailure != null)
                {
                    failures.Add(envelopeFailure);
                    continue;
                }

                var payloadFailure = EvaluateGoldenPayload(schemaSet, golden);
                if (payloadFailure != null)
                {
                    failures.Add(payloadFailure);
                }
            }
            catch (Exception exception)
            {
                failures.Add($"{golden.RepositoryRelativePath}:{Environment.NewLine}{exception}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void OperationContractViolationSchema_RejectsLifecycleOnlyPartialApplicationState ()
    {
        var golden = CliOutputGoldenFiles.ReadAllDocuments().Single(
            static document => document.RepositoryRelativePath.EndsWith(
                "call/contract-violation.json",
                StringComparison.Ordinal));
        var payload = JsonNode
            .Parse(golden.Root.GetProperty("payload").GetRawText())!
            .AsObject();
        payload["contractViolations"]![0]!["applicationState"] =
            TextVocabulary.GetText(
                ExecutionApplicationState.PartiallyApplied);
        var schemaSet = UcliStaticSchemaSetLoader.Load(
            AbsolutePath.Parse(TestRepositoryPaths.GetFullPath("schemas")));
        var schema = BuildSchema(
            schemaSet,
            "cli-output.payload.call.error");

        var result = schema.Evaluate(
            JsonSerializer.SerializeToElement(payload));

        Assert.False(result.IsValid);
    }

    [Theory]
    [MemberData(nameof(GetVerdicts))]
    [Trait("Size", "Medium")]
    public void TestRunCompletedPayload_AgreesWithPublishedSchema (Verdict verdict)
    {
        var artifactsDirectory = AbsolutePath.Parse(
            Path.Combine(Path.GetTempPath(), "ucli-test-run-schema-contract", verdict.ToString()));
        var artifactsSession = TestArtifactPaths.CreateSession(
            RunIdTestValues.Test,
            artifactsDirectory.Value);
        var serviceResult = TestRunResultTestValues.CreateCompleted(
            verdict,
            artifactsSession);
        var commandResult = TestRunCommandResultFactory.Create(serviceResult);
        var payload = JsonSerializer.SerializeToElement(
            commandResult.Payload,
            CliOutputJsonSerializerOptions.Default);
        var schemaSet = UcliStaticSchemaSetLoader.Load(
            AbsolutePath.Parse(TestRepositoryPaths.GetFullPath("schemas")));
        var schema = BuildSchema(schemaSet, "cli-output.payload.test.run.ok");

        var evaluation = schema.Evaluate(payload);

        Assert.True(
            evaluation.IsValid,
            $"The published test-run payload schema rejected completed verdict '{TextVocabulary.GetText(verdict)}':"
            + Environment.NewLine
            + JsonSerializer.Serialize(evaluation));
    }

    public static TheoryData<Verdict> GetVerdicts ()
    {
        return new TheoryData<Verdict>
        {
            Verdict.Pass,
            Verdict.Fail,
            Verdict.Incomplete,
        };
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void GeneratedPublicContracts_RejectMissingAlwaysEmittedProperties ()
    {
        var schemaSet = UcliStaticSchemaSetLoader.Load(
            AbsolutePath.Parse(TestRepositoryPaths.GetFullPath("schemas")));
        var goldens = CliOutputGoldenFiles.ReadAllDocuments();
        var cases = new MissingPropertyCase[]
        {
            new(
                "cli-output.envelope",
                "status/success.json",
                ContainerProperty: null,
                MissingProperty: "errors"),
            new(
                "cli-output.payload.build.run.ok",
                "build-run/success.json",
                ContainerProperty: "payload",
                MissingProperty: "verdict"),
            new(
                "cli-output.payload.test.run.error",
                "test-run/invalid-mode.json",
                ContainerProperty: "payload",
                MissingProperty: "errorKind"),
        };

        foreach (var testCase in cases)
        {
            var golden = goldens.Single(document =>
                document.RepositoryRelativePath.EndsWith(
                    testCase.GoldenPathSuffix,
                    StringComparison.Ordinal));
            var source = testCase.ContainerProperty == null
                ? golden.Root
                : golden.Root.GetProperty(testCase.ContainerProperty);
            var instance = JsonNode.Parse(source.GetRawText())!.AsObject();
            Assert.True(instance.Remove(testCase.MissingProperty));
            var schema = BuildSchema(schemaSet, testCase.LogicalName);

            var result = schema.Evaluate(JsonSerializer.SerializeToElement(instance));

            Assert.False(
                result.IsValid,
                $"'{testCase.LogicalName}' accepted an instance without '{testCase.MissingProperty}'.");
        }
    }

    private static string? EvaluateGoldenPayload (
        UcliStaticSchemaSet schemaSet,
        CliOutputGoldenFiles.GoldenDocument golden)
    {
        var command = ReadRequiredString(golden.Root, "command");
        var status = ReadRequiredString(golden.Root, "status");
        var logicalName = "cli-output.payload." + command + "." + status;
        var schema = BuildSchema(schemaSet, logicalName);
        return EvaluateGolden(
            schema,
            golden.Root.GetProperty("payload"),
            golden.RepositoryRelativePath,
            logicalName);
    }

    private static string? EvaluateGolden (
        global::Json.Schema.JsonSchema schema,
        JsonElement instance,
        string goldenPath,
        string logicalName)
    {
        var result = schema.Evaluate(
            instance,
            new EvaluationOptions
            {
                OutputFormat = OutputFormat.Hierarchical,
            });
        if (result.IsValid)
        {
            return null;
        }

        return $"{goldenPath} was rejected by '{logicalName}':"
            + Environment.NewLine
            + JsonSerializer.Serialize(result);
    }

    private static global::Json.Schema.JsonSchema BuildSchema (
        UcliStaticSchemaSet schemaSet,
        string logicalName)
    {
        var artifact = Assert.IsType<UcliStaticSchemaArtifact>(schemaSet.Find(logicalName));
        return global::Json.Schema.JsonSchema.Build(
            artifact.Document,
            new BuildOptions
            {
                SchemaRegistry = new SchemaRegistry
                {
                    Fetch = null!,
                },
            });
    }

    private static void AssertRejectedBySchemaAndStrictDeserializer<TContract> (
        global::Json.Schema.JsonSchema schema,
        JsonObject instance)
    {
        var json = JsonSerializer.SerializeToElement(instance);

        Assert.False(schema.Evaluate(json).IsValid);
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<TContract>(
                json,
                IpcJsonSerializerOptions.StrictPropertyNames));
    }

    private static string ReadRequiredString (
        JsonElement element,
        string propertyName)
    {
        var property = element.GetProperty(propertyName);
        Assert.Equal(JsonValueKind.String, property.ValueKind);
        return Assert.IsType<string>(property.GetString());
    }

    private readonly record struct MissingPropertyCase (
        string LogicalName,
        string GoldenPathSuffix,
        string? ContainerProperty,
        string MissingProperty);
}
