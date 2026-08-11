using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Json.Schema;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Recording;
using MackySoft.Ucli.Contracts.Schemas;
using MackySoft.Ucli.Hosting.Cli.Schemas;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Schemas;

public sealed class GameViewRecordingStaticSchemaContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void RequestSchema_DeclaresBothInputSitesAndCapabilitySource ()
    {
        var schemaSet = LoadSchemaSet();
        var artifact = Assert.IsType<UcliStaticSchemaArtifact>(
            schemaSet.Find("recording.game-view.request"));

        Assert.Equal(UcliStaticSchemaKind.UserInputDocument, artifact.Entry.Kind);
        Assert.Collection(
            artifact.Entry.Usages,
            usage =>
            {
                Assert.Equal("recording.start", usage.Command);
                Assert.Equal(UcliStaticSchemaDelivery.OptionFile, usage.Delivery);
                Assert.Equal("--requestPath", usage.Locator);
            },
            usage =>
            {
                Assert.Equal("recording.start", usage.Command);
                Assert.Equal(UcliStaticSchemaDelivery.StandardInput, usage.Delivery);
                Assert.Null(usage.Locator);
            });
        Assert.Empty(artifact.Entry.StaticDependencies);
        Assert.Equal(["recording.status"], artifact.Entry.DynamicValidationSources);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void RequestSchema_EnforcesTheStaticInputContract ()
    {
        var schema = BuildSchema(LoadSchemaSet(), "recording.game-view.request");
        var validDocuments = new[]
        {
            """
            {
              "schemaVersion": 1,
              "resolution": { "width": 1920, "height": 1080 },
              "frameRate": 30
            }
            """,
            """
            {
              "schemaVersion": 1,
              "resolution": { "width": 1920, "height": 1080 },
              "frameRate": 30,
              "maxDurationSeconds": 120
            }
            """,
            """
            {
              "schemaVersion": 1,
              "resolution": { "width": 1919, "height": 1079 },
              "frameRate": 30
            }
            """,
        };
        var invalidDocuments = new[]
        {
            "null",
            "{}",
            """{"schemaVersion":2,"resolution":{"width":1920,"height":1080},"frameRate":30}""",
            """{"schemaVersion":1,"resolution":{"width":1920},"frameRate":30}""",
            """{"schemaVersion":1,"resolution":{"width":0,"height":1080},"frameRate":30}""",
            """{"schemaVersion":1,"resolution":{"width":1920,"height":1080},"frameRate":0}""",
            """{"schemaVersion":1,"resolution":{"width":1920,"height":1080},"frameRate":30,"maxDurationSeconds":null}""",
            """{"schemaVersion":1,"resolution":{"width":1920,"height":1080},"frameRate":30,"maxDurationSeconds":0}""",
            """{"schemaVersion":1,"resolution":{"width":1920,"height":1080},"frameRate":30,"unknown":true}""",
        };

        Assert.All(validDocuments, json =>
        {
            using var document = JsonDocument.Parse(json);
            Assert.True(schema.Evaluate(document.RootElement).IsValid, json);
        });
        Assert.All(invalidDocuments, json =>
        {
            using var document = JsonDocument.Parse(json);
            Assert.False(schema.Evaluate(document.RootElement).IsValid, json);
        });
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void StatusSchema_DeclaresPositiveLimitsAndFixedTrackFlags ()
    {
        var artifact = Assert.IsType<UcliStaticSchemaArtifact>(
            LoadSchemaSet().Find("cli-output.payload.recording.status.ok"));
        var limits = FindObjectDefinition(artifact.Document, "dimensionMultiple");
        var limitProperties = limits.GetProperty("properties");
        var requiredLimits = limits.GetProperty("required")
            .EnumerateArray()
            .Select(static item => item.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var positiveLimitNames = new[]
        {
            "minimumWidth",
            "maximumWidth",
            "minimumHeight",
            "maximumHeight",
            "dimensionMultiple",
            "minimumFrameRate",
            "maximumFrameRate",
            "defaultMaxDurationSeconds",
            "maximumMaxDurationSeconds",
        };

        Assert.All(positiveLimitNames, name =>
        {
            Assert.Contains(name, requiredLimits);
            var property = limitProperties.GetProperty(name);
            Assert.Equal("integer", property.GetProperty("type").GetString());
            Assert.Equal(1, property.GetProperty("minimum").GetInt32());
        });

        var captureProfile = FindObjectDefinition(artifact.Document, "audio");
        var captureProperties = captureProfile.GetProperty("properties");
        Assert.False(captureProperties.GetProperty("audio").GetProperty("const").GetBoolean());
        Assert.False(captureProperties.GetProperty("alpha").GetProperty("const").GetBoolean());
    }

    [Theory]
    [InlineData("cli-output.payload.recording.start.ok")]
    [InlineData("cli-output.payload.recording.start.error")]
    [InlineData("cli-output.payload.recording.status.ok")]
    [InlineData("cli-output.payload.recording.status.error")]
    [InlineData("cli-output.payload.recording.stop.ok")]
    [InlineData("cli-output.payload.recording.stop.error")]
    [Trait("Size", "Medium")]
    public void RecordingOutputSchemas_EnforceExecutionScalarContract (
        string logicalName)
    {
        var artifact = Assert.IsType<UcliStaticSchemaArtifact>(
            LoadSchemaSet().Find(logicalName));
        var progressDefinitions = FindObjectDefinitions(
                artifact.Document,
                "effectiveMaxDurationSeconds")
            .ToArray();
        Assert.NotEmpty(progressDefinitions);
        Assert.All(progressDefinitions, progress =>
        {
            var progressProperties = progress.GetProperty("properties");
            Assert.Equal(
                1,
                progressProperties
                    .GetProperty("effectiveMaxDurationSeconds")
                    .GetProperty("minimum")
                    .GetInt32());
            Assert.Equal(
                0,
                progressProperties
                    .GetProperty("encodedFrameCount")
                    .GetProperty("minimum")
                    .GetInt32());
            AssertUtcTimestampPattern(progressProperties.GetProperty("startedAtUtc"));
            AssertUtcTimestampPattern(progressProperties.GetProperty("stopRequestedAtUtc"));
            AssertUtcTimestampPattern(progressProperties.GetProperty("updatedAtUtc"));
        });

        var terminalSummary = FindObjectDefinition(
            artifact.Document,
            "completedAtUtc");
        var terminalSummaryProperties = terminalSummary.GetProperty("properties");
        AssertUtcTimestampPattern(terminalSummaryProperties.GetProperty("startedAtUtc"));
        AssertUtcTimestampPattern(terminalSummaryProperties.GetProperty("completedAtUtc"));

        var diagnostic = FindObjectDefinition(artifact.Document, "message");
        AssertNonBlankPattern(
            diagnostic.GetProperty("properties").GetProperty("message"));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void StatusSchema_EnforcesCapabilityIdentityAndRequiredText ()
    {
        var artifact = Assert.IsType<UcliStaticSchemaArtifact>(
            LoadSchemaSet().Find("cli-output.payload.recording.status.ok"));

        var packageProperties = FindObjectDefinition(artifact.Document, "packageId")
            .GetProperty("properties");
        Assert.Equal(
            GameViewRecorderCompatibilityMetadata.PackageId,
            packageProperties.GetProperty("packageId").GetProperty("const").GetString());

        var compatibilityProperties = FindObjectDefinition(
                artifact.Document,
                "recorderPackageVersionRange")
            .GetProperty("properties");
        Assert.Equal(
            GameViewRecorderCompatibilityMetadata.RecorderPackageVersionRange,
            compatibilityProperties
                .GetProperty("recorderPackageVersionRange")
                .GetProperty("const")
                .GetString());

        var adapterProperties = FindObjectDefinition(artifact.Document, "adapterId")
            .GetProperty("properties");
        AssertFixedNullableText(
            adapterProperties.GetProperty("adapterId"),
            GameViewRecorderCompatibilityMetadata.AdapterId);
        AssertFixedNullableText(
            adapterProperties.GetProperty("adapterVersion"),
            GameViewRecorderCompatibilityMetadata.AdapterVersion);

        var captureProperties = FindObjectDefinition(artifact.Document, "encodingProfile")
            .GetProperty("properties");
        AssertNonBlankPattern(captureProperties.GetProperty("encodingProfile"));
        AssertNonBlankPattern(captureProperties.GetProperty("encodingQuality"));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void StatusSchema_RequiresTheFixedStatusPayloadKind ()
    {
        var schema = BuildSchema(
            LoadSchemaSet(),
            "cli-output.payload.recording.status.ok");
        var status = CreateStatusPayload(
            CreateExecutionPayload(GameViewRecordingState.Recording));

        AssertValid(schema, status);

        var missingPayloadKind = Clone(status);
        missingPayloadKind.Remove("payloadKind");
        AssertInvalid(schema, missingPayloadKind);

        var wrongPayloadKind = Clone(status);
        wrongPayloadKind["payloadKind"] = "active";
        AssertInvalid(schema, wrongPayloadKind);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void CommandOutputSchemas_AcceptOnlyTheirLifecycleBranches ()
    {
        var schemaSet = LoadSchemaSet();
        var active = CreateExecutionPayload(GameViewRecordingState.Recording);
        var recovery = CreateExecutionPayload(GameViewRecordingState.Finalizing);
        var terminal = CreateExecutionPayload(GameViewRecordingState.Completed);
        var allExecutions = new[] { active, recovery, terminal };

        var startSchema = BuildSchema(
            schemaSet,
            "cli-output.payload.recording.start.ok");
        Assert.All(allExecutions, execution => AssertValid(startSchema, execution));

        var stopSchema = BuildSchema(
            schemaSet,
            "cli-output.payload.recording.stop.ok");
        AssertInvalid(stopSchema, active);
        AssertValid(stopSchema, recovery);
        AssertValid(stopSchema, terminal);

        var statusSchema = BuildSchema(
            schemaSet,
            "cli-output.payload.recording.status.ok");
        Assert.All(allExecutions, execution => AssertValid(
            statusSchema,
            CreateStatusPayload(execution)));

        var errorLogicalNames = new[]
        {
            "cli-output.payload.recording.start.error",
            "cli-output.payload.recording.status.error",
            "cli-output.payload.recording.stop.error",
        };
        Assert.All(errorLogicalNames, logicalName =>
        {
            var schema = BuildSchema(schemaSet, logicalName);
            Assert.All(allExecutions, execution => AssertValid(
                schema,
                CreateDetailedErrorPayload(execution)));
        });
    }

    [Theory]
    [InlineData(GameViewRecordingState.Recording, "terminal", "completed")]
    [InlineData(GameViewRecordingState.Finalizing, "active", "recording")]
    [InlineData(GameViewRecordingState.Completed, "recovery", "finalizing")]
    [Trait("Size", "Medium")]
    public void ExecutionPayloadSchema_RejectsValuesOutsideTheBranchLifecycle (
        GameViewRecordingState state,
        string invalidLifecycle,
        string invalidState)
    {
        var schema = BuildSchema(
            LoadSchemaSet(),
            "cli-output.payload.recording.start.ok");
        var valid = CreateExecutionPayload(state);

        var invalidReferenceLifecycle = Clone(valid);
        invalidReferenceLifecycle["executionRef"]!["lifecycle"] = invalidLifecycle;
        AssertInvalid(schema, invalidReferenceLifecycle);

        var invalidReferenceState = Clone(valid);
        invalidReferenceState["executionRef"]!["state"] = invalidState;
        AssertInvalid(schema, invalidReferenceState);

        var invalidProgressState = Clone(valid);
        invalidProgressState["progress"]!["state"] = invalidState;
        AssertInvalid(schema, invalidProgressState);

        if (state is GameViewRecordingState.Recording
            or GameViewRecordingState.Finalizing)
        {
            var missingStatusLocator = Clone(valid);
            missingStatusLocator["executionRef"]!["statusLocator"] = null;
            AssertInvalid(schema, missingStatusLocator);
        }
        else
        {
            var invalidSummaryState = Clone(valid);
            invalidSummaryState["terminalSummary"]!["state"] = invalidState;
            AssertInvalid(schema, invalidSummaryState);
        }
    }

    private static UcliStaticSchemaSet LoadSchemaSet () =>
        UcliStaticSchemaSetLoader.Load(
            AbsolutePath.Parse(TestRepositoryPaths.GetFullPath("schemas")));

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

    private static JsonElement FindObjectDefinition (
        JsonElement document,
        string propertyName)
    {
        return FindObjectDefinitions(document, propertyName).Single();
    }

    private static IEnumerable<JsonElement> FindObjectDefinitions (
        JsonElement document,
        string propertyName)
    {
        return document.GetProperty("$defs")
            .EnumerateObject()
            .Select(static definition => definition.Value)
            .Where(definition =>
                definition.TryGetProperty("properties", out var properties)
                && properties.TryGetProperty(propertyName, out _));
    }

    private static void AssertUtcTimestampPattern (JsonElement property)
    {
        var expression = CreateExpression(property.GetProperty("pattern").GetString()!);
        Assert.Matches(expression, "2026-08-05T12:34:56Z");
        Assert.Matches(expression, "2026-08-05T12:34:56.1234567+00:00");
        Assert.DoesNotMatch(expression, "2026-08-05T12:34:56+09:00");
    }

    private static void AssertNonBlankPattern (JsonElement property)
    {
        Assert.Equal(1, property.GetProperty("minLength").GetInt32());
        var expression = CreateExpression(property.GetProperty("pattern").GetString()!);
        Assert.Matches(expression, " high ");
        Assert.DoesNotMatch(expression, string.Empty);
        Assert.DoesNotMatch(expression, " \t\r\n");
    }

    private static void AssertFixedNullableText (
        JsonElement property,
        string expectedValue)
    {
        var types = property.GetProperty("type")
            .EnumerateArray()
            .Select(static value => value.GetString())
            .ToArray();
        Assert.Contains("string", types);
        Assert.Contains("null", types);
        var expression = CreateExpression(property.GetProperty("pattern").GetString()!);
        Assert.Matches(expression, expectedValue);
        Assert.DoesNotMatch(expression, expectedValue + ".other");
    }

    private static Regex CreateExpression (string pattern) =>
        new(
            pattern,
            RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(250));

    private static JsonObject CreateExecutionPayload (
        GameViewRecordingState state)
    {
        var digest = Sha256Digest.Compute([1, 2, 3]);
        var startedAtUtc = new DateTimeOffset(
            2026,
            8,
            5,
            1,
            0,
            0,
            TimeSpan.Zero);
        var completedAtUtc = startedAtUtc.AddSeconds(2);
        var requestRef = new PathArtifactRef(
            GameViewRecordingArtifactKinds.Request,
            GameViewRecordingArtifactMediaTypes.Json,
            new ArtifactPath("recordings/request.json"),
            digest,
            sizeBytes: 10,
            startedAtUtc);
        var executionState = GameViewRecordingExecutionContract.ToExecutionState(state);
        var project = new UnityProjectIdentity(
            "C:/repo",
            new ProjectFingerprint(new string('a', 64)),
            "6000.0.0f1");
        GameViewRecordingExecutionPayload payload = state switch
        {
            GameViewRecordingState.Recording => new GameViewRecordingActivePayload(
                project,
                new ActiveExecutionRef(
                    GameViewRecordingExecutionContract.Kind,
                    RecordingId,
                    digest,
                    executionState,
                    new ExecutionStatusLocator("recording:active")),
                digest,
                requestRef,
                new GameViewRecordingActiveProgress(
                    state,
                    effectiveMaxDurationSeconds: 120,
                    encodedFrameCount: 10,
                    startedAtUtc,
                    stopRequestedAtUtc: null,
                    updatedAtUtc: startedAtUtc.AddSeconds(1)),
                [requestRef],
                []),
            GameViewRecordingState.Finalizing => new GameViewRecordingRecoveryPayload(
                project,
                new RecoveryExecutionRef(
                    GameViewRecordingExecutionContract.Kind,
                    RecordingId,
                    digest,
                    executionState,
                    new ExecutionStatusLocator("recording:recovery")),
                digest,
                requestRef,
                new GameViewRecordingRecoveryProgress(
                    state,
                    effectiveMaxDurationSeconds: 120,
                    encodedFrameCount: 10,
                    startedAtUtc,
                    stopRequestedAtUtc: startedAtUtc.AddSeconds(1),
                    updatedAtUtc: startedAtUtc.AddSeconds(1)),
                [requestRef],
                []),
            GameViewRecordingState.Completed => CreateTerminalPayload(
                project,
                digest,
                requestRef,
                new GameViewRecordingTerminalProgress(
                    state,
                    effectiveMaxDurationSeconds: 120,
                    encodedFrameCount: 10,
                    startedAtUtc,
                    stopRequestedAtUtc: startedAtUtc.AddSeconds(1),
                    updatedAtUtc: completedAtUtc),
                completedAtUtc),
            _ => throw new ArgumentOutOfRangeException(
                nameof(state),
                state,
                "The schema test requires one representative state per lifecycle."),
        };
        return JsonSerializer.SerializeToNode(
                payload,
                typeof(GameViewRecordingExecutionPayload),
                CliOutputJsonSerializerOptions.Default)!
            .AsObject();
    }

    private static GameViewRecordingTerminalPayload CreateTerminalPayload (
        UnityProjectIdentity project,
        Sha256Digest digest,
        ArtifactRef requestRef,
        GameViewRecordingTerminalProgress progress,
        DateTimeOffset completedAtUtc)
    {
        var terminalRef = new PathArtifactRef(
            GameViewRecordingArtifactKinds.TerminalRecord,
            GameViewRecordingArtifactMediaTypes.Json,
            new ArtifactPath("recordings/terminal.json"),
            Sha256Digest.Compute([4, 5, 6]),
            sizeBytes: 10,
            completedAtUtc);
        return new GameViewRecordingTerminalPayload(
            project,
            new TerminalExecutionRef(
                GameViewRecordingExecutionContract.Kind,
                RecordingId,
                digest,
                GameViewRecordingExecutionContract.ToExecutionState(progress.State),
                statusLocator: null,
                terminalRef),
            digest,
            requestRef,
            progress,
            [requestRef, terminalRef],
            [],
            new GameViewRecordingTerminalSummary(
                progress.State,
                GameViewRecordingStopReason.Manual,
                GameViewRecordingVideoDisposition.Available,
                GameViewRecordingCleanupDisposition.Complete,
                progress.StartedAtUtc,
                completedAtUtc));
    }

    private static JsonObject CreateStatusPayload (JsonObject execution)
    {
        var project = execution["project"]!.DeepClone();
        return new JsonObject
        {
            ["payloadKind"] = "status",
            ["project"] = project,
            ["capability"] = JsonSerializer.SerializeToNode(
                CreateMissingCapability(),
                CliOutputJsonSerializerOptions.Default),
            ["recordingSelection"] = new JsonObject
            {
                ["kind"] = "selected",
                ["recording"] = execution.DeepClone(),
            },
        };
    }

    private static JsonObject CreateDetailedErrorPayload (JsonObject execution) =>
        new()
        {
            ["payloadKind"] = "detailed",
            ["execution"] = execution.DeepClone(),
        };

    private static JsonObject Clone (JsonObject value) =>
        value.DeepClone().AsObject();

    private static void AssertValid (
        global::Json.Schema.JsonSchema schema,
        JsonObject value)
    {
        var result = schema.Evaluate(JsonSerializer.SerializeToElement(value));
        Assert.True(
            result.IsValid,
            $"{value.ToJsonString()}\n{JsonSerializer.Serialize(result)}");
    }

    private static void AssertInvalid (
        global::Json.Schema.JsonSchema schema,
        JsonObject value)
    {
        var result = schema.Evaluate(JsonSerializer.SerializeToElement(value));
        Assert.False(result.IsValid, value.ToJsonString());
    }

    private static readonly Guid RecordingId =
        Guid.Parse("2b5b01d2-00ed-40a1-b956-69de49a47629");

    private static GameViewRecordingCapability CreateMissingCapability () =>
        new(
            new GameViewRecordingPackageCapability(
                GameViewRecordingPackageState.Missing,
                GameViewRecorderCompatibilityMetadata.PackageId,
                version: null),
            new GameViewRecordingCompatibilityCapability(
                GameViewRecordingCompatibilityState.NotApplicable,
                GameViewRecorderCompatibilityMetadata.RecorderPackageVersionRange,
                resolvedVersion: null),
            new GameViewRecordingAdapterCapability(
                GameViewRecordingAdapterState.NotApplicable,
                adapterId: null,
                adapterVersion: null),
            new GameViewRecordingRuntimeAdmission(
                GameViewRecordingRuntimeAdmissionState.Unobserved,
                [GameViewRecordingErrorCodes.Unavailable]),
            limits: null,
            captureProfile: null);
}
