using AduosSyncServices.Contracts.DTOs.Allegro;

namespace AduosSyncServices.Contracts.Clients
{
    public interface IAllegroApiClient
    {
        Task<AllegroShippingRatesResponse> GetShippingRates(CancellationToken ct = default);
        Task<OffersResponse> GetOffers(int limit, int offset, CancellationToken ct = default);
        Task<AllegroOfferDetails.Root> GetOfferDetails(string offerId, CancellationToken ct = default);
        Task<HttpResponseMessage> CreateOffer(object offer, CancellationToken ct = default);
        Task<HttpResponseMessage> UpdateOffer(string offerId, object offerDto, CancellationToken ct = default);
        Task<AllegroImageResponse> UploadImage(byte[] imageBytes, string contentType, CancellationToken ct = default);
        Task<MatchingCategoriesResponse> GetMatchingCategories(string productName, CancellationToken ct = default);
        Task<CategoryParametersResponse> GetCategoryParameters(string categoryId, CancellationToken ct = default);
        Task<AllegroGetOrdersResponse> GetOrders(AllegroGetOrdersRequest request, CancellationToken ct = default);
        Task<AllegroMinimalOfferDetails> GetMinimalOfferInfo(string offerId, CancellationToken ct = default);
        Task<HttpResponseMessage> UpdateOrderStatus(string orderId, AllegroSetOrderStatusRequest request, CancellationToken ct = default);
        Task<HttpResponseMessage> AddOrderTrackingNumber(string orderId, AllegroAddTrackingNumberRequest request, CancellationToken ct = default);
    }
}
