using System;
using System.IO;
using System.Xml;
using NUnit.Framework.Interfaces;
using UnityEditor.TestTools.TestRunner.Api;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Writes Unity Test Framework results XML artifacts to filesystem paths. </summary>
    internal sealed class UnityTestResultsXmlWriter : IUnityTestResultsXmlWriter
    {
        private const string NUnitVersion = "3.5.0.0";

        private const string TestRunNodeName = "test-run";

        private const string TimeFormat = "u";

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

            Directory.CreateDirectory(resultsDirectoryPath);
            using (var streamWriter = File.CreateText(resultsXmlPath))
            {
                WriteResultToStream(testResult, streamWriter);
            }
        }

        /// <summary> Writes one Unity test result adaptor to a stream writer using NUnit-compatible XML schema. </summary>
        /// <param name="testResult"> The Unity test result adaptor. </param>
        /// <param name="streamWriter"> The destination stream writer. </param>
        private static void WriteResultToStream (
            ITestResultAdaptor testResult,
            StreamWriter streamWriter)
        {
            var settings = new XmlWriterSettings
            {
                Indent = true,
                NewLineOnAttributes = false,
            };
            using (var xmlWriter = XmlWriter.Create(streamWriter, settings))
            {
                WriteResultsToXml(testResult, xmlWriter);
            }
        }

        /// <summary> Emits one NUnit-style <c>test-run</c> XML document from Unity test result adaptor. </summary>
        /// <param name="testResult"> The Unity test result adaptor. </param>
        /// <param name="xmlWriter"> The XML writer. </param>
        private static void WriteResultsToXml (
            ITestResultAdaptor testResult,
            XmlWriter xmlWriter)
        {
            var total = testResult.PassCount + testResult.FailCount + testResult.SkipCount + testResult.InconclusiveCount;
            var testRunNode = new TNode(TestRunNodeName);
            testRunNode.AddAttribute("id", "2");
            testRunNode.AddAttribute("testcasecount", total.ToString());
            testRunNode.AddAttribute("result", testResult.ResultState.ToString());
            testRunNode.AddAttribute("total", total.ToString());
            testRunNode.AddAttribute("passed", testResult.PassCount.ToString());
            testRunNode.AddAttribute("failed", testResult.FailCount.ToString());
            testRunNode.AddAttribute("inconclusive", testResult.InconclusiveCount.ToString());
            testRunNode.AddAttribute("skipped", testResult.SkipCount.ToString());
            testRunNode.AddAttribute("asserts", testResult.AssertCount.ToString());
            testRunNode.AddAttribute("engine-version", NUnitVersion);
            testRunNode.AddAttribute("clr-version", Environment.Version.ToString());
            testRunNode.AddAttribute("start-time", testResult.StartTime.ToString(TimeFormat));
            testRunNode.AddAttribute("end-time", testResult.EndTime.ToString(TimeFormat));
            testRunNode.AddAttribute("duration", testResult.Duration.ToString());

            var resultNode = testResult.ToXml();
            testRunNode.ChildNodes.Add(resultNode);
            testRunNode.WriteTo(xmlWriter);
        }
    }
}
