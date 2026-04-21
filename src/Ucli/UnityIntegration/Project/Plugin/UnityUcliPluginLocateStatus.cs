namespace MackySoft.Ucli.UnityIntegration.Project.Plugin;

/// <summary> Defines the outcome of one uCLI Unity plugin marker lookup. </summary>
internal enum UnityUcliPluginLocateStatus
{
    Found,
    NotFound,
    MultipleFound,
    InvalidMarker,
}