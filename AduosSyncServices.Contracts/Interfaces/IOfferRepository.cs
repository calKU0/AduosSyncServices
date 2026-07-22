using AduosSyncServices.Contracts.DTOs.Allegro;
using AduosSyncServices.Contracts.Models;

namespace AduosSyncServices.Contracts.Interfaces
{
    public interface IOfferRepository
    {
        Task UpsertOffers(List<Offer> offers, CancellationToken ct);

        Task<List<AllegroOffer>> GetAllOffers(CancellationToken ct);

        // Returns offerId -> DeliveryName for only the given offer ids (the ones referenced by the
        // orders being synced), so we don't pull the whole offer catalog just to resolve a handful
        // of shipping methods.
        Task<Dictionary<string, string>> GetOfferDeliveryNamesByIds(IReadOnlyCollection<string> offerIds, CancellationToken ct);

        Task<List<AllegroOffer>> GetOffersToUpdate(CancellationToken ct);
        Task<List<AllegroOffer>> GetOffersWithoutDetails(CancellationToken ct);
        Task UpsertOfferDetails(List<AllegroOfferDetails.Root> offers, CancellationToken ct);

        Task DeleteOffer(string offerId, CancellationToken ct);
    }
}