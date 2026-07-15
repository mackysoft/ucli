using System;
using System.IO;

namespace MackySoft.Ucli.Unity.Tests
{
    internal static class ProjectPathTestValues
    {
        private static readonly string FileSystemRoot = Path.GetPathRoot(Environment.CurrentDirectory)
            ?? throw new InvalidOperationException("The test process current directory must have a file-system root.");

        public static string RepositoryRoot { get; } = Create("repo");

        public static string RepositoryUnityProject { get; } = Path.Combine(RepositoryRoot, "UnityProject");

        public static string WorkspaceRoot { get; } = Create("workspace");

        public static string WorkspaceUnityProject { get; } = Path.Combine(WorkspaceRoot, "UnityProject");

        private static string Create (params string[] segments)
        {
            var path = FileSystemRoot;
            foreach (var segment in segments)
            {
                path = Path.Combine(path, segment);
            }

            return path;
        }
    }
}
