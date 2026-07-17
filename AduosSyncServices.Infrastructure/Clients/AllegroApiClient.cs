using AduosSyncServices.Contracts.Clients;
using AduosSyncServices.Contracts.DTOs.Allegro;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AduosSyncServices.Infrastructure.Clients
{
    public class AllegroApiClient : IAllegroApiClient
    {
        private readonly AllegroAuthClient _auth;
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _options;

        public AllegroApiClient(AllegroAuthClient authService, HttpClient httpClient)
        {
            _auth = authService;
            _http = httpClient;

            _options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };
        }

        public async Task<AllegroShippingRatesResponse> GetShippingRates(CancellationToken ct = default)
        {
            return await GetAsync<AllegroShippingRatesResponse>("/sale/shipping-rates", ct);
        }

        public async Task<OffersResponse> GetOffers(int limit, int offset, CancellationToken ct = default)
        {
            return await GetAsync<OffersResponse>($"/sale/offers?limit={limit}&offset={offset}", ct);
        }

        public async Task<AllegroOfferDetails.Root> GetOfferDetails(string offerId, CancellationToken ct = default)
        {
            return await GetAsync<AllegroOfferDetails.Root>($"/sale/product-offers/{offerId}", ct);
        }

        public async Task<HttpResponseMessage> CreateOffer(object offer, CancellationToken ct = default)
        {
            return await SendWithResponseAsync("/sale/product-offers", HttpMethod.Post, offer, ct);
        }

        public async Task<HttpResponseMessage> UpdateOffer(string offerId, object offerDto, CancellationToken ct = default)
        {
            return await SendWithResponseAsync($"/sale/product-offers/{offerId}", HttpMethod.Patch, offerDto, ct);
        }

        public async Task<AllegroImageResponse> UploadImage(byte[] imageBytes, string contentType, CancellationToken ct = default)
        {
            return await PostAsync<AllegroImageResponse>("/sale/images", imageBytes, ct, contentType);
        }

        public async Task<MatchingCategoriesResponse> GetMatchingCategories(string productName, CancellationToken ct = default)
        {
            return await GetAsync<MatchingCategoriesResponse>($"/sale/matching-categories?name={productName}", ct);
        }

        public async Task<CategoryParametersResponse> GetCategoryParameters(string categoryId, CancellationToken ct = default)
        {
            return await GetAsync<CategoryParametersResponse>($"/sale/categories/{categoryId}/parameters", ct);
        }
        public async Task<AllegroGetOrdersResponse> GetOrders(AllegroGetOrdersRequest request, CancellationToken ct = default)
        {
            return await GetAsync<AllegroGetOrdersResponse>($"order/checkout-forms?limit={request.Limit}&offset={request.Offset}&lineItems.boughtAt.gte={request.DateFrom}", ct);
        }

        public async Task<AllegroMinimalOfferDetails> GetMinimalOfferInfo(string offerId, CancellationToken ct = default)
        {
            return await GetAsync<AllegroMinimalOfferDetails>($"/sale/product-offers/{offerId}", ct);
        }

        public async Task<HttpResponseMessage> UpdateOrderStatus(string orderId, AllegroSetOrderStatusRequest request, CancellationToken ct = default)
        {
            return await SendWithResponseAsync($"/order/checkout-forms/{orderId}/fulfillment", HttpMethod.Put, request, ct);
        }

        public async Task<HttpResponseMessage> AddOrderTrackingNumber(string orderId, AllegroAddTrackingNumberRequest request, CancellationToken ct = default)
        {
            return await SendWithResponseAsync($"/order/checkout-forms/{orderId}/shipments", HttpMethod.Post, request, ct);
        }

        private async Task<T> GetAsync<T>(string url, CancellationToken ct)
        {
            var request = await CreateRequest(HttpMethod.Get, url, ct);
            var response = await _http.SendAsync(request, ct);
            return await DeserializeAsync<T>(response);
        }

        private async Task<T> PostAsync<T>(string url, object body, CancellationToken ct, string contentType = "application/vnd.allegro.public.v1+json")
        {
            var request = await CreateRequest(HttpMethod.Post, url, ct);

            if (body != null)
            {
                if (body is byte[] bytes)
                {
                    request.Content = new ByteArrayContent(bytes);
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                }
                else
                {
                    var json = JsonSerializer.Serialize(body, _options);
                    request.Content = new StringContent(json, Encoding.UTF8, contentType);
                }
            }

            var response = await _http.SendAsync(request, ct);
            return await DeserializeAsync<T>(response);
        }

        private async Task<HttpResponseMessage> SendWithResponseAsync(string url, HttpMethod method, object body = null, CancellationToken ct = default)
        {
            var request = await CreateRequest(method, url, ct);

            if (body != null)
            {
                var json = JsonSerializer.Serialize(body, _options);
                request.Content = new StringContent(json, Encoding.UTF8, "application/vnd.allegro.public.v1+json");
            }

            return await _http.SendAsync(request, ct);
        }

        private async Task<HttpRequestMessage> CreateRequest(HttpMethod method, string url, CancellationToken ct)
        {
            var token = await _auth.GetAccessTokenAsync(ct);
            var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.allegro.public.v1+json"));
            return request;
        }

        private async Task<T> DeserializeAsync<T>(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
                return default;

            var body = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(body, _options);
        }
    }
}