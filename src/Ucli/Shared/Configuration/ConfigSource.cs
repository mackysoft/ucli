namespace MackySoft.Ucli.Shared.Configuration;

/// <summary> Identifies where <see cref="UcliConfig" /> values were loaded from. </summary>
internal enum ConfigSource
{
    /// <summary> The configuration file was not found and default values were applied. </summary>
    Default = 0,

    /// <summary> The configuration file was loaded from disk. </summary>
    File = 1,
}