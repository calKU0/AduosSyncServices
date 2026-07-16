
using AduosSyncServices.Contracts.DTOs.Allegro.GaskaApi;
using AduosSyncServices.Contracts.DTOs.GaskaApi;

namespace AduosSyncServices.Contracts.Clients
{
    public interface IGaskaApiClient
    {
        public Task<GaskaGetProductsReponse> GetProducts(GaskaGetProductsRequest request, CancellationToken cancellationToken = default);
        public Task<GaskaGetProductResponse> GetProduct(int id, string lng, CancellationToken cancellationToken = default);
        public Task<GaskaGetProductsChangedReponse> GetProductsChanged(DateTime dateFrom, CancellationToken cancellationToken = default);
        public Task<GaskaCreateDeliveryAddressResponse> CreateDeliveryAddress(GaskaCreateDeliveryAddressRequest request, CancellationToken cancellationToken = default);
        public Task<GaskaGetDeliveryAddressesResponse> GetDeliveryAddresses(CancellationToken cancellationToken = default);
        public Task<GaskaCreateOrderResponse> CreateOrder(GaskaCreateOrderRequest request, CancellationToken cancellationToken = default);
        public Task<GaskaGetOrderResponse> GetOrder(int orderId, string lng, CancellationToken cancellationToken = default);
    }
}
