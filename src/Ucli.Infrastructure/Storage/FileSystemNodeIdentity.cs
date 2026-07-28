namespace MackySoft.Ucli.Infrastructure.Storage;

/// <summary> Carries the complete filesystem-specific identifier of one physical node. </summary>
internal readonly record struct FileSystemNodeIdentifier (
    ulong Low,
    ulong High);

/// <summary> Identifies one physical filesystem node, its node kind, and its directory-entry link count. </summary>
internal readonly record struct FileSystemNodeIdentity (
    ulong VolumeOrDevice,
    FileSystemNodeIdentifier NodeIdentifier,
    ulong LinkCount,
    bool IsRegularFile,
    bool IsDirectory,
    bool IsReparsePoint)
{
    public bool IsSamePhysicalNodeAs (FileSystemNodeIdentity other)
    {
        return VolumeOrDevice == other.VolumeOrDevice
            && NodeIdentifier == other.NodeIdentifier
            && IsRegularFile == other.IsRegularFile
            && IsDirectory == other.IsDirectory
            && IsReparsePoint == other.IsReparsePoint;
    }
}
