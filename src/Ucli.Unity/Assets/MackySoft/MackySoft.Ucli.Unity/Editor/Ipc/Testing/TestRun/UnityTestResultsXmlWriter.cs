using MackySoft.Ucli.Infrastructure.Storage;
using System;
using System.IO;
using MackySoft.Ucli.Contracts.Storage;
using UnityEditor.TestTools.TestRunner.Api;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Writes Unity Test Framework results XML artifacts to filesystem paths. </summary>
    internal sealed class UnityTestResultsXmlWriter : IUnityTestResultsXmlWriter
    {
        /// <summary> Writes one Unity test result adaptor to a results XML file path. </summary>
        /// <param name="testResult"> The Unity test result adaptor. </param>
        /// <param name="resultsXmlPath"> The output XML path. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="testResult" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="resultsXmlPath" /> is empty. </exception>
        /// <exception cref="InvalidOperationException"> Thrown when output directory path cannot be resolved. </exception>
        public void Write (
            ITestResultAdaptor testResult,
            string resultsXmlPath)
        {
            if (testResult == null)
            {
                throw new ArgumentNullException(nameof(testResult));
            }

            if (string.IsNullOrWhiteSpace(resultsXmlPath))
            {
                throw new ArgumentException("resultsXmlPath must not be empty.", nameof(resultsXmlPath));
            }

            var resultsDirectoryPath = Path.GetDirectoryName(resultsXmlPath);
            if (string.IsNullOrWhiteSpace(resultsDirectoryPath))
            {
                throw new InvalidOperationException($"resultsXmlPath directory could not be resolved: {resultsXmlPath}");
            }

            UcliLocalStorageBootstrapper.EnsureInitialized(resultsDirectoryPath);
            Directory.CreateDirectory(resultsDirectoryPath);
            TestRunnerApi.SaveResultToFile(testResult, resultsXmlPath);
            if (!File.Exists(resultsXmlPath))
            {
                throw new IOException($"Failed to write test results xml: {resultsXmlPath}");
            }
        }
    }
}