using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Context
{
    /// <summary> Represents shared foundation values for init/status command execution. </summary>
    /// <param name="UnityProject"> The resolved UnityProject context. </param>
    /// <param name="Config"> The loaded config values. </param>
    /// <param name="ConfigSource"> The source where config values were loaded from. </param>
    internal sealed record InitStatusContext (
        ResolvedUnityProjectContext UnityProject,
        UcliConfig Config,
        ConfigSource ConfigSource);
}
