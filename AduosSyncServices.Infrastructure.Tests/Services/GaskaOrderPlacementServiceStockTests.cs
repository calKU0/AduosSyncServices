using AduosSyncServices.Contracts.Clients;
using AduosSyncServices.Contracts.DTOs.GaskaApi;
using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Contracts.OrderPlacement;
using AduosSyncServices.Infrastructure.Services;
using AduosSyncServices.Infrastructure.Tests.TestSupport;
using NSubstitute;
using Xunit;

namespace AduosSyncServices.Infrastructure.Tests.Services
{
    // Covers the stock-availability check and its per-order greedy allocation - the logic that decides
    // which orders can be placed at Gąska and which must be skipped.
    public class GaskaOrderPlacementServiceStockTests
    {
        private readonly IOrderRepository _orderRepo = Substitute.For<IOrderRepository>();
        private readonly IProductRepository _productRepo = Substitute.For<IProductRepository>();
        private readonly IGaskaApiClient _gaska = Substitute.For<IGaskaApiClient>();
        private readonly IAllegroApiClient _allegro = Substitute.For<IAllegroApiClient>();

        private GaskaOrderPlacementService CreateService() =>
            // productIntervalSeconds: 0 so the rate-limit pause between product lookups is a no-op.
            new(_orderRepo, _productRepo, _gaska, _allegro, productIntervalSeconds: 0);

        private void SetupProducts(params Product[] products) =>
            _productRepo.GetProductsByIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
                .Returns(products.ToList());

        private void SetupStock(int integrationId, float inStock) =>
            _gaska.GetProduct(integrationId, "pl", Arg.Any<CancellationToken>())
                .Returns(new GaskaGetProductResponse { Result = 0, Product = new ApiProduct { InStock = inStock } });

        [Fact]
        public async Task CheckStockAsync_AllInStock_IsSuccessfulWithNoShortages()
        {
            var product = OrderTestData.Product(id: 1, integrationId: 101, code: "P1");
            SetupProducts(product);
            SetupStock(101, inStock: 10);
            var orders = new[] { OrderTestData.Order(1, "A-1", new[] { (productId: 1, quantity: 4) }) };

            var result = await CreateService().CheckStockAsync(orders);

            Assert.True(result.IsSuccessful);
            Assert.Empty(result.Shortages);
            Assert.Empty(result.ShortagesByOrderId);
        }

        [Fact]
        public async Task CheckStockAsync_AggregatesQuantityAcrossOrders_ForSameProduct()
        {
            // Two orders each need 3 of the same product; only 5 in stock => 6 requested > 5 available.
            var product = OrderTestData.Product(id: 1, integrationId: 101, code: "P1");
            SetupProducts(product);
            SetupStock(101, inStock: 5);
            var orders = new[]
            {
                OrderTestData.Order(1, "A-1", new[] { (productId: 1, quantity: 3) }),
                OrderTestData.Order(2, "A-2", new[] { (productId: 1, quantity: 3) })
            };

            var result = await CreateService().CheckStockAsync(orders);

            Assert.False(result.IsSuccessful);
            var shortage = Assert.Single(result.Shortages);
            Assert.Equal("P1", shortage.ProductCode);
            Assert.Equal(6, shortage.RequestedQty);
            Assert.Equal(5, shortage.AvailableQty);
            // GetProduct is called once per DISTINCT product, not once per order line.
            await _gaska.Received(1).GetProduct(101, "pl", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task CheckStockAsync_GreedyAllocation_KeepsFirstOrderFeasible_SkipsSecond()
        {
            // Stock 5. Order1 wants 5 (fits, reserves all), Order2 wants 3 (nothing left) => only Order2 short.
            var product = OrderTestData.Product(id: 1, integrationId: 101, code: "P1");
            SetupProducts(product);
            SetupStock(101, inStock: 5);
            var orders = new[]
            {
                OrderTestData.Order(1, "A-1", new[] { (productId: 1, quantity: 5) }),
                OrderTestData.Order(2, "A-2", new[] { (productId: 1, quantity: 3) })
            };

            var result = await CreateService().CheckStockAsync(orders);

            Assert.False(result.IsSuccessful);
            Assert.False(result.ShortagesByOrderId.ContainsKey(1));
            Assert.True(result.ShortagesByOrderId.ContainsKey(2));
        }

        [Fact]
        public async Task CheckStockAsync_SkippedOrderDoesNotConsumeStock_FromLaterFeasibleOrder()
        {
            // Stock 5. Order1 wants 10 (short, must NOT reserve). Order2 wants 5 => still fits.
            var product = OrderTestData.Product(id: 1, integrationId: 101, code: "P1");
            SetupProducts(product);
            SetupStock(101, inStock: 5);
            var orders = new[]
            {
                OrderTestData.Order(1, "A-1", new[] { (productId: 1, quantity: 10) }),
                OrderTestData.Order(2, "A-2", new[] { (productId: 1, quantity: 5) })
            };

            var result = await CreateService().CheckStockAsync(orders);

            Assert.True(result.ShortagesByOrderId.ContainsKey(1));
            Assert.False(result.ShortagesByOrderId.ContainsKey(2));
        }

        [Fact]
        public async Task CheckStockAsync_ReportsProgressPerDistinctProduct()
        {
            var products = new[]
            {
                OrderTestData.Product(id: 1, integrationId: 101, code: "P1"),
                OrderTestData.Product(id: 2, integrationId: 102, code: "P2")
            };
            SetupProducts(products);
            SetupStock(101, inStock: 10);
            SetupStock(102, inStock: 0);
            var orders = new[] { OrderTestData.Order(1, "A-1", new[] { (productId: 1, quantity: 1), (productId: 2, quantity: 1) }) };

            var reports = new List<StockCheckProgressItem>();
            var progress = new Progress<StockCheckProgressItem>(reports.Add);
            // Progress<T> posts callbacks to the captured SynchronizationContext; drain via a plain await.
            await CreateService().CheckStockAsync(orders, progress);
            await Task.Yield();

            Assert.Contains(reports, r => r.ProductCode == "P1" && r.Status == StockCheckItemStatus.Available);
            Assert.Contains(reports, r => r.ProductCode == "P2" && r.Status == StockCheckItemStatus.Insufficient);
        }
    }
}
