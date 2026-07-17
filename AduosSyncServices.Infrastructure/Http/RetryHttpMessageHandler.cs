using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

namespace AduosSyncServices.Infrastructure.Http
{
    /// <summary>
    /// Shared retry policy for every outgoing API call: transient failures (network errors,
    /// timeouts, 5xx/408) are retried up to <see cref="MaxRetries"/> times with a short linear
    /// backoff; 429 Too Many Requests gets its own exponential backoff starting at 30s.
    /// Attach via <c>AddHttpMessageHandler&lt;RetryHttpMessageHandler&gt;()</c> for DI-managed
    /// clients, or wrap an <see cref="HttpMessageHandler"/> directly for manually-constructed ones.
    /// </summary>
    public sealed class RetryHttpMessageHandler : DelegatingHandler
    {
        private const int MaxRetries = 3;
        private static readonly TimeSpan TooManyRequestsBaseDelay = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan GeneralErrorBaseDelay = TimeSpan.FromSeconds(1);

        private readonly ILogger<RetryHttpMessageHandler> _logger;

        public RetryHttpMessageHandler(ILogger<RetryHttpMessageHandler>? logger = null)
        {
            _logger = logger ?? NullLogger<RetryHttpMessageHandler>.Instance;
        }

        public RetryHttpMessageHandler(HttpMessageHandler innerHandler, ILogger<RetryHttpMessageHandler>? logger = null)
            : base(innerHandler)
        {
            _logger = logger ?? NullLogger<RetryHttpMessageHandler>.Instance;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            byte[]? bodyBytes = request.Content != null
                ? await request.Content.ReadAsByteArrayAsync(cancellationToken)
                : null;

            HttpResponseMessage? response = null;

            for (var attempt = 0; ; attempt++)
            {
                var attemptRequest = CloneRequest(request, bodyBytes);

                try
                {
                    response?.Dispose();
                    response = await base.SendAsync(attemptRequest, cancellationToken);
                }
                catch (Exception ex) when (attempt < MaxRetries && !cancellationToken.IsCancellationRequested && IsTransient(ex))
                {
                    var delay = GeneralErrorBaseDelay * (attempt + 1);
                    _logger.LogWarning(ex, "Request to {Uri} failed ({ExceptionType}). Retrying in {DelaySeconds}s ({Attempt}/{MaxRetries})...",
                        request.RequestUri, ex.GetType().Name, delay.TotalSeconds, attempt + 1, MaxRetries);
                    await Task.Delay(delay, cancellationToken);
                    continue;
                }

                if (response.IsSuccessStatusCode)
                    return response;

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    if (attempt >= MaxRetries)
                        return response;

                    var delay = TimeSpan.FromSeconds(TooManyRequestsBaseDelay.TotalSeconds * Math.Pow(2, attempt));
                    _logger.LogWarning("Request to {Uri} was rate-limited (429). Waiting {DelaySeconds}s before retry {Attempt}/{MaxRetries}...",
                        request.RequestUri, delay.TotalSeconds, attempt + 1, MaxRetries);
                    await Task.Delay(delay, cancellationToken);
                    continue;
                }

                if (attempt >= MaxRetries || !IsRetryableStatusCode(response.StatusCode))
                    return response;

                var generalDelay = GeneralErrorBaseDelay * (attempt + 1);
                _logger.LogWarning("Request to {Uri} failed with {StatusCode}. Retrying in {DelaySeconds}s ({Attempt}/{MaxRetries})...",
                    request.RequestUri, (int)response.StatusCode, generalDelay.TotalSeconds, attempt + 1, MaxRetries);
                await Task.Delay(generalDelay, cancellationToken);
            }
        }

        private static bool IsTransient(Exception ex) =>
            ex is HttpRequestException or TaskCanceledException or IOException;

        private static bool IsRetryableStatusCode(HttpStatusCode statusCode) =>
            statusCode is HttpStatusCode.RequestTimeout
                or HttpStatusCode.InternalServerError
                or HttpStatusCode.BadGateway
                or HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.GatewayTimeout;

        private static HttpRequestMessage CloneRequest(HttpRequestMessage original, byte[]? bodyBytes)
        {
            var clone = new HttpRequestMessage(original.Method, original.RequestUri)
            {
                Version = original.Version
            };

            foreach (var header in original.Headers)
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

            if (bodyBytes != null)
            {
                clone.Content = new ByteArrayContent(bodyBytes);
                if (original.Content != null)
                {
                    foreach (var header in original.Content.Headers)
                        clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            return clone;
        }
    }
}
