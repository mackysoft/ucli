namespace MackySoft.Ucli.UnityIntegration.Project.Plugin.Marker;

/// <summary> Defines the expected uCLI Unity plugin marker contract. </summary>
internal static class UnityUcliPluginMarkerContract
{
    /// <summary> Gets the plugin marker file name. </summary>
    public const string MarkerFileName = "ucli-plugin.json";

    /// <summary> Gets the expected Unity plugin package identifier. </summary>
    public const string ExpectedPluginId = "com.mackysoft.ucli.unity";

    /// <summary> Gets the expected marker protocol version. </summary>
    public const int ExpectedProtocolVersion = 1;
}
