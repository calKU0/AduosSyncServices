using AduosSyncServices.Contracts.Models;

namespace AduosSyncServices.Contracts.Interfaces
{
    public interface IOrderRepository
    {
        public Task SaveAllegroOrder(AllegroOrder order);

        public Task MarkAsOrderedInExternalCompany(int orderId, int externalOrderId, bool isDropshipping);

        // Deletes a manual order that hasn't been placed with the supplier yet. Returns false (no-op)
        // when the order isn't manual or has already been sent to Gąska - the guard lives in the SP.
        public Task<bool> DeleteManualOrder(int orderId);

        // Assigns (or clears, when internalStatusId is null) the internal status for the given orders.
        public Task SetOrdersInternalStatus(IReadOnlyCollection<int> orderIds, int? internalStatusId);

        public Task<List<AllegroOrder>> GetOrdersToUpdateExternalInfo();

        public Task<List<AllegroOrder>> GetAllOrdersForExternalCompany();

        public Task UpdateOrderExternalInfo(AllegroOrder order);

        public Task<List<AllegroOrder>> GetOrdersToUpdateInAllegro();

        public Task SetEmailSent(int orderId);
    }
}