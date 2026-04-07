using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.TestRun.Artifacts;
using MackySoft.Ucli.TestRun.Configuration;

namespace MackySoft.Ucli.TestRun.Execution;

/// <summary> Encodes daemon <c>test.run</c> IPC request envelopes from resolved test-run values. </summary>
internal static class IpcDaemonTestRunRequestCodec
{
    /// <summary> Creates one daemon <c>test.run</c> IPC request envelope. </summary>
    /// <param name="configuration"> The resolved test-run configuration. </param>
    /// <param name="artifactPaths"> The run artifact paths. </param>
    /// <param name="sessionToken"> The daemon session token. </param>
    /// <returns> The encoded request envelope. </returns>
    public static IpcRequest CreateRequest (
        ResolvedTestRunConfiguration configuration,
        ArtifactPaths artifactPaths,
        string sessionToken,
        bool failFast)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(artifactPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        if (!TestRunArtifactValidator.TryValidateOutputPaths(artifactPaths, out var artifactPathError))
        {
            throw new ArgumentException(artifactPathError!, nameof(artifactPaths));
        }

        var payload = IpcPayloadCodec.SerializeToElement(
            new IpcTestRunRequest(
                TestPlatform: IpcTestRunPlatformCodec.ToValue(configuration.TestPlatform),
                BuildTarget: configuration.BuildTarget,
                TestFilter: configuration.TestFilter,
                TestCategories: configuration.TestCategories,
                AssemblyNames: configuration.AssemblyNames,
                TestSettingsPath: configuration.TestSettingsPath,
                ResultsXmlPath: artifactPaths.ResultsXmlPath,
                EditorLogPath: artifactPaths.EditorLogPath,
                FailFast: failFast));
        return new IpcRequest(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: $"test-run-{Guid.NewGuid():N}",
            SessionToken: sessionToken,
            Method: IpcMethodNames.TestRun,
            Payload: payload);
    }
}