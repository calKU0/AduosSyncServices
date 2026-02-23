namespace AduosSyncServices.Contracts.Interfaces
{
    public interface IAllegroOfferService
    {
        Task SyncAllegroOffers(CancellationToken ct = default);

        Task SyncAllegroOffersDetails(CancellationToken ct = default);

        Task CreateOffers(CancellationToken ct = default);

        Task UpdateOffers(CancellationToken ct = default);
    }
}