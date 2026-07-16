using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityLogsReadRequestValidatorTests
    {
        [Test]
        [Category("Size.Small")]
        public void TryValidate_WhenCurrentStreamIdIsEmpty_ThrowsArgumentException ()
        {
            var request = new IpcUnityLogsReadRequest(
                Tail: null,
                After: null,
                Since: null,
                Until: null,
                Level: null,
                Query: null,
                QueryTarget: null,
                Source: null,
                StackTrace: null,
                StackTraceMaxFrames: null,
                StackTraceMaxChars: null);
            var validator = new UnityLogsReadRequestValidator();

            var exception = Assert.Throws<ArgumentException>(() => validator.TryValidate(
                request,
                Guid.Empty,
                out _,
                out _));

            Assert.That(exception.ParamName, Is.EqualTo("currentStreamId"));
        }

        [Test]
        [Category("Size.Small")]
        public void TryValidate_WhenAfterCursorBelongsToAnotherStream_ReturnsFalse ()
        {
            var currentStreamId = Guid.Parse("65e40f1b-b68d-4275-9ccd-f0a3bfd5b586");
            var anotherStreamId = Guid.Parse("1b86d29d-2051-490f-8bdb-f769c3e061bd");
            var request = new IpcUnityLogsReadRequest(
                Tail: null,
                After: IpcLogCursor.Create(anotherStreamId, 1).Value,
                Since: null,
                Until: null,
                Level: null,
                Query: null,
                QueryTarget: null,
                Source: null,
                StackTrace: null,
                StackTraceMaxFrames: null,
                StackTraceMaxChars: null);
            var validator = new UnityLogsReadRequestValidator();

            var result = validator.TryValidate(request, currentStreamId, out var filter, out var errorMessage);

            Assert.That(result, Is.False);
            Assert.That(filter, Is.Null);
            Assert.That(errorMessage, Does.Contain("does not match current unity log stream"));
        }
    }
}
