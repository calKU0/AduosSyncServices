using AduosSyncServices.Contracts.DTOs.Allegro;

namespace AduosSyncServices.Contracts.Interfaces
{
    public interface ITokenRepository
    {
        Task<TokenDto?> GetTokensAsync();

        Task SaveTokensAsync(TokenDto tokens);

        Task ClearAsync();
    }
}