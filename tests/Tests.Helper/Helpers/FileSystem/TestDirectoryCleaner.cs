namespace MackySoft.Tests;

internal static class TestDirectoryCleaner
{
    public static void DeleteBestEffort (string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    return;
                }

                ClearReadOnlyAttributes(directoryPath);
                Directory.Delete(directoryPath, recursive: true);
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(50));
            }
        }
    }

    private static void ClearReadOnlyAttributes (string directoryPath)
    {
        foreach (string filePath in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(filePath, File.GetAttributes(filePath) & ~FileAttributes.ReadOnly);
        }

        foreach (string childDirectoryPath in Directory.EnumerateDirectories(directoryPath, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(childDirectoryPath, File.GetAttributes(childDirectoryPath) & ~FileAttributes.ReadOnly);
        }

        File.SetAttributes(directoryPath, File.GetAttributes(directoryPath) & ~FileAttributes.ReadOnly);
    }
}
