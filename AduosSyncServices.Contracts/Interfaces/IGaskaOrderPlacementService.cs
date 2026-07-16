using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Contracts.OrderPlacement;

namespace AduosSyncServices.Contracts.Interfaces
{
    public interface IGaskaOrderPlacementService
    {
        Task<StockCheckResult> CheckStockAsync(
            IReadOnlyCollection<AllegroOrder> orders,
            IProgress<StockCheckProgressItem>? itemProgress = null,
            CancellationToken ct = default);

        Task<HeadquartersOrderPlacementResult> PlaceHeadquartersOrderAsync(
            IReadOnlyCollection<AllegroOrder> orders,
            GaskaDeliveryCourier courier,
            IProgress<string>? statusProgress = null,
            CancellationToken ct = default);

        Task<CustomerOrdersPlacementResult> PlaceCustomerOrdersAsync(
            IReadOnlyCollection<AllegroOrder> orders,
            GaskaDeliveryCourier courier,
            IReadOnlyDictionary<int, decimal> codAmountsByAllegroOrderId,
            IProgress<string>? statusProgress = null,
            CancellationToken ct = default);
    }
}
