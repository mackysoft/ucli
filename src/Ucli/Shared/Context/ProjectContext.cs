using MackySoft.Ucli.Shared.Configuration;

namespace MackySoft.Ucli.Shared.Context;

/// <summary> Represents shared project/config values resolved for command execution. </summary>
/// <param name="UnityProject"> The resolved UnityProject context. </param>
/// <param name="Config"> The loaded config values. </param>
/// <param name="ConfigSource"> The source where config values were loaded from. </param>
internal sealed record ProjectContext (
    ResolvedUnityProjectContext UnityProject,
    UcliConfig Config,
    ConfigSource ConfigSource);