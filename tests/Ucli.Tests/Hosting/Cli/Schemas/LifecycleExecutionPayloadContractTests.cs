using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using Json.Schema;
using MackySoft.JsonSchema.Generation.Projection;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;
using MackySoft.Ucli.Application.Features.Play.UseCases.Enter;
using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Json.Generation;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Hosting.Cli.Play;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Hosting.Composition.Schemas;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Schemas;

public sealed class LifecycleExecutionPayloadContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void SuccessfulLifecyclePayloads_SerializeCompletedTerminalReference ()
    {
        var cases = new (object Payload, JsonTypeInfo TypeInfo)[]
        {
            (
                CompileCommandTestData.CreateOutput(),
                CompileCommandResultFactory.SuccessPayloadTypeInfo
            ),
            (
                RefreshCommandTestData.CreateSuccessResult().Output!,
                RefreshCommandResultFactory.SuccessPayloadTypeInfo
            ),
            (
                PlayEnterCommandTestData.CreateOutput(),
                PlayEnterCommandResultFactory.SuccessPayloadTypeInfo
            ),
            (
                PlayExitCommandTestData.CreateOutput(),
                PlayExitCommandResultFactory.SuccessPayloadTypeInfo
            ),
        };

        foreach (var testCase in cases)
        {
            var payload = JsonSerializer.SerializeToElement(
                testCase.Payload,
                testCase.TypeInfo);
            var reference = payload.GetProperty("lifecycleExecutionRef");

            Assert.Equal(
                TextVocabulary.GetText(ExecutionLifecycle.Terminal),
                reference.GetProperty("lifecycle").GetString());
            Assert.Equal(
                TextVocabulary.GetText(LifecycleExecutionState.Completed),
                reference.GetProperty("state").GetString());
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SuccessfulLifecyclePayloadConstructors_RejectFailedTerminalReference ()
    {
        var compile = CompileCommandTestData.CreateOutput();
        var refresh = RefreshCommandTestData.CreateSuccessResult().Output!;
        var playEnter = PlayEnterCommandTestData.CreateOutput();
        var playExit = PlayExitCommandTestData.CreateOutput();
        var constructors = new Action[]
        {
            () => new CompileExecutionOutput(
                compile.Project,
                CreateFailedReference(compile.LifecycleExecutionRef),
                compile.Verdict,
                compile.Verifiers,
                compile.Claims,
                compile.Reports,
                compile.ResidualRisks,
                compile.Compile),
            () => new RefreshExecutionOutput(
                refresh.Project,
                refresh.RequestId,
                CreateFailedReference(refresh.LifecycleExecutionRef),
                refresh.Refresh,
                refresh.Lifecycle,
                refresh.ReadPostcondition),
            () => PlayEnterExecutionResult.Success(new PlayEnterExecutionOutput(
                playEnter.Project,
                CreateFailedReference(playEnter.LifecycleExecutionRef),
                playEnter.DaemonStatus,
                playEnter.ServerVersion,
                playEnter.EditorMode,
                playEnter.LifecycleState,
                playEnter.BlockingReason,
                playEnter.CompileState,
                playEnter.Generations,
                playEnter.CanAcceptExecutionRequests,
                playEnter.ObservedAtUtc,
                playEnter.ActionRequired,
                playEnter.PrimaryDiagnostic,
                playEnter.PlayMode,
                playEnter.Transition,
                playEnter.TimeoutMilliseconds)),
            () => PlayExitExecutionResult.Success(new PlayExitExecutionOutput(
                playExit.Project,
                CreateFailedReference(playExit.LifecycleExecutionRef),
                playExit.DaemonStatus,
                playExit.ServerVersion,
                playExit.EditorMode,
                playExit.LifecycleState,
                playExit.BlockingReason,
                playExit.CompileState,
                playExit.Generations,
                playExit.CanAcceptExecutionRequests,
                playExit.ObservedAtUtc,
                playExit.ActionRequired,
                playExit.PrimaryDiagnostic,
                playExit.PlayMode,
                playExit.Transition,
                playExit.TimeoutMilliseconds)),
        };

        Assert.All(constructors, constructor =>
            Assert.Throws<ArgumentException>(constructor));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CompileAndRefreshSuccessPayloads_RejectInvalidTerminalRecordContract ()
    {
        var compile = CompileCommandTestData.CreateOutput();
        var refresh = RefreshCommandTestData.CreateSuccessResult().Output!;
        var constructors = new Action[]
        {
            () => new CompileExecutionOutput(
                compile.Project,
                CreateInvalidTerminalRecordReference(
                    compile.LifecycleExecutionRef,
                    invalidKind: true),
                compile.Verdict,
                compile.Verifiers,
                compile.Claims,
                compile.Reports,
                compile.ResidualRisks,
                compile.Compile),
            () => new CompileExecutionOutput(
                compile.Project,
                CreateInvalidTerminalRecordReference(
                    compile.LifecycleExecutionRef,
                    invalidKind: false),
                compile.Verdict,
                compile.Verifiers,
                compile.Claims,
                compile.Reports,
                compile.ResidualRisks,
                compile.Compile),
            () => new RefreshExecutionOutput(
                refresh.Project,
                refresh.RequestId,
                CreateInvalidTerminalRecordReference(
                    refresh.LifecycleExecutionRef,
                    invalidKind: true),
                refresh.Refresh,
                refresh.Lifecycle,
                refresh.ReadPostcondition),
            () => new RefreshExecutionOutput(
                refresh.Project,
                refresh.RequestId,
                CreateInvalidTerminalRecordReference(
                    refresh.LifecycleExecutionRef,
                    invalidKind: false),
                refresh.Refresh,
                refresh.Lifecycle,
                refresh.ReadPostcondition),
        };

        Assert.All(constructors, constructor =>
            Assert.Throws<ArgumentException>(constructor));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CompileAndRefreshFailurePayloads_RejectCompletedTerminalReference ()
    {
        var compile = CompileCommandTestData.CreateOutput();
        var refresh = RefreshCommandTestData.CreateSuccessResult().Output!;
        var failure = ApplicationFailure.FromCode(
            LifecycleExecutionErrorCodes.DeadlineExceeded,
            "Lifecycle Execution failed.");
        var factories = new Action[]
        {
            () => CompileCommandResultFactory.Create(
                CompileExecutionResult.Failed(
                    failure,
                    compile.Project,
                    Assert.IsType<TerminalExecutionRef>(
                        compile.LifecycleExecutionRef),
                    ExecutionApplicationState.Applied)),
            () => RefreshCommandResultFactory.Create(
                RefreshExecutionResult.Failure(
                    failure,
                    new RefreshExecutionErrorOutput(
                        refresh.Project,
                        refresh.RequestId,
                        Assert.IsType<TerminalExecutionRef>(
                            refresh.LifecycleExecutionRef),
                        ExecutionApplicationState.Applied,
                        Refresh: null,
                        ObservedLifecycle: null,
                        ReadPostcondition: null))),
        };

        Assert.All(factories, factory =>
            Assert.Throws<ArgumentException>(factory));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RefreshFailurePayload_RejectsPartiallyAppliedState ()
    {
        var refresh = RefreshCommandTestData.CreateSuccessResult().Output!;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RefreshExecutionErrorOutput(
                refresh.Project,
                refresh.RequestId,
                LifecycleExecutionRef: null,
                ExecutionApplicationState.PartiallyApplied,
                Refresh: null,
                ObservedLifecycle: null,
                ReadPostcondition: null));

        Assert.Equal("applicationState", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CompileAndRefreshFailurePayloads_SerializeIntoGeneratedActionSchemas ()
    {
        var compileOutput = CompileCommandTestData.CreateOutput();
        var cases = new (string Command, CommandResult Result)[]
        {
            (
                UcliCommandNames.Compile,
                CompileCommandResultFactory.Create(
                    CompileExecutionResult.Failed(
                        ApplicationFailure.FromCode(
                            LifecycleExecutionErrorCodes.DeadlineExceeded,
                            "Compile deadline exceeded."),
                        compileOutput.Project,
                        CompileCommandTestData.CreateActiveReference(),
                        ExecutionApplicationState.Indeterminate))
            ),
            (
                UcliCommandNames.Refresh,
                RefreshCommandResultFactory.Create(
                    RefreshCommandTestData.CreateFailureResult())
            ),
        };

        foreach (var testCase in cases)
        {
            var schema = global::Json.Schema.JsonSchema.Build(
                JsonSerializer.SerializeToElement(GenerateCommandSchema(
                    testCase.Command,
                    CommandResultStatus.Error)),
                new BuildOptions
                {
                    SchemaRegistry = new SchemaRegistry
                    {
                        Fetch = null!,
                    },
                });
            var payload = JsonSerializer.SerializeToElement(
                testCase.Result.Payload,
                CliOutputJsonSerializerOptions.Default);

            Assert.True(schema.Evaluate(payload).IsValid);
        }
    }

    [Theory]
    [InlineData(UcliCommandNames.Compile, LifecycleExecutionKind.Compile)]
    [InlineData(UcliCommandNames.Refresh, LifecycleExecutionKind.Refresh)]
    [InlineData(UcliCommandNames.PlayEnter, LifecycleExecutionKind.PlayEnter)]
    [InlineData(UcliCommandNames.PlayExit, LifecycleExecutionKind.PlayExit)]
    [Trait("Size", "Small")]
    public void SuccessfulLifecyclePayloadSchema_AllowsOnlyMatchingCompletedTerminalReference (
        string command,
        LifecycleExecutionKind executionKind)
    {
        var schema = GenerateCommandSchema(
            command,
            CommandResultStatus.Ok);
        var referenceContract = FindPropertyContracts(
                schema,
                "lifecycleExecutionRef")
            .Single();
        var terminalBranch = Assert.IsType<JsonArray>(
                referenceContract["oneOf"])
            .Single()!
            .AsObject();
        var terminalContract = ResolveLocalReference(schema, terminalBranch);
        var terminalProperties = terminalContract["properties"]!.AsObject();
        var definition = new LifecycleExecutionDefinition(executionKind);

        Assert.Equal(
            TextVocabulary.GetText(ExecutionLifecycle.Terminal),
            terminalProperties["lifecycle"]!["const"]!.GetValue<string>());
        Assert.Equal(
            definition.ExecutionKind.Value,
            terminalProperties["kind"]!["const"]!.GetValue<string>());
        Assert.Equal(
            LifecycleExecutionDefinitionDigest.Calculate(definition).ToString(),
            terminalProperties["definitionDigest"]!["const"]!.GetValue<string>());
        Assert.Equal(
            TextVocabulary.GetText(LifecycleExecutionState.Completed),
            terminalProperties["state"]!["const"]!.GetValue<string>());

        var terminalRecordContract = terminalProperties["terminalRecordRef"]!.AsObject();
        foreach (var branch in terminalRecordContract["oneOf"]!.AsArray())
        {
            var artifactProperties = ResolveLocalReference(
                    schema,
                    branch!.AsObject())
                ["properties"]!
                .AsObject();
            Assert.Equal(
                LifecycleExecutionArtifactContract.TerminalRecordKind.Value,
                artifactProperties["kind"]!["const"]!.GetValue<string>());
            Assert.Equal(
                LifecycleExecutionArtifactContract.TerminalRecordMediaType.Value,
                artifactProperties["mediaType"]!["const"]!.GetValue<string>());
        }
    }

    [Theory]
    [InlineData(UcliCommandNames.Compile, LifecycleExecutionKind.Compile)]
    [InlineData(UcliCommandNames.Refresh, LifecycleExecutionKind.Refresh)]
    [InlineData(UcliCommandNames.PlayEnter, LifecycleExecutionKind.PlayEnter)]
    [InlineData(UcliCommandNames.PlayExit, LifecycleExecutionKind.PlayExit)]
    [Trait("Size", "Small")]
    public void FailedLifecyclePayloadSchema_ConstrainsActionReferenceIdentityStatesAndUsesCanonicalLocatorShape (
        string command,
        LifecycleExecutionKind executionKind)
    {
        var schema = GenerateCommandSchema(
            command,
            CommandResultStatus.Error);
        var referenceContracts = FindPropertyContracts(
                schema,
                "lifecycleExecutionRef")
            .ToArray();
        Assert.NotEmpty(referenceContracts);
        var definition = new LifecycleExecutionDefinition(executionKind);

        foreach (var referenceContract in referenceContracts)
        {
            foreach (var branch in referenceContract["oneOf"]!.AsArray()
                .Where(static branch => branch?["$ref"] != null))
            {
                var referenceProperties = ResolveLocalReference(
                        schema,
                        branch!.AsObject())
                    ["properties"]!
                    .AsObject();
                Assert.True(TextVocabulary.TryGetValue(
                    referenceProperties["lifecycle"]!["const"]!.GetValue<string>(),
                    out ExecutionLifecycle lifecycle));

                Assert.Equal(
                    definition.ExecutionKind.Value,
                    referenceProperties["kind"]!["const"]!.GetValue<string>());
                Assert.Equal(
                    LifecycleExecutionDefinitionDigest.Calculate(definition).ToString(),
                    referenceProperties["definitionDigest"]!["const"]!.GetValue<string>());
                var statusLocatorType = referenceProperties["statusLocator"]!["type"]!;
                if (lifecycle == ExecutionLifecycle.Terminal)
                {
                    Assert.Equal(
                        new[] { "string", "null" },
                        statusLocatorType
                            .AsArray()
                            .Select(static item => item!.GetValue<string>()));
                }
                else
                {
                    Assert.Equal("string", statusLocatorType.GetValue<string>());
                }
                AssertAcceptsExactly(
                    referenceProperties["state"]!.AsObject(),
                    Enum.GetValues<LifecycleExecutionState>(),
                    GetExpectedErrorStates(executionKind, lifecycle));
            }
        }
    }

    [Theory]
    [InlineData(
        UcliCommandNames.PlayEnter,
        PlayLifecycleTransitionCommand.Enter,
        PlayLifecycleTransitionOutcome.Entered,
        PlayLifecycleTransitionOutcome.AlreadyEntered)]
    [InlineData(
        UcliCommandNames.PlayExit,
        PlayLifecycleTransitionCommand.Exit,
        PlayLifecycleTransitionOutcome.Exited,
        PlayLifecycleTransitionOutcome.AlreadyExited)]
    [Trait("Size", "Small")]
    public void FailedPlayPayloadSchema_UsesFiveClosedRuntimeBranches (
        string command,
        PlayLifecycleTransitionCommand transition,
        PlayLifecycleTransitionOutcome firstSuccessOutcome,
        PlayLifecycleTransitionOutcome secondSuccessOutcome)
    {
        var schema = GenerateCommandSchema(
            command,
            CommandResultStatus.Error);
        var branches = schema["oneOf"]!
            .AsArray()
            .ToDictionary(
                static branch => branch!["properties"]!["payloadKind"]!["const"]!
                    .GetValue<string>(),
                branch => ResolveLocalReference(schema, branch!.AsObject()),
                StringComparer.Ordinal);

        Assert.Equal(
            new[]
            {
                "empty",
                "start",
                "terminalFailure",
                "terminalPublicationFailure",
                "transitionFailure",
            },
            branches.Keys.Order(StringComparer.Ordinal));
        AssertClosedRequiredProperties(
            branches["empty"],
            "payloadKind");
        AssertClosedRequiredProperties(
            branches["start"],
            "payloadKind",
            "project",
            "lifecycleExecutionRef",
            "applicationState");
        var startReferenceLifecycles =
            branches["start"]["properties"]!["lifecycleExecutionRef"]!
                ["oneOf"]!
                .AsArray()
                .Select(branch => ResolveLocalReference(
                    schema,
                    branch!.AsObject()))
                .Select(reference =>
                    reference["properties"]!["lifecycle"]!["const"]!
                        .GetValue<string>())
                .Order(StringComparer.Ordinal)
                .ToArray();
        Assert.Equal(
            new[]
            {
                TextVocabulary.GetText(ExecutionLifecycle.Active),
                TextVocabulary.GetText(ExecutionLifecycle.Recovery),
                TextVocabulary.GetText(ExecutionLifecycle.Terminal),
            },
            startReferenceLifecycles);

        string[] evidencePropertyNames =
        [
            "payloadKind",
            "project",
            "lifecycleExecutionRef",
            "applicationState",
            "daemonStatus",
            "serverVersion",
            "editorMode",
            "lifecycleState",
            "blockingReason",
            "compileState",
            "generations",
            "canAcceptExecutionRequests",
            "observedAtUtc",
            "actionRequired",
            "primaryDiagnostic",
            "playMode",
            "transition",
            "timeoutMilliseconds",
        ];
        AssertClosedRequiredProperties(
            branches["transitionFailure"],
            evidencePropertyNames);
        AssertClosedRequiredProperties(
            branches["terminalPublicationFailure"],
            evidencePropertyNames);
        AssertClosedRequiredProperties(
            branches["terminalFailure"],
            evidencePropertyNames);

        var failureTransition = ResolveLocalReference(
            schema,
            branches["transitionFailure"]["properties"]!["transition"]!
                .AsObject());
        AssertClosedRequiredProperties(
            failureTransition,
            "transition",
            "result",
            "before",
            "observed");
        Assert.Equal(
            TextVocabulary.GetText(transition),
            failureTransition["properties"]!["transition"]!["const"]!
                .GetValue<string>());
        AssertAcceptsExactly(
            failureTransition["properties"]!["result"]!.AsObject(),
            Enum.GetValues<PlayLifecycleTransitionOutcome>(),
            [
                PlayLifecycleTransitionOutcome.Blocked,
                PlayLifecycleTransitionOutcome.Timeout,
            ]);

        var publicationTransition = ResolveLocalReference(
            schema,
            branches["terminalPublicationFailure"]["properties"]!["transition"]!
                .AsObject());
        AssertClosedRequiredProperties(
            publicationTransition,
            "transition",
            "result",
            "before",
            "after");
        Assert.Equal(
            TextVocabulary.GetText(transition),
            publicationTransition["properties"]!["transition"]!["const"]!
                .GetValue<string>());
        AssertAcceptsExactly(
            publicationTransition["properties"]!["result"]!.AsObject(),
            Enum.GetValues<PlayLifecycleTransitionOutcome>(),
            [firstSuccessOutcome, secondSuccessOutcome]);

        var terminalTransition = ResolveLocalReference(
            schema,
            branches["terminalFailure"]["properties"]!["transition"]!
                .AsObject());
        AssertClosedRequiredProperties(
            terminalTransition,
            "transition",
            "result",
            "before",
            "after");
        Assert.Equal(
            TextVocabulary.GetText(transition),
            terminalTransition["properties"]!["transition"]!["const"]!
                .GetValue<string>());
        AssertAcceptsExactly(
            terminalTransition["properties"]!["result"]!.AsObject(),
            Enum.GetValues<PlayLifecycleTransitionOutcome>(),
            [firstSuccessOutcome, secondSuccessOutcome]);

        var publicationReferenceBranches =
            branches["terminalPublicationFailure"]["properties"]!
                ["lifecycleExecutionRef"]!["oneOf"]!
                .AsArray();
        var publicationReferenceBranch = Assert.Single(
            publicationReferenceBranches);
        var publicationReference = ResolveLocalReference(
            schema,
            publicationReferenceBranch!.AsObject());
        Assert.Equal(
            TextVocabulary.GetText(ExecutionLifecycle.Recovery),
            publicationReference["properties"]!["lifecycle"]!["const"]!
                .GetValue<string>());

        var terminalReferenceBranch = Assert.Single(
            branches["terminalFailure"]["properties"]!
                ["lifecycleExecutionRef"]!["oneOf"]!
                .AsArray());
        var terminalReference = ResolveLocalReference(
            schema,
            terminalReferenceBranch!.AsObject());
        Assert.Equal(
            TextVocabulary.GetText(ExecutionLifecycle.Terminal),
            terminalReference["properties"]!["lifecycle"]!["const"]!
                .GetValue<string>());
        Assert.Equal(
            TextVocabulary.GetText(LifecycleExecutionState.Failed),
            terminalReference["properties"]!["state"]!["const"]!
                .GetValue<string>());
    }

    [Theory]
    [InlineData(UcliCommandNames.Compile, true)]
    [InlineData(UcliCommandNames.Refresh, false)]
    [InlineData(UcliCommandNames.PlayEnter, false)]
    [InlineData(UcliCommandNames.PlayExit, false)]
    [Trait("Size", "Small")]
    public void FailedLifecyclePayloadSchema_UsesActionApplicationStateVocabulary (
        string command,
        bool allowsPartiallyApplied)
    {
        var schema = GenerateCommandSchema(
            command,
            CommandResultStatus.Error);
        var contracts = FindPropertyContracts(
                schema,
                "applicationState")
            .ToArray();
        Assert.NotEmpty(contracts);
        var expected = Enum.GetValues<ExecutionApplicationState>()
            .Where(value =>
                allowsPartiallyApplied
                || value != ExecutionApplicationState.PartiallyApplied)
            .ToArray();

        foreach (var contract in contracts)
        {
            AssertAcceptsExactly(
                contract,
                Enum.GetValues<ExecutionApplicationState>(),
                expected);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RefreshAndCompileFailureSchemas_ExposeOnlyDocumentedCliEvidence ()
    {
        var expectations = new[]
        {
            (
                Command: UcliCommandNames.Compile,
                Properties: new[]
                {
                    "payloadKind",
                    "project",
                    "lifecycleExecutionRef",
                    "applicationState",
                },
                Required: new[]
                {
                    "payloadKind",
                    "project",
                    "lifecycleExecutionRef",
                    "applicationState",
                }),
            (
                Command: UcliCommandNames.Refresh,
                Properties: new[]
                {
                    "payloadKind",
                    "project",
                    "requestId",
                    "lifecycleExecutionRef",
                    "applicationState",
                    "refresh",
                    "observedLifecycle",
                    "readPostcondition",
                },
                Required: new[]
                {
                    "payloadKind",
                    "project",
                    "requestId",
                    "lifecycleExecutionRef",
                    "applicationState",
                    "refresh",
                    "observedLifecycle",
                }),
        };

        foreach (var expectation in expectations)
        {
            var schema = GenerateCommandSchema(
                expectation.Command,
                CommandResultStatus.Error);
            var detailedBranch = schema["oneOf"]!
                .AsArray()
                .Single(static branch =>
                    branch!["properties"]!["payloadKind"]!["const"]!
                        .GetValue<string>() == "detailed");
            var contract = ResolveLocalReference(
                schema,
                detailedBranch!.AsObject());

            Assert.False(contract["additionalProperties"]!.GetValue<bool>());
            Assert.Equal(
                expectation.Properties.Order(StringComparer.Ordinal),
                contract["properties"]!
                    .AsObject()
                    .Select(static property => property.Key)
                    .Order(StringComparer.Ordinal));
            Assert.Equal(
                expectation.Required.Order(StringComparer.Ordinal),
                contract["required"]!
                    .AsArray()
                    .Select(static property => property!.GetValue<string>())
                    .Order(StringComparer.Ordinal));
        }
    }

    [Theory]
    [InlineData(
        UcliCommandNames.PlayEnter,
        PlayLifecycleTransitionCommand.Enter,
        PlayLifecycleTransitionOutcome.Entered,
        PlayLifecycleTransitionOutcome.AlreadyEntered)]
    [InlineData(
        UcliCommandNames.PlayExit,
        PlayLifecycleTransitionCommand.Exit,
        PlayLifecycleTransitionOutcome.Exited,
        PlayLifecycleTransitionOutcome.AlreadyExited)]
    [Trait("Size", "Small")]
    public void SuccessfulPlayPayloadSchema_UsesActionSpecificSuccessTransition (
        string command,
        PlayLifecycleTransitionCommand transition,
        PlayLifecycleTransitionOutcome firstOutcome,
        PlayLifecycleTransitionOutcome secondOutcome)
    {
        var schema = GenerateCommandSchema(
            command,
            CommandResultStatus.Ok);
        var transitionObject = FindObjectContracts(
                schema,
                "transition",
                "result",
                "before",
                "after")
            .Single();
        var properties = transitionObject["properties"]!.AsObject();

        Assert.Equal(
            new[] { "after", "before", "result", "transition" },
            properties
                .Select(static property => property.Key)
                .Order(StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(
            new[] { "after", "before", "result", "transition" },
            transitionObject["required"]!
                .AsArray()
                .Select(static item => item!.GetValue<string>())
                .Order(StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(
            TextVocabulary.GetText(transition),
            properties["transition"]!["const"]!.GetValue<string>());
        AssertAcceptsExactly(
            properties["result"]!.AsObject(),
            Enum.GetValues<PlayLifecycleTransitionOutcome>(),
            [firstOutcome, secondOutcome]);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CompilePayloadSchema_UsesRuntimeNumericBoundaries ()
    {
        var schema = global::Json.Schema.JsonSchema.Build(
            JsonSerializer.SerializeToElement(GenerateCommandSchema(
                UcliCommandNames.Compile,
                CommandResultStatus.Ok)),
            new BuildOptions
            {
                SchemaRegistry = new SchemaRegistry
                {
                    Fetch = null!,
                },
            });
        var validPayload = JsonSerializer.SerializeToNode(
                CompileCommandTestData.CreateOutput(),
                CompileCommandResultFactory.SuccessPayloadTypeInfo)!
            .AsObject();

        Assert.True(schema
            .Evaluate(JsonSerializer.SerializeToElement(validPayload))
            .IsValid);

        var negativeGeneration = Clone(validPayload);
        negativeGeneration["compile"]!["lifecycle"]!["generations"]!
            ["compileGeneration"] = -1;
        AssertRejected(schema, negativeGeneration);

        var zeroGeneration = Clone(validPayload);
        zeroGeneration["compile"]!["lifecycle"]!["generations"]!
            ["compileGeneration"] = 0;
        Assert.True(schema
            .Evaluate(JsonSerializer.SerializeToElement(zeroGeneration))
            .IsValid);

        var negativeNullableGeneration = Clone(validPayload);
        negativeNullableGeneration["compile"]!["scriptCompilation"]!
            ["compileGenerationBefore"] = -1;
        AssertRejected(schema, negativeNullableGeneration);

        var zeroNullableGeneration = Clone(validPayload);
        zeroNullableGeneration["compile"]!["scriptCompilation"]!
            ["compileGenerationBefore"] = 0;
        Assert.True(schema
            .Evaluate(JsonSerializer.SerializeToElement(zeroNullableGeneration))
            .IsValid);

        var negativeDiagnosticCount = Clone(validPayload);
        negativeDiagnosticCount["compile"]!["scriptCompilation"]!
            ["diagnostics"]!["errorCount"] = -1;
        AssertRejected(schema, negativeDiagnosticCount);

        var zeroDiagnosticCount = Clone(validPayload);
        zeroDiagnosticCount["compile"]!["scriptCompilation"]!
            ["diagnostics"]!["errorCount"] = 0;
        Assert.True(schema
            .Evaluate(JsonSerializer.SerializeToElement(zeroDiagnosticCount))
            .IsValid);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void LifecycleTerminalRecordSchema_ConstrainsActionDigestAndVerdictOwnership ()
    {
        var schema = GenerateTerminalRecordSchema();

        foreach (var branch in schema["oneOf"]!.AsArray())
        {
            var properties = ResolveLocalReference(
                    schema,
                    branch!.AsObject())
                ["properties"]!
                .AsObject();
            Assert.True(TextVocabulary.TryGetValue(
                properties["executionKind"]!["const"]!.GetValue<string>(),
                out LifecycleExecutionKind executionKind));
            var definition = new LifecycleExecutionDefinition(executionKind);

            Assert.Equal(
                LifecycleExecutionDefinitionDigest.Calculate(definition).ToString(),
                properties["definitionDigest"]!["const"]!.GetValue<string>());
            if (executionKind == LifecycleExecutionKind.Compile)
            {
                Assert.False(properties["verdict"]!.AsObject().ContainsKey("const"));
            }
            else
            {
                Assert.True(properties["verdict"]!.AsObject().ContainsKey("const"));
                Assert.Null(properties["verdict"]!["const"]);
            }

            if (executionKind is LifecycleExecutionKind.PlayEnter
                or LifecycleExecutionKind.PlayExit)
            {
                var transition = executionKind == LifecycleExecutionKind.PlayEnter
                    ? PlayLifecycleTransitionCommand.Enter
                    : PlayLifecycleTransitionCommand.Exit;
                var resultReference = properties["result"]!["anyOf"]!
                    .AsArray()
                    .Select(static branch => branch?.AsObject())
                    .Single(static branch => branch?["$ref"] != null)!;
                var resultProperties = ResolveLocalReference(
                        schema,
                        resultReference)
                    ["properties"]!
                    .AsObject();

                Assert.Equal(
                    TextVocabulary.GetText(transition),
                    resultProperties["transition"]!["const"]!.GetValue<string>());
                AssertAcceptsExactly(
                    resultProperties["result"]!.AsObject(),
                    Enum.GetValues<PlayLifecycleTransitionOutcome>(),
                    transition == PlayLifecycleTransitionCommand.Enter
                        ?
                        [
                            PlayLifecycleTransitionOutcome.Entered,
                            PlayLifecycleTransitionOutcome.AlreadyEntered,
                            PlayLifecycleTransitionOutcome.Blocked,
                            PlayLifecycleTransitionOutcome.Timeout,
                        ]
                        :
                        [
                            PlayLifecycleTransitionOutcome.Exited,
                            PlayLifecycleTransitionOutcome.AlreadyExited,
                            PlayLifecycleTransitionOutcome.Blocked,
                            PlayLifecycleTransitionOutcome.Timeout,
                        ]);
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void LifecycleTerminalRecordSchema_ProjectsScalarConstructorConstraints ()
    {
        var schema = GenerateTerminalRecordSchema();

        var projectFingerprintContracts = FindPropertyContracts(
                schema,
                "projectFingerprint")
            .ToArray();
        Assert.NotEmpty(projectFingerprintContracts);
        Assert.All(
            projectFingerprintContracts,
            contract =>
            {
                Assert.Equal(64, contract["minLength"]!.GetValue<int>());
                Assert.Equal(64, contract["maxLength"]!.GetValue<int>());
                var expression = new Regex(
                    contract["pattern"]!.GetValue<string>(),
                    RegexOptions.CultureInvariant);
                Assert.Matches(expression, new string('a', 64));
                Assert.DoesNotMatch(expression, new string('A', 64));
            });

        foreach (var branch in schema["oneOf"]!.AsArray())
        {
            var properties = ResolveLocalReference(
                    schema,
                    branch!.AsObject())
                ["properties"]!
                .AsObject();
            foreach (var propertyName in new[]
            {
                "deadlineUtc",
                "startedAtUtc",
                "completedAtUtc",
            })
            {
                var expression = new Regex(
                    properties[propertyName]!["pattern"]!.GetValue<string>(),
                    RegexOptions.CultureInvariant);
                Assert.Matches(
                    expression,
                    "2026-07-31T12:34:56.1234567+00:00");
                Assert.Matches(
                    expression,
                    "2026-07-31T12:34:56Z");
                Assert.DoesNotMatch(
                    expression,
                    "2026-07-31T21:34:56+09:00");
            }
        }

        AssertPropertyMinimum(schema, "processId", 1);
        AssertPropertyMinimum(schema, "generation", 1);
        AssertPropertyMinimum(schema, "compileGeneration", 0);
        AssertPropertyMinimum(schema, "domainReloadGeneration", 0);
        AssertPropertyMinimum(schema, "assetRefreshGeneration", 0);
        AssertPropertyMinimum(schema, "playModeGeneration", 0);
        AssertPropertyMinimum(schema, "domainReloadGenerationBefore", 0);
        AssertPropertyMinimum(schema, "domainReloadGenerationAfter", 0);
        AssertPropertyMinimum(schema, "compileGenerationBefore", 0);
        AssertPropertyMinimum(schema, "compileGenerationAfter", 0);
        AssertPropertyMinimum(schema, "errorCount", 0);
        AssertPropertyMinimum(schema, "warningCount", 0);

        var evaluableSchema = global::Json.Schema.JsonSchema.Build(
            JsonSerializer.SerializeToElement(schema),
            new BuildOptions
            {
                SchemaRegistry = new SchemaRegistry
                {
                    Fetch = null!,
                },
            });
        var startedAtUtc = new DateTimeOffset(
            2026,
            7,
            31,
            12,
            0,
            0,
            TimeSpan.Zero);
        var firstEndpointGenerationId =
            Guid.Parse("10000000-0000-0000-0000-000000000001");
        LifecycleExecutionTerminalRecord validRecord =
            new RefreshLifecycleExecutionTerminalRecord(
                Guid.Parse("20000000-0000-0000-0000-000000000001"),
                LifecycleExecutionDefinitionDigest.Calculate(
                    new LifecycleExecutionDefinition(
                        LifecycleExecutionKind.Refresh)),
                new UnityProjectIdentity(
                    "/workspace/UnityProject",
                    new ProjectFingerprint(new string('a', 64)),
                    "6000.1.4f1"),
                new LifecycleExecutionHostRegistration(
                    new ProcessIdentity(42, 1),
                    Guid.Parse("30000000-0000-0000-0000-000000000001"),
                    firstEndpointGenerationId,
                    firstEndpointGenerationId),
                new UnityEditorGenerationSnapshot(0, 0, 0, 0),
                terminalGeneration: null,
                startedAtUtc.AddMinutes(1),
                startedAtUtc,
                startedAtUtc.AddMinutes(1),
                LifecycleExecutionTerminalReason.DeadlineExceeded,
                ExecutionApplicationState.Unknown,
                result: null,
                verdict: null,
                artifactRefs: Array.Empty<ArtifactRef>());
        var validJson = JsonSerializer.SerializeToNode(
                validRecord,
                typeof(LifecycleExecutionTerminalRecord),
                IpcJsonSerializerOptions.StrictPropertyNames)!
            .AsObject();

        Assert.True(evaluableSchema
            .Evaluate(JsonSerializer.SerializeToElement(validJson))
            .IsValid);

        var negativeEditorCounter = Clone(validJson);
        negativeEditorCounter["startedGeneration"]!["compileGeneration"] = -1;
        AssertRejected(evaluableSchema, negativeEditorCounter);

        var zeroProcessId = Clone(validJson);
        zeroProcessId["host"]!["process"]!["processId"] = 0;
        AssertRejected(evaluableSchema, zeroProcessId);

        var negativeProcessId = Clone(validJson);
        negativeProcessId["host"]!["process"]!["processId"] = -1;
        AssertRejected(evaluableSchema, negativeProcessId);

        var zeroProcessGeneration = Clone(validJson);
        zeroProcessGeneration["host"]!["process"]!["generation"] = 0;
        AssertRejected(evaluableSchema, zeroProcessGeneration);

        var nonUtcDeadline = Clone(validJson);
        nonUtcDeadline["deadlineUtc"] = "2026-07-31T21:01:00+09:00";
        AssertRejected(evaluableSchema, nonUtcDeadline);

        var invalidProjectFingerprint = Clone(validJson);
        invalidProjectFingerprint["project"]!["projectFingerprint"] =
            new string('A', 64);
        AssertRejected(evaluableSchema, invalidProjectFingerprint);
    }

    private static JsonObject GenerateCommandSchema (
        string command,
        CommandResultStatus status)
    {
        var registration = UcliCommandPayloadSchemaRegistrationCatalog
            .GetAll()
            .Single(item =>
                item.Command == command
                && item.Status == status);
        var generationResult =
            UcliJsonContractGenerator.GenerateWithLifecycleExecutionCliOutputProfile(
                registration.ContractId,
                registration.TypeInfo,
                new JsonSchemaDocumentOptions(
                    JsonSchemaDocumentKind.Complete,
                    id: null,
                    logicalName: null),
                Assert.IsType<LifecycleExecutionKind>(
                    registration.LifecycleExecutionKind),
                Assert.IsType<CommandResultStatus>(registration.Status));
        return JsonNode.Parse(generationResult.GetJsonSchemaUtf8())!.AsObject();
    }

    private static JsonObject GenerateTerminalRecordSchema ()
    {
        var registration = UcliStaticSchemaRegistrationCatalog
            .GetAll()
            .Single(item =>
                item.Name == "common.lifecycle-execution-terminal-record");
        var generationResult =
            UcliJsonContractGenerator
                .GenerateWithLifecycleExecutionTerminalRecordProfile(
                    registration.ContractId,
                    registration.TypeInfo,
                    new JsonSchemaDocumentOptions(
                        JsonSchemaDocumentKind.Complete,
                        id: null,
                        logicalName: null));
        return JsonNode.Parse(generationResult.GetJsonSchemaUtf8())!.AsObject();
    }

    private static JsonObject ResolveLocalReference (
        JsonNode schema,
        JsonObject reference)
    {
        var pointer = reference["$ref"]!.GetValue<string>();
        Assert.StartsWith("#/$defs/", pointer, StringComparison.Ordinal);
        var definitionName = pointer["#/$defs/".Length..];
        return schema["$defs"]![definitionName]!.AsObject();
    }

    private static void AssertPropertyMinimum (
        JsonNode schema,
        string propertyName,
        int expectedMinimum)
    {
        var contracts = FindPropertyContracts(schema, propertyName)
            .ToArray();
        Assert.NotEmpty(contracts);
        Assert.All(
            contracts,
            contract => Assert.Equal(
                expectedMinimum,
                contract["minimum"]!.GetValue<int>()));
    }

    private static JsonObject Clone (JsonObject source)
    {
        return JsonNode.Parse(source.ToJsonString())!.AsObject();
    }

    private static void AssertRejected (
        global::Json.Schema.JsonSchema schema,
        JsonObject instance)
    {
        Assert.False(schema
            .Evaluate(JsonSerializer.SerializeToElement(instance))
            .IsValid);
    }

    private static void AssertClosedRequiredProperties (
        JsonObject contract,
        params string[] expectedPropertyNames)
    {
        Assert.False(contract["additionalProperties"]!.GetValue<bool>());
        Assert.Equal(
            expectedPropertyNames.Order(StringComparer.Ordinal),
            contract["properties"]!
                .AsObject()
                .Select(static property => property.Key)
                .Order(StringComparer.Ordinal));
        Assert.Equal(
            expectedPropertyNames.Order(StringComparer.Ordinal),
            contract["required"]!
                .AsArray()
                .Select(static property => property!.GetValue<string>())
                .Order(StringComparer.Ordinal));
    }

    private static IEnumerable<JsonObject> FindPropertyContracts (
        JsonNode node,
        string propertyName)
    {
        if (node is JsonObject jsonObject)
        {
            if (jsonObject["properties"] is JsonObject properties
                && properties[propertyName] is JsonObject propertyContract)
            {
                yield return propertyContract;
            }

            foreach (var property in jsonObject)
            {
                if (property.Value == null)
                {
                    continue;
                }

                foreach (var result in FindPropertyContracts(
                    property.Value,
                    propertyName))
                {
                    yield return result;
                }
            }

            yield break;
        }

        if (node is not JsonArray jsonArray)
        {
            yield break;
        }

        foreach (var item in jsonArray)
        {
            if (item == null)
            {
                continue;
            }

            foreach (var result in FindPropertyContracts(item, propertyName))
            {
                yield return result;
            }
        }
    }

    private static IEnumerable<JsonObject> FindObjectContracts (
        JsonNode node,
        params string[] propertyNames)
    {
        if (node is JsonObject jsonObject)
        {
            if (jsonObject["properties"] is JsonObject properties
                && propertyNames.All(properties.ContainsKey))
            {
                yield return jsonObject;
            }

            foreach (var property in jsonObject)
            {
                if (property.Value == null)
                {
                    continue;
                }

                foreach (var result in FindObjectContracts(
                    property.Value,
                    propertyNames))
                {
                    yield return result;
                }
            }

            yield break;
        }

        if (node is not JsonArray jsonArray)
        {
            yield break;
        }

        foreach (var item in jsonArray)
        {
            if (item == null)
            {
                continue;
            }

            foreach (var result in FindObjectContracts(item, propertyNames))
            {
                yield return result;
            }
        }
    }

    private static IReadOnlyList<LifecycleExecutionState> GetExpectedErrorStates (
        LifecycleExecutionKind executionKind,
        ExecutionLifecycle lifecycle)
    {
        return lifecycle switch
        {
            ExecutionLifecycle.Active => executionKind switch
            {
                LifecycleExecutionKind.Refresh =>
                [
                    LifecycleExecutionState.Registered,
                    LifecycleExecutionState.Refreshing,
                ],
                LifecycleExecutionKind.Compile =>
                [
                    LifecycleExecutionState.Registered,
                    LifecycleExecutionState.Refreshing,
                    LifecycleExecutionState.Compiling,
                ],
                LifecycleExecutionKind.PlayEnter =>
                [
                    LifecycleExecutionState.Registered,
                    LifecycleExecutionState.Entering,
                ],
                LifecycleExecutionKind.PlayExit =>
                [
                    LifecycleExecutionState.Registered,
                    LifecycleExecutionState.Exiting,
                ],
                _ => throw new ArgumentOutOfRangeException(
                    nameof(executionKind),
                    executionKind,
                    null),
            },
            ExecutionLifecycle.Recovery =>
            [
                LifecycleExecutionState.Recovering,
                LifecycleExecutionState.Publishing,
            ],
            ExecutionLifecycle.Terminal =>
            [
                LifecycleExecutionState.Failed,
            ],
            _ => throw new ArgumentOutOfRangeException(
                nameof(lifecycle),
                lifecycle,
                null),
        };
    }

    private static void AssertAcceptsExactly<TEnum> (
        JsonObject propertyContract,
        IReadOnlyList<TEnum> candidates,
        IReadOnlyList<TEnum> expected)
        where TEnum : struct, Enum
    {
        var pattern = propertyContract["pattern"]?.GetValue<string>();
        var constant = propertyContract["const"]?.GetValue<string>();
        foreach (var candidate in candidates)
        {
            var text = TextVocabulary.GetText(candidate);
            var isAccepted = constant == text
                || (constant == null
                    && (pattern == null || Regex.IsMatch(text, pattern)));
            Assert.Equal(expected.Contains(candidate), isAccepted);
        }
    }

    private static ITerminalExecutionRef CreateFailedReference (
        ITerminalExecutionRef reference)
    {
        return new TerminalExecutionRef(
            reference.Kind,
            reference.Id,
            reference.DefinitionDigest,
            new ExecutionState(TextVocabulary.GetText(
                LifecycleExecutionState.Failed)),
            reference.StatusLocator,
            reference.TerminalRecordRef);
    }

    private static ITerminalExecutionRef CreateInvalidTerminalRecordReference (
        ITerminalExecutionRef reference,
        bool invalidKind)
    {
        return new TerminalExecutionRef(
            reference.Kind,
            reference.Id,
            reference.DefinitionDigest,
            reference.State,
            reference.StatusLocator,
            new PathArtifactRef(
                invalidKind
                    ? new ArtifactKind("ucli.invalidTerminalRecord")
                    : LifecycleExecutionArtifactContract.TerminalRecordKind,
                invalidKind
                    ? LifecycleExecutionArtifactContract.TerminalRecordMediaType
                    : new ArtifactMediaType("application/octet-stream"),
                new ArtifactPath(
                    $"lifecycle-executions/{reference.Id:N}/invalid-terminal-record.json"),
                Sha256Digest.Parse(new string('f', 64)),
                sizeBytes: 1,
                DateTimeOffset.Parse("2026-07-31T00:00:00Z")));
    }
}
