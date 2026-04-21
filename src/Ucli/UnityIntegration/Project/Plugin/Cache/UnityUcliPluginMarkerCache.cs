namespace MackySoft.Ucli.UnityIntegration.Project.Plugin.Cache;

/// <summary> Represents persisted runtime metadata for one resolved uCLI Unity plugin marker. </summary>
internal sealed record UnityUcliPluginMarkerCache (
    string ProjectRelativeMarkerPath,
    string PluginId,
    int ProtocolVersion);