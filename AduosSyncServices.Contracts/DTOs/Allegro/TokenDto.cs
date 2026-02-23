namespace AduosSyncServices.Contracts.DTOs.Allegro
{
    public class TokenDto
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime ExpiryDateUtc { get; set; }
        public string TokenName { get; set; }

        public bool IsExpired() => DateTime.UtcNow >= ExpiryDateUtc.AddHours(-2);
    }
}