using System.Text.Json;
using System.Text.Json.Nodes;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Tests.Execution.Lifecycle;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Protocol;

public sealed class IpcLifecycleExecutionContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void StartControlPlaneContracts_RoundTripDurableBinding ()
    {
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.Refresh);
        var request = new IpcLifecycleExecutionStartRequest(
            definition.Kind,
            LifecycleExecutionContractTestFactory.ExecutionId,
            LifecycleExecutionDefinitionDigest.Calculate(definition),
            LifecycleExecutionContractTestFactory.DeadlineUtc,
            LifecycleExecutionContractTestFactory.StartedAtUtc);
        var response = new IpcLifecycleExecutionStartResponse(
            LifecycleExecutionContractTestFactory.CreateStart(
                LifecycleExecutionKind.Refresh));

        var requestJson = IpcPayloadCodec.SerializeToElement(request);
        var responseJson = IpcPayloadCodec.SerializeToElement(response);

        JsonAssert.For(requestJson)
            .HasString("kind", "refresh")
            .HasString(
                "executionId",
                LifecycleExecutionContractTestFactory.ExecutionId.ToString("D"))
            .HasString(
                "definitionDigest",
                LifecycleExecutionDefinitionDigest.Calculate(definition).ToString());
        JsonAssert.For(responseJson)
            .HasProperty("start", start => start
                .HasProperty("lifecycleExecutionRef", reference => reference
                    .HasString("kind", "refresh")
                    .HasString("state", "registered")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RefreshResponses_SerializeTypedSuccessAndRequiredNullableFailure ()
    {
        var success = new IpcRefreshResponse(
            LifecycleExecutionContractTestFactory.Project,
            LifecycleExecutionContractTestFactory.CreateReference(
                LifecycleExecutionKind.Refresh,
                ExecutionLifecycle.Terminal,
                LifecycleExecutionState.Completed),
            LifecycleExecutionContractTestFactory.CreateRefreshResult());
        var failure = new IpcRefreshErrorResponse(
            LifecycleExecutionContractTestFactory.Project,
            lifecycleExecutionRef: null,
            ExecutionApplicationState.NotApplied,
            refresh: null,
            observedLifecycle: null,
            readPostcondition: null);

        var successJson = IpcPayloadCodec.SerializeToElement(success);
        var failureJson = IpcPayloadCodec.SerializeToElement(failure);

        JsonAssert.For(successJson)
            .HasProperty("lifecycleExecutionRef", reference => reference
                .HasString("kind", "refresh")
                .HasString("state", "completed"))
            .HasProperty("result", result => result
                .HasProperty("refresh", refresh => refresh
                    .HasInt32("domainReloadGenerationBefore", 20)
                    .HasInt32("domainReloadGenerationAfter", 21)));
        Assert.Equal(
            JsonValueKind.Null,
            failureJson.GetProperty("lifecycleExecutionRef").ValueKind);
        Assert.False(failureJson.TryGetProperty("result", out _));
        Assert.Equal(JsonValueKind.Null, failureJson.GetProperty("refresh").ValueKind);
        Assert.Equal(
            JsonValueKind.Null,
            failureJson.GetProperty("observedLifecycle").ValueKind);
        Assert.False(failureJson.TryGetProperty("readPostcondition", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RefreshErrorResponse_WhenTerminalPublicationFails_RoundTripsProjectedEvidence ()
    {
        var typedResult =
            LifecycleExecutionContractTestFactory.CreateRefreshResult();
        var response = new IpcRefreshErrorResponse(
            LifecycleExecutionContractTestFactory.Project,
            LifecycleExecutionContractTestFactory.CreateReference(
                LifecycleExecutionKind.Refresh,
                ExecutionLifecycle.Recovery,
            LifecycleExecutionState.Publishing),
            ExecutionApplicationState.Applied,
            new RefreshLifecycleStartEvidence(
                typedResult.Refresh.StartedAtUtc,
                typedResult.Refresh.DomainReloadGenerationBefore),
            typedResult.Lifecycle,
            typedResult.ReadPostcondition);

        var element = IpcPayloadCodec.SerializeToElement(response);
        var success = IpcPayloadCodec.TryDeserialize(
            element,
            out IpcRefreshErrorResponse roundTripped,
            out var error);

        Assert.True(success, error.Message);
        Assert.False(element.TryGetProperty("result", out _));
        Assert.Equal(
            typedResult.Refresh.StartedAtUtc,
            roundTripped.Refresh!.StartedAtUtc);
        Assert.Equal(typedResult.Lifecycle, roundTripped.ObservedLifecycle);
        Assert.Equal(
            typedResult.ReadPostcondition,
            roundTripped.ReadPostcondition);
        Assert.Equal(
            ExecutionLifecycle.Recovery,
            roundTripped.LifecycleExecutionRef!.Lifecycle);
        Assert.Equal(
            TextVocabulary.GetText(LifecycleExecutionState.Publishing),
            roundTripped.LifecycleExecutionRef.State.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RefreshErrorResponse_WhenApplicationStateIsPartiallyApplied_RejectsValue ()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IpcRefreshErrorResponse(
                LifecycleExecutionContractTestFactory.Project,
                lifecycleExecutionRef: null,
                ExecutionApplicationState.PartiallyApplied,
                refresh: null,
                observedLifecycle: null,
                readPostcondition: null));

        Assert.Equal("applicationState", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PlayErrorResponse_WhenSuccessfulResultHasNoPublishableTerminal_RejectsReference ()
    {
        var result = LifecycleExecutionContractTestFactory.CreatePlayResult(
            PlayLifecycleTransitionCommand.Enter);
        var references = new ExecutionRef[]
        {
            LifecycleExecutionContractTestFactory.CreateReference(
                LifecycleExecutionKind.PlayEnter,
                ExecutionLifecycle.Active,
                LifecycleExecutionState.Entering),
            LifecycleExecutionContractTestFactory.CreateReference(
                LifecycleExecutionKind.PlayEnter,
                ExecutionLifecycle.Recovery,
                LifecycleExecutionState.Recovering),
        };

        Assert.All(references, reference =>
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                new IpcPlayTransitionErrorResponse(
                    reference,
                    ExecutionApplicationState.Applied,
                    result));

            Assert.Equal("lifecycleExecutionRef", exception.ParamName);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PlayErrorResponse_WhenSuccessfulResultHasCompletedTerminal_RejectsReference ()
    {
        var reference =
            LifecycleExecutionContractTestFactory.CreateReference(
                LifecycleExecutionKind.PlayEnter,
                ExecutionLifecycle.Terminal,
                LifecycleExecutionState.Completed);
        var result = LifecycleExecutionContractTestFactory.CreatePlayResult(
            PlayLifecycleTransitionCommand.Enter);

        var exception = Assert.Throws<ArgumentException>(() =>
            new IpcPlayTransitionErrorResponse(
                reference,
                ExecutionApplicationState.Applied,
                result));
        var valid = IpcPayloadCodec.SerializeToElement(
            new IpcPlayTransitionErrorResponse(
                LifecycleExecutionContractTestFactory.CreateReference(
                    LifecycleExecutionKind.PlayEnter,
                    ExecutionLifecycle.Terminal,
                    LifecycleExecutionState.Failed),
                ExecutionApplicationState.Applied,
                result));
        var invalid = JsonNode.Parse(valid.GetRawText())!.AsObject();
        invalid["lifecycleExecutionRef"]!["state"] = "completed";
        var deserialized = IpcPayloadCodec.TryDeserialize(
            JsonSerializer.SerializeToElement(invalid),
            out IpcPlayTransitionErrorResponse _,
            out _);

        Assert.Equal("lifecycleExecutionRef", exception.ParamName);
        Assert.False(deserialized);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PlayErrorResponse_WhenSuccessfulResultHasFailedTerminal_AcceptsReference ()
    {
        var reference =
            LifecycleExecutionContractTestFactory.CreateReference(
                LifecycleExecutionKind.PlayExit,
                ExecutionLifecycle.Terminal,
                LifecycleExecutionState.Failed);
        var result = LifecycleExecutionContractTestFactory.CreatePlayResult(
            PlayLifecycleTransitionCommand.Exit);

        var response = new IpcPlayTransitionErrorResponse(
            reference,
            ExecutionApplicationState.Applied,
            result);
        var element = IpcPayloadCodec.SerializeToElement(response);

        Assert.Equal(reference, response.LifecycleExecutionRef);
        Assert.Equal(result, response.Result);
        JsonAssert.For(element)
            .HasProperty("lifecycleExecutionRef", serializedReference => serializedReference
                .HasString("lifecycle", "terminal")
                .HasString("state", "failed"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PlayErrorResponse_WhenApplicationStateIsPartiallyApplied_RejectsValue ()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IpcPlayTransitionErrorResponse(
                LifecycleExecutionContractTestFactory.CreateReference(
                    LifecycleExecutionKind.PlayEnter,
                    ExecutionLifecycle.Terminal,
                    LifecycleExecutionState.Failed),
                ExecutionApplicationState.PartiallyApplied,
                LifecycleExecutionContractTestFactory.CreatePlayResult(
                    PlayLifecycleTransitionCommand.Enter)));

        Assert.Equal("applicationState", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PlayErrorResponse_WhenSuccessfulResultAwaitsPublication_AcceptsRecoveryReference ()
    {
        var reference =
            LifecycleExecutionContractTestFactory.CreateReference(
                LifecycleExecutionKind.PlayExit,
                ExecutionLifecycle.Recovery,
                LifecycleExecutionState.Publishing);
        var result = LifecycleExecutionContractTestFactory.CreatePlayResult(
            PlayLifecycleTransitionCommand.Exit);

        var response = new IpcPlayTransitionErrorResponse(
            reference,
            ExecutionApplicationState.Applied,
            result);

        Assert.Equal(reference, response.LifecycleExecutionRef);
        Assert.Equal(result, response.Result);
    }

    [Theory]
    [InlineData(
        PlayLifecycleTransitionCommand.Enter,
        PlayLifecycleTransitionOutcome.AlreadyEntered)]
    [InlineData(
        PlayLifecycleTransitionCommand.Exit,
        PlayLifecycleTransitionOutcome.AlreadyExited)]
    [Trait("Size", "Small")]
    public void PlayErrorResponse_WhenAlreadySatisfiedResultAwaitsPublication_RequiresNotApplied (
        PlayLifecycleTransitionCommand transition,
        PlayLifecycleTransitionOutcome outcome)
    {
        var kind = transition == PlayLifecycleTransitionCommand.Enter
            ? LifecycleExecutionKind.PlayEnter
            : LifecycleExecutionKind.PlayExit;
        var reference =
            LifecycleExecutionContractTestFactory.CreateReference(
                kind,
                ExecutionLifecycle.Recovery,
                LifecycleExecutionState.Publishing);
        var ordinaryResult =
            LifecycleExecutionContractTestFactory.CreatePlayResult(transition);
        var result = new PlayLifecycleTransitionResult(
            transition,
            outcome,
            ordinaryResult.Before,
            After: ordinaryResult.Before,
            Observed: null,
            ApplicationState: null);

        var response = new IpcPlayTransitionErrorResponse(
            reference,
            ExecutionApplicationState.NotApplied,
            result);
        var exception = Assert.Throws<ArgumentException>(() =>
            new IpcPlayTransitionErrorResponse(
                reference,
                ExecutionApplicationState.Applied,
                result));

        Assert.Equal(ExecutionApplicationState.NotApplied, response.ApplicationState);
        Assert.Equal("applicationState", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ErrorResponse_WhenExecutionWasNotRegistered_RequiresNotApplied ()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new IpcCompileErrorResponse(
                lifecycleExecutionRef: null,
                ExecutionApplicationState.Indeterminate,
                result: null,
                observedLifecycle: null));

        Assert.Equal("applicationState", exception.ParamName);
    }

    [Theory]
    [InlineData("otherArtifact", "application/json")]
    [InlineData("lifecycleExecutionTerminalRecord", "text/plain")]
    [Trait("Size", "Small")]
    public void SuccessResponse_WhenTerminalArtifactContractDiffers_RejectsReference (
        string artifactKind,
        string mediaType)
    {
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.Refresh);
        var reference = new TerminalExecutionRef(
            definition.ExecutionKind,
            LifecycleExecutionContractTestFactory.ExecutionId,
            LifecycleExecutionDefinitionDigest.Calculate(definition),
            new ExecutionState(
                TextVocabulary.GetText(LifecycleExecutionState.Completed)),
            statusLocator: null,
            new PathArtifactRef(
                new ArtifactKind(artifactKind),
                new ArtifactMediaType(mediaType),
                new ArtifactPath("lifecycle-executions/terminal-record.json"),
                Sha256Digest.Parse(new string('f', 64)),
                sizeBytes: 512,
                LifecycleExecutionContractTestFactory.StartedAtUtc));

        var exception = Assert.Throws<ArgumentException>(() =>
            new IpcRefreshResponse(
                LifecycleExecutionContractTestFactory.Project,
                reference,
                LifecycleExecutionContractTestFactory.CreateRefreshResult()));

        Assert.Equal("lifecycleExecutionRef", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void LifecycleExecutionErrorCodes_ExposeStableMachineValues ()
    {
        Assert.Equal(
            "LIFECYCLE_EXECUTION_DEFINITION_CONFLICT",
            LifecycleExecutionErrorCodes.DefinitionConflict.Value);
        Assert.Equal(
            "LIFECYCLE_EXECUTION_PROJECT_MISMATCH",
            LifecycleExecutionErrorCodes.ProjectMismatch.Value);
        Assert.Equal(
            "LIFECYCLE_EXECUTION_HOST_MISMATCH",
            LifecycleExecutionErrorCodes.HostMismatch.Value);
        Assert.Equal(
            "LIFECYCLE_EXECUTION_UNITY_EXITED",
            LifecycleExecutionErrorCodes.UnityExited.Value);
        Assert.Equal(
            "LIFECYCLE_EXECUTION_GENERATION_MISMATCH",
            LifecycleExecutionErrorCodes.GenerationMismatch.Value);
        Assert.Equal(
            "LIFECYCLE_EXECUTION_DEADLINE_EXCEEDED",
            LifecycleExecutionErrorCodes.DeadlineExceeded.Value);
        Assert.Equal(
            "LIFECYCLE_EXECUTION_TERMINAL_PUBLICATION_FAILED",
            LifecycleExecutionErrorCodes.TerminalPublicationFailed.Value);
    }
}
