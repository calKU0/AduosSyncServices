using System.Net;
using AduosSyncServices.Infrastructure.Http;
using Xunit;

namespace AduosSyncServices.Infrastructure.Tests.Http
{
    public class RetryHttpMessageHandlerTests
    {
        // Scripted inner handler: each queued step either returns a status code or throws.
        private sealed class ScriptedHandler : HttpMessageHandler
        {
            private readonly Queue<Func<HttpResponseMessage>> _steps;
            public int Calls { get; private set; }
            public List<string> ReceivedBodies { get; } = new();

            public ScriptedHandler(params Func<HttpResponseMessage>[] steps) => _steps = new(steps);

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls++;
                if (request.Content != null)
                    ReceivedBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
                return _steps.Dequeue()();
            }
        }

        private static HttpClient ClientOver(ScriptedHandler inner) =>
            new(new RetryHttpMessageHandler(inner));

        private static Func<HttpResponseMessage> Status(HttpStatusCode code) => () => new HttpResponseMessage(code);
        private static Func<HttpResponseMessage> Throws() => () => throw new HttpRequestException("transient");

        [Fact]
        public async Task Success_FirstTry_NoRetry()
        {
            var inner = new ScriptedHandler(Status(HttpStatusCode.OK));
            var response = await ClientOver(inner).GetAsync("https://x.test/");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(1, inner.Calls);
        }

        [Fact]
        public async Task NonRetryableStatus_ReturnedImmediately()
        {
            var inner = new ScriptedHandler(Status(HttpStatusCode.BadRequest));
            var response = await ClientOver(inner).GetAsync("https://x.test/");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(1, inner.Calls);
        }

        [Fact]
        public async Task RetryableServerError_ThenSuccess_Retries()
        {
            var inner = new ScriptedHandler(Status(HttpStatusCode.InternalServerError), Status(HttpStatusCode.OK));
            var response = await ClientOver(inner).GetAsync("https://x.test/");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(2, inner.Calls);
        }

        [Fact]
        public async Task TransientException_ThenSuccess_Retries()
        {
            var inner = new ScriptedHandler(Throws(), Status(HttpStatusCode.OK));
            var response = await ClientOver(inner).GetAsync("https://x.test/");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(2, inner.Calls);
        }

        [Fact]
        public async Task RequestBody_IsReplayedOnEveryRetry()
        {
            var inner = new ScriptedHandler(Status(HttpStatusCode.ServiceUnavailable), Status(HttpStatusCode.OK));
            var client = ClientOver(inner);

            await client.PostAsync("https://x.test/", new StringContent("payload-123"));

            Assert.Equal(2, inner.Calls);
            Assert.All(inner.ReceivedBodies, b => Assert.Equal("payload-123", b));
            Assert.Equal(2, inner.ReceivedBodies.Count);
        }
    }
}
