using AduosSyncServices.Contracts.Clients;
using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.Contracts.DTOs.Allegro;
using AduosSyncServices.Contracts.DTOs.GaskaApi;
using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Infrastructure.Services;
using AduosSyncServices.Infrastructure.Tests.TestSupport;
using NSubstitute;
using Xunit;

namespace AduosSyncServices.Infrastructure.Tests.Services
{
    // Covers partial placement: when some orders are short on stock, only the feasible ones are placed
    // at Gąska, and the short ones are left completely untouched (not sent, not marked, no Allegro
    // status change).
    public class GaskaOrderPlacementServicePartialTests
    {
        private readonly IOrderRepository _orderRepo = Substitute.For<IOrderRepository>();
        private readonly IProductRepository _productRepo = Substitute.For<IProductRepository>();
        private readonly IGaskaApiClient _gaska = Substitute.For<IGaskaApiClient>();
        private readonly IAllegroApiClient _allegro = Substitute.For<IAllegroApiClient>();

        public GaskaOrderPlacementServicePartialTests()
        {
            // Happy-path defaults for the write endpoints; individual tests override stock as needed.
            _gaska.CreateDeliveryAddress(Arg.Any<GaskaCreateDeliveryAddressRequest>(), Arg.Any<CancellationToken>())
                .Returns(ci => new GaskaCreateDeliveryAddressResponse { Result = 0, AddressId = 777 });
            _gaska.CreateOrder(Arg.Any<GaskaCreateOrderRequest>(), Arg.Any<CancellationToken>())
                .Returns(ci => new GaskaCreateOrderResponse { Result = 0, NewOrders = new List<int> { 555 } });
            _gaska.GetOrder(Arg.Any<int>(), "pl", Arg.Any<CancellationToken>())
                .Returns(ci => new GaskaGetOrderResponse { Result = 0, Order = new GaskaGetOrderResponse.OrderDto { OrderNumber = "G-555", Items = new() } });
            _gaska.GetDeliveryAddresses(Arg.Any<CancellationToken>())
                .Returns(new GaskaGetDeliveryAddressesResponse
                {
                    Result = 0,
                    AdressDetails = new List<DeliveryAdressDetails> { new() { Id = 1, Default = true } }
                });
            _allegro.UpdateOrderStatus(Arg.Any<string>(), Arg.Any<AllegroSetOrderStatusRequest>(), Arg.Any<CancellationToken>())
                .Returns(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }

        private GaskaOrderPlacementService CreateService() =>
            new(_orderRepo, _productRepo, _gaska, _allegro, productIntervalSeconds: 0);

        private void SetupProducts(params Product[] products) =>
            _productRepo.GetProductsByIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
                .Returns(products.ToList());

        private void SetupStock(int integrationId, float inStock) =>
            _gaska.GetProduct(integrationId, "pl", Arg.Any<CancellationToken>())
                .Returns(new GaskaGetProductResponse { Result = 0, Product = new ApiProduct { InStock = inStock } });

        [Fact]
        public async Task PlaceCustomerOrders_ShortOrderIsSkipped_FeasibleOrderIsPlaced()
        {
            SetupProducts(
                OrderTestData.Product(id: 1, integrationId: 101, code: "P1"),
                OrderTestData.Product(id: 2, integrationId: 102, code: "P2"));
            SetupStock(101, inStock: 5);   // order 1 needs 10 -> short
            SetupStock(102, inStock: 10);  // order 2 needs 3  -> ok

            var orders = new[]
            {
                OrderTestData.Order(1, "A-1", new[] { (productId: 1, quantity: 10) }),
                OrderTestData.Order(2, "A-2", new[] { (productId: 2, quantity: 3) })
            };

            var result = await CreateService().PlaceCustomerOrdersAsync(orders, GaskaDeliveryCourier.Dpd, new Dictionary<int, decimal>());

            Assert.True(result.Results.Single(r => r.AllegroOrderId == 2).IsSuccessful);
            Assert.False(result.Results.Single(r => r.AllegroOrderId == 1).IsSuccessful);

            // Feasible order marked/placed; short order never touched.
            await _orderRepo.Received(1).MarkAsOrderedInExternalCompany(2, Arg.Any<int>(), Arg.Any<bool>());
            await _orderRepo.DidNotReceive().MarkAsOrderedInExternalCompany(1, Arg.Any<int>(), Arg.Any<bool>());
            await _allegro.DidNotReceive().UpdateOrderStatus("A-1", Arg.Any<AllegroSetOrderStatusRequest>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task PlaceCustomerOrders_AlreadySentOrder_IsRejectedNotResent()
        {
            SetupProducts(OrderTestData.Product(id: 1, integrationId: 101, code: "P1"));
            SetupStock(101, inStock: 100);
            var orders = new[] { OrderTestData.Order(1, "A-1", new[] { (productId: 1, quantity: 1) }, sentToExternalCompany: true) };

            var result = await CreateService().PlaceCustomerOrdersAsync(orders, GaskaDeliveryCourier.Dpd, new Dictionary<int, decimal>());

            Assert.False(result.Results.Single().IsSuccessful);
            await _gaska.DidNotReceive().CreateOrder(Arg.Any<GaskaCreateOrderRequest>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task PlaceCustomerOrders_CodCourierWithNonCodOrder_RejectsWholeBatchBeforeApiCalls()
        {
            SetupProducts(OrderTestData.Product(id: 1, integrationId: 101, code: "P1"));
            SetupStock(101, inStock: 100);
            // Fedex Dropshipping Pobranie requires every order to be cash-on-delivery.
            var orders = new[] { OrderTestData.Order(1, "A-1", new[] { (productId: 1, quantity: 1) }, paymentType: AllegroPaymentType.ONLINE) };

            var result = await CreateService().PlaceCustomerOrdersAsync(
                orders, GaskaDeliveryCourier.FedexDropshippingPobranie, new Dictionary<int, decimal>());

            Assert.False(result.Results.Single().IsSuccessful);
            await _gaska.DidNotReceive().CreateDeliveryAddress(Arg.Any<GaskaCreateDeliveryAddressRequest>(), Arg.Any<CancellationToken>());
            await _gaska.DidNotReceive().CreateOrder(Arg.Any<GaskaCreateOrderRequest>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task PlaceHeadquartersOrder_ShortOrderSkipped_RestPlacedWithWarning()
        {
            SetupProducts(
                OrderTestData.Product(id: 1, integrationId: 101, code: "P1"),
                OrderTestData.Product(id: 2, integrationId: 102, code: "P2"));
            SetupStock(101, inStock: 5);   // order 1 needs 10 -> short
            SetupStock(102, inStock: 10);  // order 2 needs 3  -> ok

            var orders = new[]
            {
                OrderTestData.Order(1, "A-1", new[] { (productId: 1, quantity: 10) }),
                OrderTestData.Order(2, "A-2", new[] { (productId: 2, quantity: 3) })
            };

            var result = await CreateService().PlaceHeadquartersOrderAsync(orders, GaskaDeliveryCourier.Dpd);

            Assert.True(result.IsSuccessful);
            Assert.Equal(new[] { 2 }, result.AllegroOrderIds);
            Assert.Contains(result.Warnings, w => w.Contains("A-1"));
            await _orderRepo.Received(1).MarkAsOrderedInExternalCompany(2, Arg.Any<int>(), Arg.Any<bool>());
            await _orderRepo.DidNotReceive().MarkAsOrderedInExternalCompany(1, Arg.Any<int>(), Arg.Any<bool>());
        }

        [Fact]
        public async Task PlaceHeadquartersOrder_AllOrdersShort_FailsAndPlacesNothing()
        {
            SetupProducts(OrderTestData.Product(id: 1, integrationId: 101, code: "P1"));
            SetupStock(101, inStock: 0);
            var orders = new[] { OrderTestData.Order(1, "A-1", new[] { (productId: 1, quantity: 5) }) };

            var result = await CreateService().PlaceHeadquartersOrderAsync(orders, GaskaDeliveryCourier.Dpd);

            Assert.False(result.IsSuccessful);
            await _gaska.DidNotReceive().CreateOrder(Arg.Any<GaskaCreateOrderRequest>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task PlaceHeadquartersOrder_CodCourier_IsRejected()
        {
            SetupProducts(OrderTestData.Product(id: 1, integrationId: 101, code: "P1"));
            SetupStock(101, inStock: 100);
            var orders = new[] { OrderTestData.Order(1, "A-1", new[] { (productId: 1, quantity: 1) }) };

            var result = await CreateService().PlaceHeadquartersOrderAsync(orders, GaskaDeliveryCourier.FedexDropshippingPobranie);

            Assert.False(result.IsSuccessful);
            await _gaska.DidNotReceive().CreateOrder(Arg.Any<GaskaCreateOrderRequest>(), Arg.Any<CancellationToken>());
        }
    }
}
