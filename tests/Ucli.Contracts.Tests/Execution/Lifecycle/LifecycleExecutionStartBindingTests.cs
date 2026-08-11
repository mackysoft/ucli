using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Execution.Lifecycle;

public sealed class LifecycleExecutionStartBindingTests
{
    [Theory]
    [InlineData(LifecycleExecutionKind.Refresh, typeof(IpcRefreshRequest))]
    [InlineData(LifecycleExecutionKind.Compile, typeof(IpcCompileRequest))]
    [InlineData(LifecycleExecutionKind.PlayEnter, typeof(IpcPlayEnterRequest))]
    [InlineData(LifecycleExecutionKind.PlayExit, typeof(IpcPlayExitRequest))]
    [Trait("Size", "Small")]
    public void TypedRequest_SerializesTheSharedStartBinding (
        LifecycleExecutionKind kind,
        Type requestType)
    {
        var start = LifecycleExecutionContractTestFactory.CreateStart(kind);
        var request = Activator.CreateInstance(requestType, start)
            ?? throw new InvalidOperationException("Could not create typed request.");

        var json = JsonSerializer.SerializeToElement(
            request,
            requestType,
            IpcJsonSerializerOptions.StrictPropertyNames);

        var startJson = json.GetProperty("start");
        JsonAssert.For(startJson)
            .HasString(
                "startedAtUtc",
                "2026-07-31T00:00:00+00:00")
            .HasString(
                "deadlineUtc",
                "2026-07-31T00:05:00+00:00")
            .HasProperty("lifecycleExecutionRef", reference => reference
                .HasString("kind", TextVocabulary.GetText(kind))
                .HasString("lifecycle", "active")
                .HasString("state", "registered"))
            .HasProperty("host", host => host
                .HasString(
                    "editorInstanceId",
                    LifecycleExecutionContractTestFactory.Host.EditorInstanceId.ToString("D"))
                .HasString(
                    "firstEndpointRegistrationGenerationId",
                    LifecycleExecutionContractTestFactory.Host
                        .FirstEndpointRegistrationGenerationId.ToString("D"))
                .HasString(
                    "currentEndpointRegistrationGenerationId",
                    LifecycleExecutionContractTestFactory.Host
                        .CurrentEndpointRegistrationGenerationId.ToString("D")));
        Assert.Single(json.EnumerateObject());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenDefinitionDigestDoesNotMatchFixedKind_RejectsBeforeBinding ()
    {
        var reference = new ActiveExecutionRef(
            new ExecutionKind("refresh"),
            LifecycleExecutionContractTestFactory.ExecutionId,
            Sha256Digest.Parse(new string('a', 64)),
            new ExecutionState("registered"),
            new ExecutionStatusLocator("lifecycle-executions/status.json"));

        var exception = Assert.Throws<ArgumentException>(() =>
            new LifecycleExecutionStartBinding(
                reference,
                LifecycleExecutionContractTestFactory.Project,
                LifecycleExecutionContractTestFactory.Host,
                LifecycleExecutionContractTestFactory.StartedGeneration,
                LifecycleExecutionContractTestFactory.DeadlineUtc,
                LifecycleExecutionContractTestFactory.StartedAtUtc));

        Assert.Equal("lifecycleExecutionRef", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenReferenceIsTerminal_RejectsBinding ()
    {
        var terminal = LifecycleExecutionContractTestFactory.CreateReference(
            LifecycleExecutionKind.Refresh,
            ExecutionLifecycle.Terminal,
            LifecycleExecutionState.Completed);
        Assert.Equal(
            "lifecycleExecutionRef",
            Assert.Throws<ArgumentException>(() => CreateBinding(terminal)).ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TypedRequest_WhenBindingKindDiffers_RejectsValue ()
    {
        var compileStart = LifecycleExecutionContractTestFactory.CreateStart(
            LifecycleExecutionKind.Compile);

        var exception = Assert.Throws<ArgumentException>(
            () => new IpcRefreshRequest(compileStart));

        Assert.Equal("start", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void HostRegistration_WhenAnIdentityIsEmpty_RejectsValue ()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new LifecycleExecutionHostRegistration(
                new ProcessIdentity(42, 100),
                Guid.Empty,
                Guid.NewGuid(),
                Guid.NewGuid()));

        Assert.Equal("editorInstanceId", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void StartBinding_WhenDeadlineDoesNotFollowStart_RejectsValue ()
    {
        var valid = LifecycleExecutionContractTestFactory.CreateStart(
            LifecycleExecutionKind.Refresh);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LifecycleExecutionStartBinding(
                valid.LifecycleExecutionRef,
                valid.Project,
                valid.Host,
                valid.StartedGeneration,
                valid.StartedAtUtc,
                valid.StartedAtUtc));

        Assert.Equal("deadlineUtc", exception.ParamName);
    }

    private static LifecycleExecutionStartBinding CreateBinding (
        ExecutionRef executionRef)
    {
        return new LifecycleExecutionStartBinding(
            executionRef,
            LifecycleExecutionContractTestFactory.Project,
            LifecycleExecutionContractTestFactory.Host,
            LifecycleExecutionContractTestFactory.StartedGeneration,
            LifecycleExecutionContractTestFactory.DeadlineUtc,
            LifecycleExecutionContractTestFactory.StartedAtUtc);
    }
}
