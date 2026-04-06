using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Orchestrates daemon <c>test.run</c> execution and artifact export. </summary>
    internal sealed class UnityTestRunService : IUnityTestRunService
    {
        private readonly IUnityTestRunRequestContextFactory requestContextFactory;

        private readonly IUnityTestRunner unityTestRunner;

        private readonly IUnityTestResultsXmlWriter testResultsXmlWriter;

        private readonly IEditorLogRangeExporter editorLogRangeExporter;

        private readonly IUnityEditorReadinessGate readinessGate;

        /// <summary> Initializes a new instance of the <see cref="UnityTestRunService" /> class. </summary>
        /// <param name="requestContextFactory"> The request-context factory dependency. </param>
        /// <param name="unityTestRunner"> The Unity test runner dependency. </param>
        /// <param name="testResultsXmlWriter"> The test-results XML writer dependency. </param>
        /// <param name="editorLogRangeExporter"> The editor-log range exporter dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
        public UnityTestRunService (
            IUnityTestRunRequestContextFactory requestContextFactory,
            IUnityTestRunner unityTestRunner,
            IUnityTestResultsXmlWriter testResultsXmlWriter,
            IEditorLogRangeExporter editorLogRangeExporter,
            IUnityEditorReadinessGate readinessGate)
        {
            this.requestContextFactory = requestContextFactory ?? throw new ArgumentNullException(nameof(requestContextFactory));
            this.unityTestRunner = unityTestRunner ?? throw new ArgumentNullException(nameof(unityTestRunner));
            this.testResultsXmlWriter = testResultsXmlWriter ?? throw new ArgumentNullException(nameof(testResultsXmlWriter));
            this.editorLogRangeExporter = editorLogRangeExporter ?? throw new ArgumentNullException(nameof(editorLogRangeExporter));
            this.readinessGate = readinessGate ?? throw new ArgumentNullException(nameof(readinessGate));
        }

        /// <summary> Executes one daemon <c>test.run</c> request and returns IPC response payload. </summary>
        /// <param name="request"> The decoded request payload. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by caller. </param>
        /// <returns> The response payload. </returns>
        /// <exception cref="ArgumentException"> Thrown when request payload violates contract. </exception>
        /// <exception cref="InvalidOperationException"> Thrown when run artifacts cannot be produced. </exception>
        public async Task<UnityTestRunServiceResult> Execute (
            IpcTestRunRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var requestContext = requestContextFactory.Create(request);
            var readinessResult = await readinessGate.EnsureExecutionReady(request.FailFast, cancellationToken).ConfigureAwait(false);
            if (!readinessResult.IsReady)
            {
                return UnityTestRunServiceResult.Failure(readinessResult.Error!);
            }

            var startOffset = GetFileLengthOrZero(requestContext.ConsoleLogPath);
            var testResult = await unityTestRunner.Run(requestContext, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var endOffset = GetFileLengthOrZero(requestContext.ConsoleLogPath);

            testResultsXmlWriter.Write(testResult, requestContext.ResultsXmlPath);
            await editorLogRangeExporter.ExportRange(
                requestContext.ConsoleLogPath,
                requestContext.EditorLogPath,
                startOffset,
                endOffset,
                cancellationToken);

            var exitCode = testResult.FailCount > 0 ? 2 : 0;
            return UnityTestRunServiceResult.Success(new IpcTestRunResponse(exitCode));
        }

        /// <summary> Reads file length when file exists; otherwise returns zero. </summary>
        /// <param name="path"> The file path. </param>
        /// <returns> The file length when existing; otherwise <c>0</c>. </returns>
        private static long GetFileLengthOrZero (string path)
        {
            return File.Exists(path) ? new FileInfo(path).Length : 0L;
        }
    }
}
