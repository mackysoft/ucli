namespace MackySoft.Ucli.Infrastructure.Storage;

/// <summary> Carries the complete filesystem-specific identifier of one physical node. </summary>
internal readonly record struct FileSystemNodeIdentifier (
    ulong Low,
    ulong High);

/// <summary> Carries the node characteristics needed to reject links and non-file destinations. </summary>
internal readonly record struct FileSystemNodeClassification (
    bool IsRegularFile,
    bool IsDirectory,
    bool IsReparsePoint);

/// <summary> Identifies one physical filesystem node, its node kind, and its directory-entry link count. </summary>
internal readonly record struct FileSystemNodeIdentity (
    ulong VolumeOrDevice,
    FileSystemNodeIdentifier NodeIdentifier,
    ulong LinkCount,
    FileSystemNodeClassification Classification)
{
    public bool IsRegularFile => Classification.IsRegularFile;

    public bool IsDirectory => Classification.IsDirectory;

    public bool IsReparsePoint => Classification.IsReparsePoint;

    public bool IsSamePhysicalNodeAs (FileSystemNodeIdentity other)
    {
        return VolumeOrDevice == other.VolumeOrDevice
            && NodeIdentifier == other.NodeIdentifier
            && Classification == other.Classification;
    }
}
