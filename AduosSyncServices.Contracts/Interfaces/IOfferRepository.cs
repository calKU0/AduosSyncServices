using AduosSyncServices.Contracts.DTOs.Allegro;
using AduosSyncServices.Contracts.Models;

namespace AduosSyncServices.Contracts.Interfaces
{
    public interface IOfferRepository
    {
        Task UpsertOffers(List<Offer> offers, CancellationToken ct);

        Task<List<AllegroOffer>> GetAllOffers(CancellationToken ct);

        Task<List<AllegroOffer>> GetOffersToUpdate(CancellationToken ct);
        Task<List<AllegroOffer>> GetOffersWithoutDetails(CancellationToken ct);
        Task UpsertOfferDetails(List<AllegroOfferDetails.Root> offers, CancellationToken ct);

        Task DeleteOffer(int productId, CancellationToken ct);
    }
}