using MackySoft.FileSystem;

namespace MackySoft.Ucli.Application.Shared.Configuration;

/// <summary> Represents one project-provided Program Preset registration. </summary>
internal sealed record ProgramPresetRegistration (string Description, RootRelativePath ProgramPath);
