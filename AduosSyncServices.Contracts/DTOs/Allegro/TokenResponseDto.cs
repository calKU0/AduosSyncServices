using System.Text.Json.Serialization;

namespace AduosSyncServices.Contracts.DTOs.Allegro
{
    public class TokenResponseDto
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; }

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("scope")]
        public string Scope { get; set; }

        [JsonPropertyName("allegro_api")]
        public bool AllegroApi { get; set; }

        [JsonPropertyName("jti")]
        public string Jti { get; set; }
    }
}