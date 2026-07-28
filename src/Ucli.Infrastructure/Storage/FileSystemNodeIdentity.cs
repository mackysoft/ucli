namespace MackySoft.Ucli.Infrastructure.Storage;

/// <summary> Identifies one physical filesystem node, its node kind, and its directory-entry link count. </summary>
internal readonly record struct FileSystemNodeIdentity (
    ulong VolumeOrDevice,
    ulong Node,
    ulong LinkCount,
    bool IsRegularFile,
    bool IsDirectory,
    bool IsReparsePoint)
{
    public bool IsSamePhysicalNodeAs (FileSystemNodeIdentity other)
    {
        return VolumeOrDevice == other.VolumeOrDevice
            && Node == other.Node
            && IsRegularFile == other.IsRegularFile
            && IsDirectory == other.IsDirectory
            && IsReparsePoint == other.IsReparsePoint;
    }
}
