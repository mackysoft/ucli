using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;

namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal static class LifecycleExecutionIpcTestResponseFactory
{
    private static readonly ProjectFingerprint ProjectFingerprint =
        ProjectFingerprintTestFactory.Create("lifecycle-execution-ipc");

    private static readonly LifecycleExecutionHostRegistration Host =
        new(
            new ProcessIdentity(4200, 123456),
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Guid.Parse("10000000-0000-0000-0000-000000000001"));

    public static IpcResponse? TryCreateResponse (IpcRequestEnvelope request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(
                request.Method,
                TextVocabulary.GetText(UnityIpcMethod.LifecycleStart),
                StringComparison.Ordinal))
        {
            return null;
        }

        Assert.True(IpcPayloadCodec.TryDeserialize(
            request.Payload,
            out IpcLifecycleExecutionStartRequest startRequest,
            out var readError),
            readError.Message);
        return CreateResponse(
            request,
            CreateStartBinding(startRequest));
    }

    public static IpcResponse CreateResponse (
        IpcRequestEnvelope request,
        LifecycleExecutionStartBinding start)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(start);
        var payload = IpcPayloadCodec.SerializeToElement(
            new IpcLifecycleExecutionStartResponse(start));
        return new IpcResponse(
            IpcProtocol.CurrentVersion,
            request.RequestId,
            IpcResponseStatus.Ok,
            payload,
            []);
    }

    public static LifecycleExecutionStartBinding CreateStartBinding (
        IpcLifecycleExecutionStartRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var definition = new LifecycleExecutionDefinition(request.Kind);
        return new LifecycleExecutionStartBinding(
            new ActiveExecutionRef(
                definition.ExecutionKind,
                request.ExecutionId,
                request.DefinitionDigest,
                new ExecutionState(TextVocabulary.GetText(
                    LifecycleExecutionState.Registered)),
                new ExecutionStatusLocator(
                    $"lifecycle-executions/{request.ExecutionId:N}/status.json")),
            new UnityProjectIdentity(
                "/workspace/UnityProject",
                ProjectFingerprint,
                "6000.1.4f1"),
            Host,
            new UnityEditorGenerationSnapshot(10, 20, 30, 40),
            request.DeadlineUtc,
            request.StartedAtUtc);
    }

    public static async ValueTask<LifecycleExecutionStartBinding> PersistStartAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest dispatchRequest)
    {
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        var startRequest = dispatchRequest.CreateLifecycleStartRequest();
        var request = new IpcRequestEnvelope(
            IpcProtocol.CurrentVersion,
            Guid.NewGuid(),
            sessionToken: null,
            TextVocabulary.GetText(UnityIpcMethod.LifecycleStart),
            IpcPayloadCodec.SerializeToElement(startRequest),
            TextVocabulary.GetText(IpcResponseMode.Single),
            startRequest.DeadlineUtc,
            requestDeadlineRemainingMilliseconds: 1000);
        return await PersistStartAsync(
            unityProject,
            request);
    }

    public static async ValueTask<LifecycleExecutionStartBinding> PersistStartAsync (
        ResolvedUnityProjectContext unityProject,
        IpcRequestEnvelope request)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(request);
        Assert.Equal(
            UnityIpcMethod.LifecycleStart,
            IpcRequestAssert.ParseMethod(request));
        Assert.True(IpcPayloadCodec.TryDeserialize(
            request.Payload,
            out IpcLifecycleExecutionStartRequest startRequest,
            out var readError),
            readError.Message);

        var store = FileLifecycleExecutionStore.CreateForProject(
            unityProject.UnityProjectRoot,
            unityProject.ProjectFingerprint);
        var startResult = await store.StartAsync(
            new LifecycleExecutionDefinition(startRequest.Kind),
            startRequest.ExecutionId,
            startRequest.DefinitionDigest,
            new UnityProjectIdentity(
                unityProject.UnityProjectRoot.Value,
                unityProject.ProjectFingerprint,
                unityProject.UnityVersion),
            Host,
            new UnityEditorGenerationSnapshot(10, 20, 30, 40),
            startRequest.DeadlineUtc,
            startRequest.StartedAtUtc,
            CancellationToken.None);
        Assert.True(startResult.IsSuccess);
        return Assert.IsType<LifecycleExecutionStartBinding>(
            startResult.Binding);
    }
}
