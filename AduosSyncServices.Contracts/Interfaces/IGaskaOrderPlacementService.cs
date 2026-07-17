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

        /// <param name="skipStockCheck">Pass true when the caller has just run CheckStockAsync itself
        /// (e.g. to drive a live UI checklist) - avoids re-querying Gąska for every product a second
        /// time, which would double both the API calls and the per-product rate-limit delay.</param>
        Task<HeadquartersOrderPlacementResult> PlaceHeadquartersOrderAsync(
            IReadOnlyCollection<AllegroOrder> orders,
            GaskaDeliveryCourier courier,
            bool skipStockCheck = false,
            IProgress<string>? statusProgress = null,
            CancellationToken ct = default);

        /// <param name="skipStockCheck">See PlaceHeadquartersOrderAsync.</param>
        Task<CustomerOrdersPlacementResult> PlaceCustomerOrdersAsync(
            IReadOnlyCollection<AllegroOrder> orders,
            GaskaDeliveryCourier courier,
            IReadOnlyDictionary<int, decimal> codAmountsByAllegroOrderId,
            bool skipStockCheck = false,
            IProgress<string>? statusProgress = null,
            CancellationToken ct = default);
    }
}
