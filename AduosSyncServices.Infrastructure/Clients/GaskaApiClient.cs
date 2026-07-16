using AduosSyncServices.Contracts.Clients;
using AduosSyncServices.Contracts.DTOs.Allegro.GaskaApi;
using AduosSyncServices.Contracts.DTOs.GaskaApi;
using Microsoft.AspNetCore.WebUtilities;
using System.Net.Http.Json;
using System.Text.Json;

namespace AduosSyncServices.Infrastructure.Clients
{
    public class GaskaApiClient : IGaskaApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public GaskaApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };
        }

        public async Task<GaskaGetProductResponse> GetProduct(int id, string lng, CancellationToken cancellationToken = default)
        {
            return await GetDataAsync<GaskaGetProductResponse>($"/product", new Dictionary<string, string?>
            {
                { "id", id.ToString() },
                { "lng", lng }
            }, cancellationToken);
        }

        public async Task<GaskaGetProductsReponse> GetProducts(GaskaGetProductsRequest request, CancellationToken cancellationToken = default)
        {
            var query = new Dictionary<string, string?>
            {
                ["category"] = request.CategoryId?.ToString(),
                ["perPage"] = request.PageSize.ToString(),
                ["page"] = request.Page.ToString(),
                ["lng"] = request.Language
            };

            if (request.CategoryId is > 0)
            {
                query["categoryId"] = request.CategoryId.Value.ToString();
            }

            return await GetDataAsync<GaskaGetProductsReponse>("/products", query, cancellationToken);
        }

        public async Task<GaskaGetProductsChangedReponse> GetProductsChanged(DateTime dateFrom, CancellationToken cancellationToken = default)
        {
            return await GetDataAsync<GaskaGetProductsChangedReponse>("/productsChanged", new Dictionary<string, string?>
            {
                { "dateFrom", dateFrom.ToString("yyyy-MM-dd") }
            }, cancellationToken);
        }

        public async Task<GaskaCreateDeliveryAddressResponse> CreateDeliveryAddress(GaskaCreateDeliveryAddressRequest request, CancellationToken cancellationToken = default)
        {
            return await PostDataAsync<GaskaCreateDeliveryAddressResponse>("/addDeliveryAddress", request, cancellationToken);
        }

        public async Task<GaskaCreateOrderResponse> CreateOrder(GaskaCreateOrderRequest request, CancellationToken cancellationToken = default)
        {
            return await PostDataAsync<GaskaCreateOrderResponse>("/order", request, cancellationToken);
        }

        public async Task<GaskaGetDeliveryAddressesResponse> GetDeliveryAddresses(CancellationToken cancellationToken = default)
        {
            return await GetDataAsync<GaskaGetDeliveryAddressesResponse>("/deliveryAddresses", null, cancellationToken);
        }

        public async Task<GaskaGetOrderResponse> GetOrder(int orderId, string lng, CancellationToken cancellationToken = default)
        {
            return await GetDataAsync<GaskaGetOrderResponse>($"/order", new Dictionary<string, string?>
            {
                { "id", orderId.ToString() },
                { "lng", lng }
            }, cancellationToken);
        }

        private async Task<T> GetDataAsync<T>(string endpoint, IDictionary<string, string?>? queryParameters = null, CancellationToken cancellationToken = default)
        {
            if (queryParameters?.Any() == true)
            {
                endpoint = QueryHelpers.AddQueryString(endpoint, queryParameters!);
            }

            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("Failed to deserialize response");
        }

        private async Task<T> PostDataAsync<T>(string endpoint, object requestBody, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync(endpoint, requestBody, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("Failed to deserialize response");
        }
    }
}
