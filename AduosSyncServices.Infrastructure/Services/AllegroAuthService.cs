using AduosSyncServices.Contracts.DTOs.Allegro;
using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Contracts.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AduosSyncServices.Infrastructure.Services
{
    public class AllegroAuthService
    {
        private ILogger<AllegroAuthService> _logger;
        private readonly AllegroApiCredentials _settings;
        private readonly ITokenRepository _tokenRepo;
        private readonly HttpClient _http;

        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public AllegroAuthService(ILogger<AllegroAuthService> logger, IOptions<AllegroApiCredentials> settings, ITokenRepository tokenRepo, HttpClient httpClient)
        {
            _logger = logger;
            _settings = settings.Value;
            _tokenRepo = tokenRepo;
            _http = httpClient;
        }

        public async Task<string> GetAccessTokenAsync(CancellationToken ct = default(CancellationToken))
        {
            var tokens = await _tokenRepo.GetTokensAsync();

            if (tokens != null && !tokens.IsExpired())
                return tokens.AccessToken;

            if (tokens != null && !string.IsNullOrWhiteSpace(tokens.RefreshToken))
            {
                try
                {
                    tokens = await RefreshWithRefreshTokenAsync(tokens.RefreshToken, ct);
                    await _tokenRepo.SaveTokensAsync(tokens);
                    return tokens.AccessToken;
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, "Refresh token failed");
                    //await _tokenRepo.ClearAsync();
                }
            }

            var device = await StartDeviceFlowAsync(ct);
            _logger.LogInformation($"Allegro device flow started. Give this link to the user: {device.VerificationUriComplete} (code: {FormatUserCode(device.UserCode)})");

            tokens = await PollForDeviceTokenAsync(device, ct);
            await _tokenRepo.SaveTokensAsync(tokens);
            return tokens.AccessToken;
        }

        private async Task<TokenDto> RefreshWithRefreshTokenAsync(string refreshToken, CancellationToken ct)
        {
            using (var req = new HttpRequestMessage(HttpMethod.Post, "/auth/oauth/token"))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Basic", BuildBasic(_settings.ClientId, _settings.ClientSecret));
                req.Content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string,string>("grant_type","refresh_token"),
                    new KeyValuePair<string,string>("refresh_token", refreshToken)
                });

                using (var resp = await _http.SendAsync(req, ct))
                {
                    if (!resp.IsSuccessStatusCode)
                    {
                        var body = await resp.Content.ReadAsStringAsync();
                        throw new HttpRequestException($"Refresh failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");
                    }

                    var json = await resp.Content.ReadAsStringAsync();
                    var tr = JsonSerializer.Deserialize<TokenResponseDto>(json, _jsonOptions);
                    return new TokenDto
                    {
                        AccessToken = tr.AccessToken,
                        RefreshToken = tr.RefreshToken,
                        ExpiryDateUtc = DateTime.UtcNow.AddSeconds(tr.ExpiresIn)
                    };
                }
            }
        }

        private async Task<DeviceCodeResponseDto> StartDeviceFlowAsync(CancellationToken ct)
        {
            var uri = $"/auth/oauth/device?client_id={Uri.EscapeDataString(_settings.ClientId)}";

            using (var req = new HttpRequestMessage(HttpMethod.Post, uri))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Basic", BuildBasic(_settings.ClientId, _settings.ClientSecret));
                req.Content = new StringContent("", Encoding.UTF8, "application/x-www-form-urlencoded");

                using (var resp = await _http.SendAsync(req, ct))
                {
                    resp.EnsureSuccessStatusCode();

                    var json = await resp.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<DeviceCodeResponseDto>(json, _jsonOptions);
                }
            }
        }

        private async Task<TokenDto> PollForDeviceTokenAsync(DeviceCodeResponseDto device, CancellationToken ct)
        {
            var startedUtc = DateTime.UtcNow;
            var expiresAtUtc = startedUtc.AddSeconds(device.ExpiresIn);
            var intervalSec = Math.Max(device.Interval, 5);

            while (DateTime.UtcNow < expiresAtUtc)
            {
                ct.ThrowIfCancellationRequested();

                using (var req = new HttpRequestMessage(HttpMethod.Post, "/auth/oauth/token"))
                {
                    req.Headers.Authorization = new AuthenticationHeaderValue("Basic", BuildBasic(_settings.ClientId, _settings.ClientSecret));
                    req.Content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string,string>("grant_type","urn:ietf:params:oauth:grant-type:device_code"),
                        new KeyValuePair<string,string>("device_code", device.DeviceCode)
                    });

                    using (var resp = await _http.SendAsync(req, ct))
                    {
                        var body = await resp.Content.ReadAsStringAsync();

                        if (resp.IsSuccessStatusCode)
                        {
                            var tr = JsonSerializer.Deserialize<TokenResponseDto>(body, _jsonOptions);
                            return new TokenDto
                            {
                                AccessToken = tr.AccessToken,
                                RefreshToken = tr.RefreshToken,
                                ExpiryDateUtc = DateTime.UtcNow.AddSeconds(tr.ExpiresIn)
                            };
                        }

                        if (resp.StatusCode == HttpStatusCode.BadRequest)
                        {
                            string error = TryGetError(body);

                            switch (error)
                            {
                                case "authorization_pending":
                                    await Task.Delay(TimeSpan.FromSeconds(intervalSec), ct);
                                    continue;

                                case "slow_down":
                                    intervalSec += 5;
                                    await Task.Delay(TimeSpan.FromSeconds(intervalSec), ct);
                                    continue;

                                case "access_denied":
                                    throw new InvalidOperationException("User denied access in Allegro device flow.");

                                case "expired_token":
                                case "expired_device_code":
                                    throw new TimeoutException("Device code expired before authorization was completed.");

                                default:
                                    throw new HttpRequestException($"Device flow polling failed: {body}");
                            }
                        }

                        throw new HttpRequestException($"Device flow polling HTTP {(int)resp.StatusCode}: {resp.ReasonPhrase}. Body: {body}");
                    }
                }
            }

            throw new TimeoutException("Device flow timed out.");
        }

        private static string BuildBasic(string clientId, string clientSecret)
        {
            var bytes = Encoding.UTF8.GetBytes(clientId + ":" + clientSecret);
            return Convert.ToBase64String(bytes);
        }

        private static string TryGetError(string json)
        {
            try
            {
                using (var doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("error", out var e))
                        return e.GetString();
                }
            }
            catch
            {
                // ignore parse errors
            }
            return null;
        }

        private static string FormatUserCode(string userCode)
        {
            if (string.IsNullOrWhiteSpace(userCode)) return userCode;

            userCode = userCode.Replace(" ", "");
            var sb = new StringBuilder();
            for (int i = 0; i < userCode.Length; i++)
            {
                if (i > 0 && i % 3 == 0) sb.Append(' ');
                sb.Append(userCode[i]);
            }
            return sb.ToString();
        }
    }
}