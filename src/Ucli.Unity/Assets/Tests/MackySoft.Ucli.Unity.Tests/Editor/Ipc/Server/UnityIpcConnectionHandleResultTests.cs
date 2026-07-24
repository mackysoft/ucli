using System;
using System.Text.Json;
using MackySoft.Text.Vocabularies;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityIpcConnectionHandleResultTests
    {
        [Test]
        [Category("Size.Small")]
        public void NoTerminalResponse_RepresentsExplicitEmptyOutcome ()
        {
            var result = UnityIpcConnectionHandleResult.NoTerminalResponse;

            Assert.That(result.HasTerminalResponse, Is.False);
            Assert.That(result.Request, Is.Null);
            Assert.That(result.Method, Is.Null);
            Assert.That(result.Response, Is.Null);
            Assert.That(result.IsShutdownAdmissionCommitted, Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void Constructor_WhenRequestIsNull_ThrowsArgumentNullException ()
        {
            var response = CreateTerminalResponse(Guid.NewGuid(), IpcResponseStatus.Error);

            var exception = Assert.Throws<ArgumentNullException>(() => new UnityIpcConnectionHandleResult(
                request: null,
                response: response,
                isShutdownAdmissionCommitted: false));

            Assert.That(exception.ParamName, Is.EqualTo("request"));
        }

        [Test]
        [Category("Size.Small")]
        public void Constructor_WhenResponseIsNull_ThrowsArgumentNullException ()
        {
            var request = CreateShutdownRequest(Guid.NewGuid());

            var exception = Assert.Throws<ArgumentNullException>(() => new UnityIpcConnectionHandleResult(
                request,
                response: null,
                isShutdownAdmissionCommitted: false));

            Assert.That(exception.ParamName, Is.EqualTo("response"));
        }

        [Test]
        [Category("Size.Small")]
        public void Constructor_WhenResponseIsNotCorrelated_ThrowsArgumentException ()
        {
            var request = CreateShutdownRequest(Guid.NewGuid());
            var response = CreateTerminalResponse(Guid.NewGuid(), IpcResponseStatus.Error);

            var exception = Assert.Throws<ArgumentException>(() => new UnityIpcConnectionHandleResult(
                request,
                response,
                isShutdownAdmissionCommitted: false));

            Assert.That(exception.ParamName, Is.EqualTo("response"));
        }

        [Test]
        [Category("Size.Small")]
        public void Constructor_WhenRejectedShutdownIsCommitted_ThrowsArgumentException ()
        {
            var request = CreateShutdownRequest(Guid.NewGuid());
            var response = CreateTerminalResponse(request.RequestId, IpcResponseStatus.Error);

            var exception = Assert.Throws<ArgumentException>(() => new UnityIpcConnectionHandleResult(
                request,
                response,
                isShutdownAdmissionCommitted: true));

            Assert.That(exception.ParamName, Is.EqualTo("isShutdownAdmissionCommitted"));
        }

        [Test]
        [Category("Size.Small")]
        public void ValidatedRequest_WhenMethodIsUndefined_ThrowsArgumentOutOfRangeException ()
        {
            var requestId = Guid.NewGuid();
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new ValidatedUnityIpcRequest(
                requestId,
                (UnityIpcMethod)int.MaxValue,
                JsonSerializer.SerializeToElement(new IpcShutdownRequest("tests")),
                IpcResponseMode.Single,
                DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30),
                30_000));

            Assert.That(exception.ParamName, Is.EqualTo("method"));
        }

        private static ValidatedUnityIpcRequest CreateShutdownRequest (Guid requestId)
        {
            var envelope = new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                sessionToken: "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
                method: TextVocabulary.GetText(UnityIpcMethod.Shutdown),
                payload: JsonSerializer.SerializeToElement(new IpcShutdownRequest("tests")),
                responseMode: "single",
                requestDeadlineUtc: DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30),
                requestDeadlineRemainingMilliseconds: 30_000);
            return ValidatedUnityIpcRequestTestFactory.Create(envelope);
        }

        private static IpcResponse CreateTerminalResponse (
            Guid requestId,
            IpcResponseStatus status)
        {
            var errors = status == IpcResponseStatus.Error
                ? new[] { new IpcError(UcliCoreErrorCodes.InternalError, "tests", null) }
                : Array.Empty<IpcError>();
            return new IpcResponse(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                status: status,
                payload: JsonSerializer.SerializeToElement(new IpcShutdownResponse(false, "tests")),
                errors: errors);
        }
    }
}
